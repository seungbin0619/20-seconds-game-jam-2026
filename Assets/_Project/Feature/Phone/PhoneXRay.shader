// 받지 못했을 때 벽이나 물건에 가려진 폰의 형상을 단색으로 비춰 보이게 한다.
// 깊이와 상관없이 항상 위에 그린다. 폰이 누운 그대로의 모양에 일렁이는 효과를 준다:
// 겉면이 숨 쉬듯 부풀었다 줄고, 밝은 물결 띠가 표면을 따라 흐른다.
Shader "MissedCall/PhoneXRay" {
    Properties {
        _Color ("Color", Color) = (1, 0.42, 0.34, 0.85)
        _Speed ("Wave Speed", Float) = 6
        _Frequency ("Wave Frequency (1/m)", Float) = 45
        _Swell ("Swell (m)", Float) = 0.006
        _MinAlpha ("Min Alpha Scale", Range(0, 1)) = 0.45
    }

    SubShader {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+100" }

        Pass {
            Name "XRay"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Speed;
                float _Frequency;
                float _Swell;
                half _MinAlpha;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 월드 단위로 부풀린다. 물체 단위로 하면 납작한 상자의 비율 때문에 두께 쪽만 거의 안 움직인다.
                float swell = 0.5 + 0.5 * sin(_Time.y * _Speed * 0.5);
                positionWS += normalWS * _Swell * swell;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                float3 p = input.positionWS;
                float wave = sin((p.x + p.z) * _Frequency - _Time.y * _Speed);
                float wave2 = sin((p.x - p.z) * _Frequency * 0.6 + _Time.y * _Speed * 0.7);
                half shimmer = lerp(_MinAlpha, 1.0h, saturate(0.5h + 0.3h * wave + 0.2h * wave2));
                return half4(_Color.rgb, _Color.a * shimmer);
            }
            ENDHLSL
        }
    }
}
