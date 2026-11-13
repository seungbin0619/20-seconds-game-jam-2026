// 풀스크린 셰이딩 (game-design.md "아트 방향: Last Ring").
// URP Full Screen Pass 렌더러 기능에서 그린다. 스타일 브랜치(style/*)마다 Look()만 다르다.
// 값은 머티리얼이 아니라 ScreenFxDriver가 전역 셰이더 값(_McFx*)으로 매 프레임 넣는다.
//
// 스타일: 손그림 외곽선 + 수채 (style/sketch). 외곽선을 연필처럼 끊기고 떨리게(라인 보일) 긋고,
// 면은 종이 결 위에 묽게 번진 물감처럼 칠한다. 조여 올수록 선이 빨리, 크게 떤다.
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

            // 종이색(sRGB). 살짝 차가운 흰색.
            static const half3 Paper = half3(0.914, 0.925, 0.918);
            // 선이 다시 그려지는 횟수(1/s). 판이 끝나 갈수록 빨라진다.
            static const float BoilRate = 6;
            // 선이 흔들리는 폭(1080p 기준 px)
            static const float Wobble = 1.5;

            float ValueNoise(float2 x) {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(
                    lerp(Hash12(i), Hash12(i + float2(1, 0)), f.x),
                    lerp(Hash12(i + float2(0, 1)), Hash12(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 x) {
                return ValueNoise(x) * 0.55 + ValueNoise(x * 2.1 + 17.0) * 0.3 + ValueNoise(x * 4.3 + 41.0) * 0.15;
            }

            half Edge(float2 uv, float2 texel) {
                float depth = EyeDepth(uv);
                float3 normal = SampleSceneNormals(uv);
                float2 offsets[4] = {
                    float2(texel.x, 0), float2(-texel.x, 0),
                    float2(0, texel.y), float2(0, -texel.y)
                };

                half depthEdge = 0;
                half normalEdge = 0;
                [unroll]
                for (int i = 0; i < 4; i++) {
                    float2 nuv = uv + offsets[i];
                    depthEdge = max(depthEdge, (EyeDepth(nuv) - depth) / depth);
                    normalEdge = max(normalEdge, 1 - dot(normal, SampleSceneNormals(nuv)));
                }

                half edge = max(
                    smoothstep(_McFxOutline.y, _McFxOutline.y * 1.5, depthEdge),
                    smoothstep(_McFxOutline.z, _McFxOutline.z * 1.5, normalEdge));
                half fade = 1 - saturate((depth - _McFxOutline.w) / _McFxOutline.w);
                return edge * fade;
            }

            // 연필 선: 보일 프레임마다 선 위치를 노이즈로 밀고, 두 번 겹쳐 긋고, 군데군데 흐리게 끊는다.
            half Pencil(ScreenPixel p, float boil, float shake) {
                if (_McFxOutline.x <= 0) {
                    return 0;
                }

                float width = max(1, _McFxOutline.x * 0.75 * p.scale);
                float2 grid = p.uv * float2(_ScreenSize.x * _ScreenSize.w, 1) * 9;
                float2 push = (float2(ValueNoise(grid + boil * 7.1), ValueNoise(grid + boil * 3.7 + 50)) - 0.5);
                float2 offset = push * Wobble * shake * p.scale * p.texel * 2;

                half a = Edge(p.uv + offset, p.texel * width);
                half b = Edge(p.uv - offset * 1.6 + p.texel * 0.7, p.texel * width);
                half stroke = max(a, b * 0.6);
                // 선 농도가 노이즈를 따라 오르내린다.
                half pressure = lerp(0.45, 1, ValueNoise(p.uv * _ScreenSize.y * 0.05 + boil * 13));
                return stroke * pressure;
            }

            half3 Watercolor(half3 linearColor, ScreenPixel p) {
                half3 srgb = LinearToSRGB(saturate(linearColor));

                // 물감은 묽게: 채도를 조금 낮추고 종이색 쪽으로 밝힌다.
                half lum = Luminance(srgb);
                srgb = lerp(lum.xxx, srgb, 0.8);
                srgb = lerp(srgb, Paper, 0.18);

                // 물감이 고인 얼룩과 종이 결.
                float2 q = p.uv * float2(_ScreenSize.x * _ScreenSize.w, 1);
                half blot = Fbm(q * 6);
                half grain = ValueNoise(q * 240) * 0.6 + ValueNoise(q * 520) * 0.4;
                srgb *= lerp(0.88, 1.06, blot);
                srgb *= lerp(0.93, 1.03, grain);
                return srgb;
            }

            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _McFxTime.x;
                float tight = _McFxTime.y;
                float low = _McFxTime.z;

                ScreenPixel p = MakePixel(input.texcoord);
                half3 srgb = Watercolor(SampleScene(p), p);

                float boil = floor(t * BoilRate * (1 + tight + low * 2));
                half stroke = Pencil(p, boil, 1 + low * 2.5);
                half3 ink = LinearToSRGB(_McFxOutlineColor.rgb);
                srgb = lerp(srgb, ink, stroke * 0.9);

                half3 color = SRGBToLinear(saturate(srgb));
                return half4(ApplyTense(color, p), 1);
            }
            ENDHLSL
        }
    }
}
