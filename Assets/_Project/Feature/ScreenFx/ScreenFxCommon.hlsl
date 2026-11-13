// 스타일과 상관없이 모든 풀스크린 룩이 함께 쓰는 부분.
// 장면 색 읽기(색수차 포함)와 남은 시간에 따라 조여 오는 연출(채도 빼기, 붉은 맥박, 비네트).
// 스타일 셰이더는 SampleScene → 자기 룩 → ApplyTense 순서로 쓴다.
#ifndef MISSEDCALL_SCREENFX_COMMON
#define MISSEDCALL_SCREENFX_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

// x 외곽선 두께(px, 0이면 끔), y 깊이 문턱(비율), z 노멀 문턱, w 흐려지기 시작하는 거리(m)
float4 _McFxOutline;
// xy 떨림(px), z 밝기 단계 수(0이면 끊지 않음)
float4 _McFxShade;
// x 비네트, y 채도 빼기, z 붉은 맥박, w 색수차(화면 높이 비율)
float4 _McFxTense;
// x 실제 시간(초), y 조여 오는 정도(0~1, fxTenseStart부터), z 마지막 구간 정도(0~1, lowTimeWarning부터)
float4 _McFxTime;
float4 _McFxOutlineColor;
float4 _McFxPulseColor;

struct ScreenPixel {
    float2 uv;
    float2 texel;
    // 1080p 기준 px를 현재 해상도 px로 바꾸는 배율
    float scale;
    // 화면 가운데에서의 거리. 세로 0.5가 1이 아니라, 가로세로 비율을 맞춘 거리
    float radius;
};

ScreenPixel MakePixel(float2 uv) {
    ScreenPixel p;
    p.uv = uv;
    p.texel = _ScreenSize.zw;
    p.scale = _ScreenSize.y / 1080.0;
    float aspect = _ScreenSize.x * _ScreenSize.w;
    p.radius = length((uv - 0.5) * float2(aspect, 1));
    return p;
}

half3 SampleColor(float2 uv) {
    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
}

// 색수차: 가장자리로 갈수록 빨강과 파랑을 바깥, 안쪽으로 벌린다.
half3 SampleScene(ScreenPixel p) {
    float2 split = (p.uv - 0.5) * (_McFxTense.w * p.radius * 2);
    half3 color;
    color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p.uv + split).r;
    color.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p.uv).g;
    color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p.uv - split).b;
    return color;
}

float EyeDepth(float2 uv) {
    return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
}

float Hash12(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

half3 ApplyTense(half3 color, ScreenPixel p) {
    color = lerp(color, Luminance(color).xxx, _McFxTense.y);

    // 붉은 맥박: 밝기를 살린 채 붉게 물들이고, 가장자리일수록 세게.
    half edgeMask = smoothstep(0.1, 0.8, p.radius);
    half3 tint = _McFxPulseColor.rgb * (Luminance(color) * 1.6 + 0.04);
    color = lerp(color, tint, saturate(_McFxTense.z * lerp(0.35, 1, edgeMask)));

    // 비네트: 세질수록 어두운 테두리가 안쪽으로 좁혀 온다.
    half vignette = smoothstep(0.85 - _McFxTense.x * 0.6, 1.0, p.radius + 0.15);
    return lerp(color, _McFxOutlineColor.rgb, saturate(vignette * _McFxTense.x * 1.8));
}

#endif
