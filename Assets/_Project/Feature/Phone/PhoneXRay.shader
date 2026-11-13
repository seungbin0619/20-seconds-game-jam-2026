// 받지 못했을 때 벽이나 물건에 가려진 폰의 형상을 단색으로 비춰 보이게 한다.
// 깊이와 상관없이 항상 위에 그린다. 형상은 PhoneDevice가 플레이어 쪽으로 세우고 키운다.
Shader "MissedCall/PhoneXRay" {
    Properties {
        _Color ("Color", Color) = (1, 0.42, 0.34, 0.85)
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
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                return _Color;
            }
            ENDHLSL
        }
    }
}
