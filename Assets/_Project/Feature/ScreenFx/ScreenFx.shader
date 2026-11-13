// 풀스크린 셰이딩 (game-design.md "아트 방향: Last Ring").
// URP Full Screen Pass 렌더러 기능에서 그린다. 스타일 브랜치(style/*)마다 Look()만 다르다.
// 값은 머티리얼이 아니라 ScreenFxDriver가 전역 셰이더 값(_McFx*)으로 매 프레임 넣는다.
Shader "Hidden/MissedCall/ScreenFx" {
    SubShader {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass {
            Name "ScreenFx"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "ScreenFxCommon.hlsl"

            // 스타일 없음: 장면 색 그대로.
            half3 Look(half3 color, ScreenPixel p) {
                return color;
            }

            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ScreenPixel p = MakePixel(input.texcoord);
                half3 color = Look(SampleScene(p), p);
                return half4(ApplyTense(color, p), 1);
            }
            ENDHLSL
        }
    }
}
