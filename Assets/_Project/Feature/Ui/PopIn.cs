using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 켜질 때 살짝 아래에서 떠오르며 커지고 투명에서 나타난다. 화면마다 폰 패널(handset)에 붙인다.
    /// 일시정지 중(timeScale 0)에도 움직이도록 실제 시간을 쓴다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PopIn : MonoBehaviour {
        [SerializeField] private float duration = 0.28f;
        [SerializeField] private float startScale = 0.92f;
        [SerializeField] private float rise = 18f;
        [Tooltip("켜진 뒤 이 시간(초) 기다렸다가 시작한다")]
        [SerializeField] private float delay;

        private CanvasGroup group;
        private RectTransform rect;
        private float startedAt;
        private bool playing;

        private void Awake() {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
        }

        private void OnEnable() {
            startedAt = Time.unscaledTime;
            playing = true;
            Apply(0f);
        }

        private void OnDisable() {
            Apply(1f);
        }

        private void LateUpdate() {
            if (!playing) {
                return;
            }

            float t = duration > 0f ? (Time.unscaledTime - startedAt - delay) / duration : 1f;
            if (t >= 1f) {
                playing = false;
            }

            Apply(Mathf.Clamp01(t));
        }

        // 끝에서 살짝 넘쳤다 돌아오는 back-out 곡선
        private void Apply(float t) {
            if (group == null) {
                return;
            }

            const float overshoot = 1.6f;
            float u = t - 1f;
            float eased = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            float scale = Mathf.LerpUnclamped(startScale, 1f, eased);
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -rise * (1f - Mathf.Clamp01(eased)));
            group.alpha = Mathf.Clamp01(t * 2.2f);
        }
    }
}
