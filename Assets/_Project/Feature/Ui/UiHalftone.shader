// 게임 UI 도형과 하프톤. 스프라이트 없이 셰이더가 도형을 그린다.
// 정점 데이터(UiShape가 채움):
//   color          채우기 색 (rgb), a는 CanvasGroup 등에서 내려온 투명도
//   uv0            도형 안 위치 0~1
//   uv1 (x,y,z,w)  도형 크기(px), 모서리 깎기(px), 기울임(px, 위쪽이 오른쪽으로)
//   uv2 (x,y,z,w)  하프톤 세기 0~1, 하프톤 방향(라디안, 9 = 고르게, 8 = 가운데에서 바깥으로, 7 = 가운데가 가장 큼),
//                  점 색조(+면 _DotLight, -면 검정, 1.5보다 크면 채우기 색(정점 색)으로 진하기 z-1.5), 테두리 두께(px, 0이면 채움)
//   uv3 (x)        채우기 불투명도 (0이면 점만 보인다)
Shader "MissedCall/UiHalftone" {
    Properties {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _DotLight ("Dot Light", Color) = (0.15, 0.22, 0.29, 1)
        _DotCell ("Dot Cell (px at 720p)", Float) = 7
        _DotAngle ("Dot Grid Angle (deg)", Float) = 45
        _DotDrift ("Dot Drift (px/s)", Vector) = (6, 3, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 tone : TEXCOORD2;
                float4 screen : TEXCOORD3;
                float fill : TEXCOORD4;
            };

            fixed4 _DotLight;
            float _DotCell;
            float _DotAngle;
            float4 _DotDrift;
            // 일시정지 중에도 흐르는 시간. UiTilt가 넣는다.
            float _McUiTime;

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv0;
                o.shape = v.uv1;
                o.tone = v.uv2;
                o.screen = ComputeScreenPos(o.pos);
                o.fill = v.uv3.x;
                return o;
            }

            // 모서리 두 곳(왼쪽 위, 오른쪽 아래)을 깎고 기울인 사각형의 부호 거리(px). 안쪽이 음수.
            float ShapeDistance(float2 uv, float4 shape) {
                float2 size = max(shape.xy, 1.0);
                float2 p = (uv - 0.5) * size;
                // 기울임: 위로 갈수록 오른쪽으로 민 평행사변형
                p.x -= shape.w * (uv.y - 0.5);
                float2 halfSize = size * 0.5 - float2(abs(shape.w) * 0.5, 0.0);
                float2 q = abs(p) - halfSize;
                float box = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                // 깎은 모서리: 왼쪽 위(-x,+y), 오른쪽 아래(+x,-y)
                float c = shape.z;
                float diag = (abs(p.x - p.y) - (halfSize.x + halfSize.y - c)) * 0.70710678;
                bool cutCorner = (p.x < 0.0 && p.y > 0.0) || (p.x > 0.0 && p.y < 0.0);
                return c > 0.0 && cutCorner ? max(box, diag) : box;
            }

            float Halftone(v2f i) {
                float amount = i.tone.x;
                if (amount <= 0.001) {
                    return 0.0;
                }

                float g;
                if (i.tone.y > 8.5) {
                    g = 1.0;
                } else if (i.tone.y > 6.5 && i.tone.y < 7.5) {
                    // 가운데가 가장 크고 바깥으로 갈수록 작아진다 (터지는 모양)
                    g = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                } else if (i.tone.y > 7.5) {
                    // 가운데는 비우고 가장자리로 갈수록 커진다
                    g = saturate((length(i.uv - 0.5) * 2.0 - 0.35) / 0.8);
                } else {
                    float2 dir = float2(cos(i.tone.y), sin(i.tone.y));
                    g = saturate(0.5 + dot(i.uv - 0.5, dir));
                }

                g *= amount;
                float2 screenPx = i.screen.xy / i.screen.w * _ScreenParams.xy;
                float cell = _DotCell * _ScreenParams.y / 720.0;
                float a = radians(_DotAngle);
                float2x2 rot = float2x2(cos(a), -sin(a), sin(a), cos(a));
                float2 p = mul(rot, screenPx + _DotDrift.xy * _McUiTime) / cell;
                float2 f = frac(p) - 0.5;
                // 점 반지름은 세기의 제곱근에 비례 (면적이 세기에 비례)
                float radius = sqrt(g) * 0.62;
                float d = length(f) - radius;
                float aa = fwidth(d) + 1e-4;
                return saturate(0.5 - d / aa) * step(0.02, g);
            }

            fixed4 frag(v2f i) : SV_Target {
                float d = ShapeDistance(i.uv, i.shape);
                float aa = max(fwidth(d), 1e-4);
                float mask = saturate(0.5 - d / aa);
                float outline = i.tone.w;
                if (outline > 0.0) {
                    mask *= saturate(0.5 + (d + outline) / aa);
                }

                float fillA = i.fill;
                bool tinted = i.tone.z > 1.5;
                float dots = Halftone(i) * (tinted ? i.tone.z - 1.5 : abs(i.tone.z));
                float3 dotRgb = tinted ? i.color.rgb : i.tone.z >= 0.0 ? _DotLight.rgb : float3(0.0, 0.0, 0.0);
                // 채우기 위에 점을 얹는다 (over)
                float outA = dots + fillA * (1.0 - dots);
                float3 rgb = (dotRgb * dots + i.color.rgb * fillA * (1.0 - dots)) / max(outA, 1e-4);
                float alpha = outA * mask * i.color.a;
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
