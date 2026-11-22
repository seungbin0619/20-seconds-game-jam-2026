using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 화면 하나(타이틀, HUD, 받기, 결과, 일시정지 등)를 켜고 끌 때 전환 효과를 준다. 켤 때는 밀려 들어오며 나타나고,
    /// 끌 때는 반대로 밀려 나가며 사라진 뒤에 꺼진다. 안에 <see cref="UiTilt"/>가 있으면 패널도 돌아 들어오고 돌아 나간다.
    /// 일시정지 중(timeScale 0)에도 움직이도록 실제 시간을 쓴다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiScreen : MonoBehaviour {
        [SerializeField] private float inTime = 0.16f;
        [SerializeField] private float outTime = 0.12f;
        [Tooltip("나타날 때 이 위치(px)에서 들어오고, 사라질 때 반대쪽으로 나간다")]
        [SerializeField] private Vector2 offset = new(0f, -24f);
        [Tooltip("나타날 때 이 크기에서 시작하고, 사라질 때 이 크기로 줄어든다")]
        [SerializeField] private float scale = 1f;

        private CanvasGroup group;
        private RectTransform rect;
        private UiTilt[] tilts;
        private Vector2 basePosition;
        private float shown;
        private bool visible;
        private bool ready;

        private void Init() {
            if (ready) {
                return;
            }

            ready = true;
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
            tilts = GetComponentsInChildren<UiTilt>(true);
        }

        /// <summary>켜거나 끈다. 이미 그 상태면 아무것도 하지 않는다.</summary>
        public void SetVisible(bool value) {
            Init();
            if (value) {
                if (visible && gameObject.activeSelf) {
                    return;
                }

                bool wasHidden = !gameObject.activeSelf;
                visible = true;
                if (wasHidden) {
                    shown = 0f;
                    gameObject.SetActive(true);
                }

                foreach (UiTilt tilt in tilts) {
                    tilt.Enter();
                }

                Apply();
                return;
            }

            if (!visible && !gameObject.activeSelf) {
                return;
            }

            visible = false;
            // 한 번도 보인 적 없으면(시작할 때) 바로 끈다.
            if (shown <= 0f) {
                gameObject.SetActive(false);
                return;
            }

            foreach (UiTilt tilt in tilts) {
                tilt.Leave();
            }
        }

        private void Update() {
            float time = visible ? inTime : outTime;
            float step = time > 0f ? Time.unscaledDeltaTime / time : 1f;
            shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, step);
            Apply();
            if (!visible && shown <= 0f) {
                gameObject.SetActive(false);
            }
        }

        private void Apply() {
            // 들어올 때는 감속, 나갈 때는 가속
            float k = visible ? 1f - (1f - shown) * (1f - shown) : shown * shown;
            group.alpha = k;
            Vector2 from = visible ? offset : -offset;
            rect.anchoredPosition = basePosition + from * (1f - k);
            float s = Mathf.Lerp(scale, 1f, k);
            rect.localScale = new Vector3(s, s, 1f);
        }
    }
}
