Shader "ConvolutionBloom/SourceGenerate"
{
    Properties
    {
        _MainTex ("_MainTex", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            SAMPLER(sampler_LinearClamp);
            Texture2D _MainTex;
            float FFTBloomThreshold;

            float Luma(float3 color)
            {
                return dot(color, float3(0.299f, 0.587f, 0.114f));
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                uv.y = 1.0 - uv.y;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                if(_ScreenParams.x > _ScreenParams.y)
                {
                    uv.y *= aspect;
                }
                else
                {
                    uv.x /= aspect;
                }
                if(uv.y > 1.0) return float4(0, 0, 0, 0);

            #define USE_FILTER 1
            #if USE_FILTER
                float3 color = float3(0, 0, 0);
                float weight = 0.0;
                const float2 offsets[4] = {float2(-0.5, -0.5), float2(-0.5, +0.5), float2(+0.5, -0.5), float2(+0.5, +0.5)};
                for(int i=0; i<4; i++)
                {
                    float2 offset = 2.0 * offsets[i] / _ScreenParams.xy;
                    float3 c = _MainTex.Sample(sampler_LinearClamp, uv + offset).rgb;
                    float lu = Luma(c);
                    float w = 1.0 / (1.0 + lu);
                    color += c * w;
                    weight += w;
                }
                color /= weight;
            #else
                float3 color = _MainTex.Sample(sampler_LinearClamp, uv).rgb;
            #endif  // USE_FILTER

                float luma = Luma(color);
                float scale = saturate(luma - FFTBloomThreshold);
                float3 finalColor = color * scale;
                return float4(finalColor, 0.0);
            }
            ENDHLSL
        }
    }
}
