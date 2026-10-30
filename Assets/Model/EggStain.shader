Shader "Game/EggStain"
{
    Properties
    {
        _EggWhiteColor ("Egg White Color", Color) = (0.98, 0.96, 0.81, 1)
        _YolkColor ("Yolk Color", Color) = (1, 0.65, 0.04, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        inputTexture ("Input Texture", 2D) = "white" {}
        [Toggle] _UseInputTexture ("Use Input Texture", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -2, -2
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(inputTexture);
            SAMPLER(samplerinputTexture);
            CBUFFER_START(UnityPerMaterial)
                half4 _EggWhiteColor;
                half4 _YolkColor;
                half _Opacity;
                half _UseInputTexture;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz + input.normalOS * 0.002);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                if (_UseInputTexture > 0.5)
                {
                    if (input.uv.x < 0 || input.uv.x > 1 || input.uv.y < 0 || input.uv.y > 1)
                        return half4(0, 0, 0, 0);
                    half4 image = SAMPLE_TEXTURE2D(inputTexture, samplerinputTexture, input.uv);
                    image.a *= _Opacity;
                    return image;
                }
                float2 p = input.uv * 2.0 - 1.0;
                float angle = atan2(p.y, p.x);
                float edge = length(p) / (0.8 + 0.10 * sin(angle * 5.0) + 0.08 * sin(angle * 9.0 + 1.0));
                float white = 1.0 - smoothstep(0.90, 1.0, edge);
                float yolk = 1.0 - smoothstep(0.28, 0.38, length(p - float2(0.05, 0.04)));
                float3 color = lerp(_EggWhiteColor.rgb, _YolkColor.rgb, yolk);
                return half4(color, white * _Opacity);
            }
            ENDHLSL
        }
    }
}
