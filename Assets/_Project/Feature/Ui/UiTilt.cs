using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 폰 패널을 원근감 있게 기울이고 움직인다. 화면 오버레이 캔버스는 원근 투영이 없으므로 자식 도형(<see cref="UiShape"/>)과
    /// 글자(TMP)의 정점을 직접 투영한다.
    /// - 켜질 때: 옆에서 비스듬히 돌아 빠르게 들어오며 나타난다.
    /// - 켜져 있는 동안: 천천히 흔들리고, 커서가 보이면 커서 쪽으로 살짝 고개를 돌린다.
    /// 일시정지 중(timeScale 0)에도 움직이도록 실제 시간을 쓴다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiTilt : MonoBehaviour {
        [Tooltip("원근 초점 거리(캔버스 px). 작을수록 원근이 세다")]
        [SerializeField] private float focal = 1100f;
        [Header("Entrance")]
        [SerializeField] private float enterTime = 0.28f;
        [SerializeField] private float enterYaw = -42f;
        [SerializeField] private float enterPitch = 10f;
        [SerializeField] private float enterSlide = -90f;
        [Header("Exit")]
        [SerializeField] private float exitTime = 0.12f;
        [SerializeField] private float exitYaw = 34f;
        [SerializeField] private float exitPitch = -8f;
        [SerializeField] private float exitSlide = 70f;
        [Header("Idle")]
        [Tooltip("기본 기울기(도). 패널이 늘 살짝 비스듬하다")]
        [SerializeField] private Vector2 restTilt = new(4f, -7f);
        [Tooltip("흔들림 폭(도)과 빠르기")]
        [SerializeField] private Vector2 swayAmount = new(1.6f, 2.2f);
        [SerializeField] private float swaySpeed = 1.6f;
        [Tooltip("커서를 따라 돌아가는 최대 각도(도)")]
        [SerializeField] private Vector2 followAmount = new(5f, 8f);
        [SerializeField] private float followResponse = 14f;

        private readonly List<UiShape> shapes = new();
        private readonly List<TMP_Text> texts = new();
        private CanvasGroup group;
        private RectTransform rect;
        private float startedAt;
        // 0보다 크면 사라지는 중. 사라지기 시작한 때
        private float leftAt = -1f;
        private Vector2 follow;

        private Quaternion rotation = Quaternion.identity;
        private float slide;

        private void Awake() {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
        }

        /// <summary>다시 들어오게 한다. UiScreen이 켤 때 부른다.</summary>
        public void Enter() {
            startedAt = Time.unscaledTime;
            leftAt = -1f;
        }

        /// <summary>돌아 나가게 한다. UiScreen이 끌 때 부른다. 다 나간 뒤 끄는 것은 UiScreen이 한다.</summary>
        public void Leave() {
            if (leftAt < 0f) {
                leftAt = Time.unscaledTime;
            }
        }

        private void OnEnable() {
            startedAt = Time.unscaledTime;
            leftAt = -1f;
            follow = Vector2.zero;
            shapes.Clear();
            texts.Clear();
            GetComponentsInChildren(true, shapes);
            GetComponentsInChildren(true, texts);
            Apply();
        }

        private void OnDisable() {
            if (group != null) {
                group.alpha = 1f;
            }
        }

        private void LateUpdate() {
            Apply();
        }

        private void Apply() {
            float t = Time.unscaledTime - startedAt;
            float enter = enterTime > 0f ? Mathf.Clamp01(t / enterTime) : 1f;
            float eased = BackOut(enter);
            float settle = 1f - eased;

            Vector2 target = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState != CursorLockMode.Locked && Screen.width > 0 && Screen.height > 0) {
                Vector2 p = mouse.position.ReadValue();
                var n = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
                target = new Vector2(-n.y * followAmount.x, n.x * followAmount.y);
            }

            follow = Vector2.Lerp(follow, target, 1f - Mathf.Exp(-followResponse * Time.unscaledDeltaTime));
            // 나갈 때는 가속하며 반대쪽으로 돈다.
            float leave = leftAt >= 0f && exitTime > 0f ? Mathf.Clamp01((Time.unscaledTime - leftAt) / exitTime) : 0f;
            leave *= leave;
            float pitch = restTilt.x + swayAmount.x * Mathf.Sin(t * swaySpeed * 1.13f + 1f) + follow.x + enterPitch * settle
                + exitPitch * leave;
            float yaw = restTilt.y + swayAmount.y * Mathf.Sin(t * swaySpeed) + follow.y + enterYaw * settle + exitYaw * leave;
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            slide = enterSlide * settle + exitSlide * leave;
            group.alpha = Mathf.Clamp01(enter * 3f) * (1f - leave);

            foreach (UiShape shape in shapes) {
                shape.Refresh();
            }

            foreach (TMP_Text text in texts) {
                if (text.isActiveAndEnabled) {
                    ProjectText(text);
                }
            }
        }

        /// <summary>
        /// from 기준 로컬 위치를 이 패널 가운데를 기준으로 돌리고 원근 투영한 위치(from 기준 로컬)로 바꾼다.
        /// </summary>
        public Vector3 Project(Transform from, Vector3 local) {
            // 에디터 모드에서는 Awake 전에 도형이 그려진다.
            if (rect == null) {
                rect = (RectTransform)transform;
            }

            Vector3 p = rect.InverseTransformPoint(from.TransformPoint(local));
            p.z = 0f;
            Vector3 v = rotation * p;
            float s = focal / Mathf.Max(1f, focal + v.z);
            var projected = new Vector3(v.x * s + slide, v.y * s, 0f);
            return from.InverseTransformPoint(rect.TransformPoint(projected));
        }

        // 글자의 모든 정점을 투영한다.
        private void ProjectText(TMP_Text text) {
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            Transform from = text.transform;
            for (int c = 0; c < info.characterCount; c++) {
                TMP_CharacterInfo character = info.characterInfo[c];
                if (!character.isVisible) {
                    continue;
                }

                int mesh = character.materialReferenceIndex;
                int start = character.vertexIndex;
                Vector3[] vertices = info.meshInfo[mesh].vertices;
                for (int v = 0; v < 4; v++) {
                    vertices[start + v] = Project(from, vertices[start + v]);
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        // 끝에서 살짝 넘쳤다 돌아온다.
        private static float BackOut(float k) {
            const float overshoot = 1.4f;
            float u = k - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
