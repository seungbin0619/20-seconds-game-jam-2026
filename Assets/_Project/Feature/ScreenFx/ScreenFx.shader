// 풀스크린 셰이딩 (game-design.md "아트 방향: Last Ring").
// URP Full Screen Pass 렌더러 기능에서 그린다. 스타일 브랜치(style/*)마다 Look()만 다르다.
// 값은 머티리얼이 아니라 ScreenFxDriver가 전역 셰이더 값(_McFx*)으로 매 프레임 넣는다.
//
// 스타일: 손그림 외곽선 + 수채 (style/sketch). 외곽선을 연필처럼 끊기고 떨리게(라인 보일) 긋고,
// 면은 종이 결 위에 묽게 번진 물감처럼 칠한다. 시간 연출은 UI(하프톤, 각진 카드)에 맞춘다 (11/22):
// 비네트 대신 가장자리부터 하프톤 점이 차올라 화면을 조이고, 박자마다 점이 부풀며, 마지막 구간에는
// 만화 집중선이 가장자리에서 번쩍이고 선이 급하게 덧그려진다. 종이는 저녁에서 밤으로 어두워진다.
// 공통 ApplyTense는 쓰지 않는다.
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
            // 가장자리부터 차오르는 하프톤 점 색(sRGB). UI 배경색 #10161C.
            static const half3 DotInk = half3(0.063, 0.086, 0.11);
            // 집중선 색(sRGB). UI 본문색 #F3EEE3.
            static const half3 LineInk = half3(0.953, 0.933, 0.89);
            // 하프톤 칸 크기(1080p 기준 px)
            static const float DotCell = 13;
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

            // 비네트와 먹물 대신: 가장자리부터 45도 격자의 하프톤 점이 차오른다. 남은 시간에만 따라 넓어지고
            // 물러나지 않는다. 박자(pulse)마다 점이 잠깐 부푼다. 0초에는 가운데만 남는다.
            half3 HalftoneCreep(half3 srgb, ScreenPixel p, float t, half creep, half pulse) {
                if (creep <= 0) {
                    return srgb;
                }

                float2 px = p.uv * _ScreenSize.xy / max(0.001, p.scale);
                const float c = 0.70710678;
                float2 grid = float2(px.x * c - px.y * c, px.x * c + px.y * c) / DotCell;
                float2 f = frac(grid) - 0.5;

                // 가장자리 선이 살짝 일렁이며 들어온다.
                half wobble = (Fbm(p.uv * 3 + t * 0.06) - 0.5) * 0.14;
                half reach = p.radius + wobble;
                half front = 1.15 - creep * 0.9 - pulse * 0.04;
                half g = saturate((reach - front) / 0.45);
                g = saturate(g + pulse * 0.18 * step(0.001, g));
                half radius = sqrt(g) * 0.74;
                half d = length(f) - radius;
                half aa = max(fwidth(d), 1e-4);
                half dotMask = saturate(0.5 - d / aa) * step(0.001, g);
                return lerp(srgb, DotInk, dotMask * 0.94);
            }

            // 마지막 구간: 화면 가장자리에서 가운데로 뻗는 만화 집중선. 박자마다 새로 그어지고, 끝에 가까울수록 깊이 들어온다.
            half3 FocusLines(half3 srgb, ScreenPixel p, float beat, half low) {
                if (low <= 0) {
                    return srgb;
                }

                float aspect = _ScreenSize.x * _ScreenSize.w;
                float2 v = (p.uv - 0.5) * float2(aspect, 1);
                float angle = atan2(v.y, v.x) / 6.2831853 + 0.5;
                const float count = 110;
                float k = angle * count + Hash12(float2(beat, 5)) * count;
                float id = floor(k);
                half w = Hash12(float2(id, beat + 11));
                // 일부 칸에만 선을 긋는다. 선마다 시작 깊이가 다르다.
                half on = step(0.5, w);
                half start = lerp(0.98, 0.5, low) + Hash12(float2(id, beat + 23)) * 0.2;
                half along = saturate((p.radius - start) / 0.35);
                // 안쪽으로 갈수록 가늘어지는 쐐기
                half width = (0.12 + (w - 0.5) * 0.5) * along;
                half lineMask = step(abs(frac(k) - 0.5), width * 0.5) * on * step(0.001, along);
                return lerp(srgb, LineInk, lineMask * 0.75 * saturate(low * 1.5));
            }

            // 정전: 달빛처럼 푸르게 어둡히고, 폰 화면 둘레만 차가운 불빛으로 밝힌다.
            half3 Blackout(half3 srgb, ScreenPixel p) {
                half amount = _McFxBlackout.x;
                if (amount <= 0) {
                    return srgb;
                }

                float aspect = _ScreenSize.x * _ScreenSize.w;
                float2 d = (p.uv - _McFxBlackout.yz) * float2(aspect, 1);
                half r = max(0.01, _McFxGlow.x);
                half glow = exp(-dot(d, d) / (r * r)) * _McFxBlackout.w;
                half3 dark = srgb * (1 - amount) * half3(0.7, 0.82, 1.0);
                half3 lit = srgb * half3(0.85, 1.0, 1.1);
                return lerp(dark, lit, saturate(glow));
            }

            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _McFxTime.x;
                float tight = _McFxTime.y;
                float low = _McFxTime.z;

                ScreenPixel p = MakePixel(input.texcoord);
                half3 srgb = Watercolor(SampleScene(p), p);
                // 시간대: 한밤은 푸르게 어둡고, 흐린 아침은 채도가 빠진다. 값이 없으면(0) 그대로.
                if (any(_McFxMood != 0)) {
                    srgb = lerp(srgb, Luminance(srgb).xxx, _McFxMood.a) * _McFxMood.rgb;
                }

                srgb = Blackout(srgb, p);
                // 종이가 시간이 갈수록 어둡고 차가워진다 (저녁에서 밤으로).
                srgb *= lerp(half3(1, 1, 1), PaperDusk, tight * 0.75);

                float boil = floor(t * BoilRate * (1 + tight + low * 2));
                half stroke = max(Pencil(p, boil, 1 + low * 2.5), Scribble(p, boil, low));
                half3 ink = LinearToSRGB(_McFxOutlineColor.rgb);
                srgb = lerp(srgb, ink, stroke * 0.9);

                srgb = lerp(srgb, Luminance(srgb).xxx, _McFxTense.y);
                half pulse = saturate(_McFxTense.z * 2.5);
                srgb = HalftoneCreep(srgb, p, t, saturate(tight * 0.4 + low * 0.6), pulse);
                // 집중선은 박자(진동 간격 1초)의 절반마다 새로 긋는다.
                srgb = FocusLines(srgb, p, floor(t * 2), low);
                return half4(SRGBToLinear(saturate(srgb)), 1);
            }
            ENDHLSL
        }
    }
}
