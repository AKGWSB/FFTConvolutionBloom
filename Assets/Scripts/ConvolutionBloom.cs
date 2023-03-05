using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ConvolutionBloom : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        public ConvolitonBloomSettings BloomSettings;

        RenderTexture m_sourceTexture;
        RenderTexture m_kernelTexture;
        int m_sourceFrequencyTextureID;
        int m_kernelFrequencyTextureID;

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_sourceFrequencyTextureID = Shader.PropertyToID("SourceFrequencyTexture");
            m_kernelFrequencyTextureID = Shader.PropertyToID("KernelFrequencyTexture");

            RenderTextureDescriptor desc = new RenderTextureDescriptor();
            desc.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            desc.colorFormat = RenderTextureFormat.ARGBFloat;
            desc.width = 512;
            desc.height = 512;
            desc.volumeDepth = 1;
            desc.msaaSamples = 1;
            desc.enableRandomWrite = true;

            m_sourceTexture = RenderTexture.GetTemporary(desc);
            m_kernelTexture = RenderTexture.GetTemporary(desc);

            desc.width = 512 * 2;
            cmd.GetTemporaryRT(m_sourceFrequencyTextureID, desc);
            cmd.GetTemporaryRT(m_kernelFrequencyTextureID, desc);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // FFT
            var cs = BloomSettings.FFTComputeShader; 
            int kFFTForwardHorizontal = cs.FindKernel("FFTForwardHorizontal");
            int kFFTInverseHorizontal = cs.FindKernel("FFTInverseHorizontal");
            int kFFTVertical = cs.FindKernel("FFTVertical");
            int kConvolution = cs.FindKernel("Convolution");
            int kKernelTransform = cs.FindKernel("KernelTransform");
            int kTwoForOneFFTForwardHorizontal = cs.FindKernel("TwoForOneFFTForwardHorizontal");
            int kTwoForOneFFTInverseHorizontal = cs.FindKernel("TwoForOneFFTInverseHorizontal");
            int kTwoForOneFFTForwardHorizontalRadix8 = cs.FindKernel("TwoForOneFFTForwardHorizontalRadix8");
            int kTwoForOneFFTInverseHorizontalRadix8 = cs.FindKernel("TwoForOneFFTInverseHorizontalRadix8");
            int kFFTVerticalRadix8 = cs.FindKernel("FFTVerticalRadix8");

            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.SetGlobalFloat("FFTBloomIntensity", BloomSettings.Intensity);
            cmd.SetGlobalFloat("FFTBloomThreshold", BloomSettings.Threshold);
            Vector4 kernelGenParam = new Vector4(BloomSettings.KernelPositionOffset.x, BloomSettings.KernelPositionOffset.y, BloomSettings.KernelSizeScale.x, BloomSettings.KernelSizeScale.y);
            cmd.SetGlobalVector("FFTBloomKernelGenParam", kernelGenParam);
            Vector4 kernelGenParam1 = new Vector4(BloomSettings.KernelDistanceExp, BloomSettings.KernelDistanceExpClampMin, BloomSettings.KernelDistanceExpScale, BloomSettings.KernelImageUseLuminanceAsRGB ? 1.0f : 0.0f);
            cmd.SetGlobalVector("FFTBloomKernelGenParam1", kernelGenParam1);

            // 降采样 kernel 图像
            cmd.Blit(BloomSettings.KernelTexture, m_kernelTexture, BloomSettings.KernelGenerateMaterial);

            // 对 kernel 做变换
            cmd.SetComputeTextureParam(cs, kKernelTransform, "SourceTexture", m_kernelTexture);
            cmd.DispatchCompute(cs, kKernelTransform, 512 / 8, 256 / 8, 1); 
            
            // 降采样 scene color
            cmd.Blit(renderingData.cameraData.renderer.cameraColorTarget, m_sourceTexture, BloomSettings.SourceGenerateMaterial);

            // 对卷积核做 FFT
            // 水平
            cmd.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontalRadix8, "SourceTexture", m_kernelTexture);
            cmd.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontalRadix8, "FrequencyTexture", m_kernelFrequencyTextureID);
            cmd.DispatchCompute(cs, kTwoForOneFFTForwardHorizontalRadix8, 512, 1, 1); 
            // 竖直
            cmd.SetComputeFloatParam(cs, "IsForward", 1.0f);
            cmd.SetComputeTextureParam(cs, kFFTVerticalRadix8, "FrequencyTexture", m_kernelFrequencyTextureID);
            cmd.DispatchCompute(cs, kFFTVerticalRadix8, 512, 1, 1); 

            // 对原图像做 FFT
            // 水平
            cmd.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontalRadix8, "SourceTexture", m_sourceTexture);
            cmd.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontalRadix8, "FrequencyTexture", m_sourceFrequencyTextureID);
            cmd.DispatchCompute(cs, kTwoForOneFFTForwardHorizontalRadix8, 512, 1, 1); 
            // 竖直
            cmd.SetComputeFloatParam(cs, "IsForward", 1.0f);
            cmd.SetComputeTextureParam(cs, kFFTVerticalRadix8, "FrequencyTexture", m_sourceFrequencyTextureID);
            cmd.DispatchCompute(cs, kFFTVerticalRadix8, 512, 1, 1); 

            // 频域卷积
            cmd.SetComputeTextureParam(cs, kConvolution, "SourceFrequencyTexture", m_sourceFrequencyTextureID);
            cmd.SetComputeTextureParam(cs, kConvolution, "KernelFrequencyTexture", m_kernelFrequencyTextureID);
            cmd.DispatchCompute(cs, kConvolution, 512 / 8, 512 / 8, 1); 

            // 还原原图像
            // 竖直
            cmd.SetComputeFloatParam(cs, "IsForward", 0.0f);
            cmd.SetComputeTextureParam(cs, kFFTVerticalRadix8, "FrequencyTexture", m_sourceFrequencyTextureID);
            cmd.DispatchCompute(cs, kFFTVerticalRadix8, 512, 1, 1); 
            // 水平
            cmd.SetComputeTextureParam(cs, kTwoForOneFFTInverseHorizontalRadix8, "FrequencyTexture", m_sourceFrequencyTextureID);
            cmd.DispatchCompute(cs, kTwoForOneFFTInverseHorizontalRadix8, 512, 1, 1);       

            // final blit
            cmd.Blit(m_sourceFrequencyTextureID, renderingData.cameraData.renderer.cameraColorTarget, BloomSettings.FinalBlitMaterial);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            RenderTexture.ReleaseTemporary(m_sourceTexture);
            RenderTexture.ReleaseTemporary(m_kernelTexture);
            cmd.ReleaseTemporaryRT(m_sourceFrequencyTextureID);
            cmd.ReleaseTemporaryRT(m_kernelFrequencyTextureID);
        }
    }

    CustomRenderPass m_ScriptablePass;
    public ConvolitonBloomSettings BloomSettings;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        m_ScriptablePass.BloomSettings = BloomSettings;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


