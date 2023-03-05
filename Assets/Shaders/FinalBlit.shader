Shader "ConvolutionBloom/FinalBlit"
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
            Blend One One
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
            Texture2D _BloomTex;

            float FFTBloomIntensity;

            float4 frag (v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = i.uv;
                uv.x *= 0.5;
                uv.y *= 1 / aspect;
                return float4(_MainTex.Sample(sampler_LinearClamp, uv).rgb * FFTBloomIntensity, 1.0);
            }
            ENDHLSL
        }
    }
}
