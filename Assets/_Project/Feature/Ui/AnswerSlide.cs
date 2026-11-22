using MissedCall.Core.Audio;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 받기 화면. 폰을 집으면 포인터 잠금이 풀리고 커서가 보인다 (PointerLook).
    /// 손잡이를 클릭한 채 트랙 오른쪽 끝까지 끌면 받는다. 중간에 놓으면 처음 자리로 돌아간다.
    /// </summary>
    public sealed class AnswerSlide : MonoBehaviour {
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform thumb;
        [SerializeField] private TMP_Text caller;
        [Tooltip("손잡이와 트랙 가장자리 사이 간격(px)")]
        [SerializeField] private float inset = 4f;
        [Tooltip("놓았을 때 처음 자리로 돌아가는 시간(초, 끝에서 처음까지)")]
        [SerializeField] private float returnTime = 0.2f;
        [Tooltip("커서가 손잡이 위에 있거나 끄는 동안 손잡이 크기 배율")]
        [SerializeField] private float hoverScale = 1.08f;
        [Tooltip("손대지 않은 동안 손잡이가 오른쪽으로 살짝 밀렸다 돌아오며 끌라고 알려주는 폭(트랙 비율)과 간격(초)")]
        [SerializeField] private float nudge = 0.06f;
        [SerializeField] private float nudgeInterval = 0.8f;

        private GameFlow flow;
        private float progress;
        private bool dragging;
        private float grabOffset;
        private float idleTime;

        public void Bind(GameFlow gameFlow) {
            flow = gameFlow;
        }

        /// <summary>폰을 집었을 때 부른다. 폰을 집은 클릭이 아직 눌려 있어도 새로 눌러야 끌 수 있다.</summary>
        public void Begin() {
            progress = 0f;
            dragging = false;
            idleTime = 0f;
            caller.text = flow.Context.Caller != null ? flow.Context.Caller.displayName : "Unknown Number";
            Place(false);
        }

        private void Update() {
            if (flow == null || flow.State != RoundState.Answer) {
                return;
            }

            if (flow.IsPaused) {
                dragging = false;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            bool overThumb = RectTransformUtility.RectangleContainsScreenPoint(thumb, screen, null);
            float pointerX = TrackX(screen);
            if (!dragging && overThumb && mouse.leftButton.wasPressedThisFrame) {
                dragging = true;
                grabOffset = pointerX - ThumbX;
                SoundCues.Play(SoundCue.SlideGrab);
            }

            if (dragging && !mouse.leftButton.isPressed) {
                dragging = false;
                if (progress > 0.12f) {
                    SoundCues.Play(SoundCue.SlideBack);
                }
            }

            if (dragging) {
                float room = Room;
                progress = room > 0f ? Mathf.Clamp01((pointerX - grabOffset - inset) / room) : 1f;
                if (progress >= 1f) {
                    dragging = false;
                    Place(false);
                    flow.Answer();
                    return;
                }
            } else if (progress > 0f) {
                // 처음엔 빠르게, 끝에서 부드럽게 멈춘다. returnTime은 끝에서 처음까지 거의 다 돌아오는 시간이다.
                float k = returnTime > 0f ? 4f / returnTime : 1000f;
                progress = Mathf.Max(0f, progress * Mathf.Exp(-k * Time.unscaledDeltaTime) - 0.05f * Time.unscaledDeltaTime);
            }

            idleTime = dragging || overThumb || progress > 0f ? 0f : idleTime + Time.unscaledDeltaTime;
            Place(dragging || overThumb);
        }

        // 손대지 않으면 주기마다 오른쪽으로 톡 밀렸다 돌아온다. 처음 0.4초는 가만히 둔다.
        private float Nudge() {
            if (idleTime < 0.4f || nudgeInterval <= 0f) {
                return 0f;
            }

            float t = Mathf.Repeat(idleTime - 0.4f, nudgeInterval) / 0.35f;
            return t < 1f ? nudge * Mathf.Sin(t * Mathf.PI) : 0f;
        }

        // 손잡이 왼쪽 끝이 움직일 수 있는 거리(px, 트랙 로컬)
        private float Room => Mathf.Max(0f, track.rect.width - thumb.rect.width - inset * 2f);

        private float ThumbX => thumb.anchoredPosition.x;

        // 화면 좌표를 트랙 왼쪽 끝 기준 가로 위치로 바꾼다.
        private float TrackX(Vector2 screen) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, screen, null, out Vector2 local);
            return local.x - track.rect.xMin;
        }

        private void Place(bool highlighted) {
            thumb.anchoredPosition = new Vector2(inset + Room * (progress + Nudge()), 0f);
            float scale = highlighted ? hoverScale : 1f;
            thumb.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
