// 풀스크린 셰이딩 (game-design.md "아트 방향: Last Ring").
// URP Full Screen Pass 렌더러 기능에서 그린다. 스타일 브랜치(style/*)마다 Look()만 다르다.
// 값은 머티리얼이 아니라 ScreenFxDriver가 전역 셰이더 값(_McFx*)으로 매 프레임 넣는다.
//
// 스타일: 손그림 외곽선 + 수채 (style/sketch). 외곽선을 연필처럼 끊기고 떨리게(라인 보일) 긋고,
// 면은 종이 결 위에 묽게 번진 물감처럼 칠한다. 시간 연출도 그림체 안에서 한다:
// 비네트 대신 가장자리부터 차오르는 빗금, 붉은 맥박 대신 번지는 붉은 물감, 종이가 저녁에서 밤으로
// 어두워지고, 마지막 구간에는 선이 급하게 덧그려진다. 공통 ApplyTense는 쓰지 않는다.
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
            // 시간이 다 됐을 때 종이에 곱하는 색. 어둡고 푸르다.
            static const half3 PaperDusk = half3(0.68, 0.74, 0.84);
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

            // 마지막 구간: 같은 경계를 크게 어긋나게 한 번 더 긋는다. 급하게 덧그린 선처럼 삐져나온다.
            half Scribble(ScreenPixel p, float boil, float low) {
                if (_McFxOutline.x <= 0 || low <= 0) {
                    return 0;
                }

                float width = max(1, _McFxOutline.x * p.scale);
                float2 grid = p.uv * float2(_ScreenSize.x * _ScreenSize.w, 1) * 4;
                float2 push = float2(ValueNoise(grid + boil * 5.3 + 90), ValueNoise(grid + boil * 2.9 + 130)) - 0.5;
                float2 offset = push * 9 * low * p.scale * p.texel;
                half gap = step(0.35, ValueNoise(p.uv * _ScreenSize.y * 0.03 + boil * 7));
                return Edge(p.uv + offset, p.texel * width * 1.3) * gap * low;
            }

            // 비네트 대신: 가장자리부터 연필 빗금이 차오르고, 더 세지면 반대 방향 빗금이 겹친다.
            half3 Hatch(half3 srgb, ScreenPixel p, float boil, half3 ink) {
                // 비네트 최대치(fxVignetteMax, 0.45 언저리)에서 1이 되도록 맞춘다.
                half amount = saturate(_McFxTense.x * 2.2);
                if (amount <= 0) {
                    return srgb;
                }

                float2 px = p.uv * _ScreenSize.xy / max(1, p.scale);
                half wobble = Fbm(p.uv * 5 + boil * 0.37) * 0.25;
                half reach = p.radius + wobble;
                half regionA = smoothstep(0.95 - amount * 0.4, 1.05 - amount * 0.25, reach);
                half regionB = smoothstep(1.05 - amount * 0.3, 1.15 - amount * 0.15, reach);

                float jitter = Hash12(float2(boil, 3)) * 7;
                half lineA = step(frac((px.x + px.y + jitter) / 7), 0.28);
                half lineB = step(frac((px.x - px.y + jitter * 1.7) / 9), 0.22);
                // 연필 농도가 군데군데 빠진다.
                half pressure = lerp(0.5, 1, ValueNoise(px * 0.08 + boil));

                half cover = max(lineA * regionA, lineB * regionB) * pressure;
                return lerp(srgb, ink, cover * 0.85);
            }

            // 붉은 맥박 대신: 가장자리부터 붉은 물감이 번진다. 번진 끝이 진하게 고인다.
            half3 RedWash(half3 srgb, ScreenPixel p, float t) {
                half pulse = saturate(_McFxTense.z * 2.5);
                if (pulse <= 0) {
                    return srgb;
                }

                half shape = p.radius + (Fbm(p.uv * 3.5 + t * 0.05) - 0.5) * 0.5;
                half front = 1.05 - pulse * 0.45;
                half wash = smoothstep(front, front + 0.25, shape);
                // 물감 가장자리에 안료가 고여 진한 띠가 생긴다.
                half rim = smoothstep(0.06, 0, abs(shape - front - 0.02)) * pulse;
                half3 pigment = LinearToSRGB(_McFxPulseColor.rgb);
                srgb *= lerp(1, pigment, wash * 0.65);
                return srgb * lerp(1, pigment * 0.7, rim * 0.4);
            }

            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _McFxTime.x;
                float tight = _McFxTime.y;
                float low = _McFxTime.z;

                ScreenPixel p = MakePixel(input.texcoord);
                half3 srgb = Watercolor(SampleScene(p), p);
                // 종이가 시간이 갈수록 어둡고 차가워진다 (저녁에서 밤으로).
                srgb *= lerp(half3(1, 1, 1), PaperDusk, tight * 0.75);

                float boil = floor(t * BoilRate * (1 + tight + low * 2));
                half stroke = max(Pencil(p, boil, 1 + low * 2.5), Scribble(p, boil, low));
                half3 ink = LinearToSRGB(_McFxOutlineColor.rgb);
                srgb = lerp(srgb, ink, stroke * 0.9);

                srgb = lerp(srgb, Luminance(srgb).xxx, _McFxTense.y);
                srgb = RedWash(srgb, p, t);
                srgb = Hatch(srgb, p, boil, ink);
                return half4(SRGBToLinear(saturate(srgb)), 1);
            }
            ENDHLSL
        }
    }
}
