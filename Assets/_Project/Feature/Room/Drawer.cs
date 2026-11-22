using MissedCall.Core.Aim;
using MissedCall.Core.Audio;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Room {
    /// <summary>
    /// 클릭하면 앞으로 빠져나오고 다시 클릭하면 들어가는 서랍. 안에 물건 더미 없이 폰만 숨을 수 있는 자리(<see cref="HideSpot"/>)가 있다.
    /// 닫혀 있으면 그 자리에 덮임을 더해 벨이 먹먹하게 들린다. 키네마틱 강체라 움직이는 동안 물리 질의에도 따라온다.
    /// 판마다 닫힌 상태로 되돌린다 (<see cref="IRoundReset"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Drawer : MonoBehaviour, IClickable, IRoundReset {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // 조준 강조. 물건과 같은 값을 기본색에 더한다.
        private static readonly Color AimTint = new(0.23f, 0.2f, 0.125f, 0f);

        [Tooltip("다 열렸을 때 닫힌 자리에서 옮겨 가는 거리와 방향 (부모 로컬)")]
        [SerializeField] private Vector3 openOffset = new(0f, 0f, 0.3f);
        [Tooltip("열리고 닫히는 데 걸리는 시간(초)")]
        [SerializeField] private float slideTime = 0.22f;
        [Tooltip("닫혀 있을 때 폰에 더하는 덮임 수")]
        [SerializeField] private int closedCovers = 3;
        [SerializeField] private HideSpot spot;
        [Tooltip("조준하면 밝혀 보이는 앞판")]
        [SerializeField] private Renderer front;

        private Rigidbody body;
        private MaterialPropertyBlock block;
        private Vector3 closedLocal;
        private bool hasClosed;
        // 0 닫힘 ~ 1 열림
        private float amount;
        private bool open;

        public AimKind Kind => AimKind.Drawer;
        public bool IsOpen => open;

        private void Awake() {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            RememberClosed();
        }

        private void RememberClosed() {
            if (!hasClosed) {
                closedLocal = transform.localPosition;
                hasClosed = true;
            }
        }

        public void ResetForRound() {
            RememberClosed();
            open = false;
            amount = 0f;
            transform.localPosition = closedLocal;
            ApplyCovers();
        }

        public void Click() {
            open = !open;
            SoundCues.PlayAt(SoundCue.Drawer, transform.position, open ? 1f : 0.7f);
        }

        public void SetAimed(bool aimed) {
            if (front == null) {
                return;
            }

            if (!aimed) {
                front.SetPropertyBlock(null);
                return;
            }

            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, front.sharedMaterial.color + AimTint);
            front.SetPropertyBlock(block);
        }

        private void FixedUpdate() {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) {
                return;
            }

            amount = Mathf.MoveTowards(amount, target, Time.fixedDeltaTime / Mathf.Max(0.01f, slideTime));
            // 끝에서 부드럽게 멈춘다.
            float eased = amount * amount * (3f - 2f * amount);
            Vector3 local = closedLocal + openOffset * eased;
            Transform parent = transform.parent;
            body.MovePosition(parent != null ? parent.TransformPoint(local) : local);
            ApplyCovers();
        }

        // 반쯤 넘게 열리면 덮지 않는다.
        private void ApplyCovers() {
            if (spot != null) {
                spot.ExtraCovers = amount < 0.5f ? closedCovers : 0;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Vector3 offset, HideSpot drawerSpot, Renderer frontRenderer) {
            openOffset = offset;
            spot = drawerSpot;
            front = frontRenderer;
        }
#endif
    }
}
