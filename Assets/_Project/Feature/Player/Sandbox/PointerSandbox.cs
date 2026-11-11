using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MissedCall.Feature.Player.Sandbox {
    /// <summary>
    /// 포인터 잠금과 TMP 영문 폰트 확인용 테스트 씬. 잠금 상태, 이동량, 클릭 처리 결과를 TMP 텍스트로 보여준다.
    /// 잠금을 얻은 클릭이 "Throw"로 세어지지 않아야 한다. 게임 코드가 아니다.
    /// </summary>
    public sealed class PointerSandbox : MonoBehaviour {
        [SerializeField] private PointerLook look;
        [SerializeField] private Transform body;
        [SerializeField] private TMP_Text status;
        [SerializeField] private float walkSpeed = 3.2f;

        private const string Charset =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz\n0123456789 !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        private readonly StringBuilder text = new(512);
        private int lockClicks;
        private int throws;
        private int locks;
        private int unlocks;
        private bool wasLocked;
        private Vector2 maxDelta;

        private static readonly float[] Scales = { 1f, 0.8f, 0.6f, 0.4f };

        private readonly float[] frameTimes = new float[120];
        private int frameIndex;
        private int scaleIndex = 1;

        // 최근 120프레임의 평균 FPS와 가장 긴 프레임(ms).
        private void MeasureFrame(out float fps, out float worstMs) {
            frameTimes[frameIndex] = Time.unscaledDeltaTime;
            frameIndex = (frameIndex + 1) % frameTimes.Length;
            float sum = 0f;
            float worst = 0f;
            foreach (float t in frameTimes) {
                sum += t;
                worst = Mathf.Max(worst, t);
            }

            fps = sum > 0f ? frameTimes.Length / sum : 0f;
            worstMs = worst * 1000f;
        }

        private static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        private void ToggleRendering(Keyboard keyboard) {
            UniversalRenderPipelineAsset pipeline = Pipeline;
            if (pipeline == null) {
                return;
            }

            // 빌드에서는 런타임 복사본이 바뀐다. 에디터에서는 에셋이 바뀌므로 확인 후 되돌린다.
            if (keyboard.digit1Key.wasPressedThisFrame) {
                scaleIndex = (scaleIndex + 1) % Scales.Length;
                pipeline.renderScale = Scales[scaleIndex];
            }

            if (keyboard.digit2Key.wasPressedThisFrame) {
                pipeline.supportsHDR = !pipeline.supportsHDR;
            }
        }

        private void Update() {
            MeasureFrame(out float fps, out float worstMs);
            if (Keyboard.current != null) {
                ToggleRendering(Keyboard.current);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) {
                if (look.CanAct) {
                    throws++;
                } else {
                    lockClicks++;
                }
            }

            if (look.IsLocked != wasLocked) {
                if (look.IsLocked) {
                    locks++;
                } else {
                    unlocks++;
                }

                wasLocked = look.IsLocked;
            }

            Vector2 d = look.LastDelta;
            maxDelta = new Vector2(Mathf.Max(maxDelta.x, Mathf.Abs(d.x)), Mathf.Max(maxDelta.y, Mathf.Abs(d.y)));
            Walk();

            text.Clear();
            text.Append("<b>Pointer lock sandbox</b>\n");
            text.Append(look.IsLocked ? "<color=#7CFC9A>LOCKED</color>" : "<color=#FFD23F>UNLOCKED - click to lock</color>");
            text.Append($"\nLocks {locks} / Unlocks {unlocks} / Lock clicks {lockClicks} / Throws {throws}");
            text.Append($"\nDelta {d.x,6:0.0} {d.y,6:0.0}  Max {maxDelta.x:0} {maxDelta.y:0}");
            text.Append($"\nYaw {look.Yaw:0.0}  Pitch {look.Pitch:0.0}");
            text.Append($"\nScreen {Screen.width}x{Screen.height}  DPI {Screen.dpi:0}");
            UniversalRenderPipelineAsset pipeline = Pipeline;
            if (pipeline != null) {
                text.Append($"\n<b>FPS {fps:0}</b>  worst {worstMs:0} ms  /  render scale {pipeline.renderScale:0.0} ({Screen.width * pipeline.renderScale:0}x{Screen.height * pipeline.renderScale:0})  HDR {(pipeline.supportsHDR ? "on" : "off")}");
            }

            text.Append("\nWASD: walk / Click: lock, then throw / Esc: unlock / 1: render scale / 2: HDR\n\n");
            text.Append(Charset);
            status.SetText(text);
        }

        private void Walk() {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) {
                return;
            }

            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float strafe = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            Vector3 move = body.rotation * new Vector3(strafe, 0f, forward);
            if (move.sqrMagnitude > 1f) {
                move.Normalize();
            }

            body.position += move * (walkSpeed * Time.deltaTime);
        }
    }
}
