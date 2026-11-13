using MissedCall.Core;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.ScreenFx {
    /// <summary>
    /// 풀스크린 셰이더(ScreenFx.shader)에 매 프레임 값을 넣는다. 기본 룩(외곽선, 음영 단계)은 늘 켜 두고,
    /// 판이 도는 동안 남은 시간에 따라 비네트, 채도 빼기, 붉은 맥박, 색수차, 외곽선 떨림을 올린다.
    /// 값은 머티리얼 에셋이 아니라 전역 셰이더 값으로 넣어 에셋을 더럽히지 않는다.
    /// </summary>
    public sealed class ScreenFxDriver : MonoBehaviour {
        [SerializeField] private GameFlow flow;
        [Tooltip("외곽선을 그린다. 저성능 환경에서 끈다")]
        [SerializeField] private bool outline = true;
        [SerializeField] private Color outlineColor = new(0.063f, 0.086f, 0.110f); // #10161C
        [SerializeField] private Color pulseColor = new(1f, 0.420f, 0.341f); // #FF6B57
        [Tooltip("받은 뒤 조여 오던 연출이 풀리는 빠르기(1/s)")]
        [SerializeField] private float releaseRate = 4f;
        [Tooltip("놓쳤을 때 붉은 기운이 번쩍인 뒤 가라앉는 빠르기(1/s)")]
        [SerializeField] private float missFlashDecay = 3f;
        [Tooltip("외곽선이 떨리는 위치를 바꾸는 횟수(1/s)")]
        [SerializeField] private float jitterRate = 12f;
        [Tooltip("0 이상이면 판 상태와 상관없이 이 남은 시간(초)의 화면을 보여준다. 에디터에서 값을 맞출 때 쓴다")]
        [SerializeField, Range(-1f, 20f)] private float previewTimeLeft = -1f;

        private static readonly int OutlineId = Shader.PropertyToID("_McFxOutline");
        private static readonly int ShadeId = Shader.PropertyToID("_McFxShade");
        private static readonly int TenseId = Shader.PropertyToID("_McFxTense");
        private static readonly int OutlineColorId = Shader.PropertyToID("_McFxOutlineColor");
        private static readonly int PulseColorId = Shader.PropertyToID("_McFxPulseColor");
        private static readonly int TimeId = Shader.PropertyToID("_McFxTime");

        // x 비네트, y 채도 빼기, z 붉은 맥박, w 색수차. 받은 뒤에는 이 값에서 서서히 0으로 푼다.
        private Vector4 tense;
        private Vector2 jitter;
        // 외곽선 두께에 더하는 비율. 마지막 lowTimeWarning초 동안 0에서 fxOutlineThicken까지 오른다.
        private float thicken;
        // 스타일 셰이더가 쓰는 진행도. tight는 fxTenseStart부터, low는 lowTimeWarning부터 0에서 1로 오른다.
        private float tight;
        private float low;
        private float missedAt = -1f;

        public bool Outline {
            get => outline;
            set => outline = value;
        }

        private void OnEnable() {
            if (flow != null) {
                flow.StateChanged += OnStateChanged;
            }
        }

        private void OnDisable() {
            if (flow != null) {
                flow.StateChanged -= OnStateChanged;
            }

            // 꺼지면 시간 연출이 화면에 남지 않게 한다.
            Shader.SetGlobalVector(TenseId, Vector4.zero);
            Shader.SetGlobalVector(ShadeId, Vector4.zero);
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            missedAt = next == RoundState.Missed ? Time.unscaledTime : -1f;
            if (next == RoundState.Playing || next == RoundState.Title) {
                tense = Vector4.zero;
                jitter = Vector2.zero;
                thicken = 0f;
            }
        }

        private void LateUpdate() {
            if (flow == null) {
                return;
            }

            GameTuning tuning = flow.Tuning;
            UpdateTense(tuning);

            Shader.SetGlobalVector(OutlineId, new Vector4(
                outline ? tuning.fxOutlineWidth * (1f + thicken) : 0f,
                tuning.fxOutlineDepthThreshold,
                tuning.fxOutlineNormalThreshold,
                Mathf.Max(0.01f, tuning.fxOutlineFadeDistance)));
            Shader.SetGlobalVector(ShadeId, new Vector4(jitter.x, jitter.y, Mathf.Max(0, tuning.fxShadeBands), 0f));
            Shader.SetGlobalVector(TenseId, tense);
            Shader.SetGlobalColor(OutlineColorId, outlineColor.linear);
            Shader.SetGlobalColor(PulseColorId, pulseColor.linear);
            Shader.SetGlobalVector(TimeId, new Vector4(Time.unscaledTime, tight, low, 0f));
        }

        private void UpdateTense(GameTuning tuning) {
            RoundContext context = flow.Context;
            tight = 0f;
            low = 0f;
            if (previewTimeLeft >= 0f) {
                Running(tuning, previewTimeLeft, tuning.duration - previewTimeLeft + Time.unscaledTime);
                return;
            }

            switch (context.State) {
                case RoundState.Playing:
                case RoundState.Answer:
                    if (flow.IsPaused) {
                        // 일시정지 중에는 기본 룩만 보인다. 재개하면 남은 시간으로 바로 돌아온다.
                        tense = Vector4.zero;
                        jitter = Vector2.zero;
                        thicken = 0f;
                    } else {
                        Running(tuning, context.TimeLeft, context.Elapsed);
                    }
                    break;
                case RoundState.Missed:
                    Missed(tuning);
                    break;
                case RoundState.Won:
                    // 받으면 조여 오던 화면이 풀린다.
                    float keep = Mathf.Exp(-releaseRate * Time.unscaledDeltaTime);
                    tense *= keep;
                    jitter = Vector2.zero;
                    thicken = 0f;
                    break;
                default:
                    tense = Vector4.zero;
                    jitter = Vector2.zero;
                    thicken = 0f;
                    break;
            }
        }

        private void Running(GameTuning tuning, float left, float elapsed) {
            tight = tuning.fxTenseStart > 0f ? Mathf.Clamp01(1f - left / tuning.fxTenseStart) : 0f;
            bool lowTime = tuning.lowTimeWarning > 0f && left <= tuning.lowTimeWarning;
            low = lowTime ? 1f - left / tuning.lowTimeWarning : 0f;

            float pulse = 0f;
            float aberration = 0f;
            jitter = Vector2.zero;
            thicken = 0f;
            if (lowTime) {
                thicken = tuning.fxOutlineThicken * low;
                // 진동과 같은 박자로 뛴다 (벨 한 주기의 절반). 박자마다 확 올라왔다 가라앉는다.
                float interval = Mathf.Max(0.05f, tuning.jiggleInterval);
                float phase = Mathf.Repeat((elapsed - tuning.jiggleDelay) / interval, 1f);
                float beat = Mathf.Exp(-phase * 6f);
                pulse = tuning.fxPulseStrength * Mathf.Lerp(0.5f, 1f, low) * beat;
                aberration = tuning.fxAberrationMax * low;
                jitter = Jitter(tuning.fxOutlineJitter * low);
            }

            tense = new Vector4(tuning.fxVignetteMax * tight, tuning.fxDesaturateMax * tight, pulse, aberration);
        }

        private void Missed(GameTuning tuning) {
            float since = missedAt >= 0f ? Time.unscaledTime - missedAt : 10f;
            float flash = Mathf.Exp(-since * missFlashDecay);
            float hold = tuning.fxPulseStrength * 0.6f;
            tight = 1f;
            tense = new Vector4(
                tuning.fxVignetteMax,
                tuning.fxDesaturateMax,
                Mathf.Lerp(hold, 0.7f, flash),
                tuning.fxAberrationMax * flash);
            jitter = Vector2.zero;
            thicken = 0f;
        }

        /// <summary>정해진 간격마다 다른 방향으로 튄다. 판 무작위와 상관없는 연출이라 시간에서 해시로 만든다.</summary>
        private Vector2 Jitter(float amount) {
            if (amount <= 0f) {
                return Vector2.zero;
            }

            float tick = Mathf.Floor(Time.unscaledTime * jitterRate);
            return new Vector2(Hash(tick) * 2f - 1f, Hash(tick + 17.3f) * 2f - 1f) * amount;
        }

        private static float Hash(float x) {
            float s = Mathf.Sin(x * 12.9898f) * 43758.547f;
            return s - Mathf.Floor(s);
        }
    }
}
