using System;
using MissedCall.Core;
using UnityEngine;

namespace MissedCall.Feature.Player {
    /// <summary>
    /// 걸음걸이 연출. 몸통과 머리 사이의 피벗에 붙어 눈 높이를 잡고, 걸은 거리에 맞춰 머리를 흔든다.
    /// 발이 닿을 때마다 머리가 살짝 내려가고(걸음마다 한 번), 딛는 발 쪽으로 옮겨가며 기운다(두 걸음에 한 번).
    /// 흔들림 크기는 속도를 따라가므로 걷기 시작과 멈출 때 서서히 생기고 가라앉는다.
    /// </summary>
    public sealed class HeadBob : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private PlayerMotor motor;

        // 0~2π가 두 걸음. 0과 π에서 발이 닿는다.
        private float phase;
        private float amount;

        /// <summary>발이 닿을 때 호출된다. 인자는 발(0 왼발, 1 오른발). 발소리에 쓴다.</summary>
        public event Action<int> Stepped;

        private void OnEnable() {
            motor.Teleported += ResetGait;
            ResetGait();
        }

        private void OnDisable() {
            motor.Teleported -= ResetGait;
        }

        private void ResetGait() {
            phase = 0f;
            amount = 0f;
            Apply();
        }

        private void LateUpdate() {
            float dt = Time.deltaTime;
            float speed = motor.Velocity.magnitude;
            float ratio = Mathf.Clamp01(speed / tuning.walkSpeed);
            amount = Mathf.Lerp(amount, ratio, 1f - Mathf.Exp(-tuning.bobResponse * dt));

            if (speed > 0.01f && tuning.stepLength > 0f) {
                float previous = phase;
                phase += speed * dt / tuning.stepLength * Mathf.PI;
                int stepBefore = Mathf.FloorToInt(previous / Mathf.PI);
                int stepAfter = Mathf.FloorToInt(phase / Mathf.PI);
                if (stepAfter != stepBefore) {
                    Stepped?.Invoke(stepAfter & 1);
                }

                if (phase >= Mathf.PI * 2f) {
                    phase -= Mathf.PI * 2f;
                }
            }

            Apply();
        }

        private void Apply() {
            float s = Mathf.Sin(phase);
            // |sin|은 발이 닿는 순간(0, π) 꺾이며 바닥을 찍어 발을 딛는 느낌을 준다.
            float down = tuning.bobHeight * (1f - Mathf.Abs(s)) * amount;
            float side = tuning.swayWidth * s * amount;
            float roll = -tuning.swayRoll * s * amount;
            transform.localPosition = new Vector3(side, tuning.eyeHeight - down, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
