using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Room {
    /// <summary>
    /// 방 모듈에 딸린 물리 가구 (빨래 바구니). 차거나 들이받으면 넘어져 안의 물건이 쏟아진다.
    /// 라이트맵을 받지 않고 라이트 프로브로 밝기를 받는다. 판마다 처음 자리로 되돌린다 (<see cref="IRoundReset"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LooseProp : MonoBehaviour, IRoundReset {
        private Rigidbody body;
        private Vector3 startLocal;
        private Quaternion startRotation;
        private bool hasStart;

        private void Awake() {
            body = GetComponent<Rigidbody>();
            RememberStart();
        }

        private void RememberStart() {
            if (!hasStart) {
                startLocal = transform.localPosition;
                startRotation = transform.localRotation;
                hasStart = true;
            }
        }

        public void ResetForRound() {
            RememberStart();
            body ??= GetComponent<Rigidbody>();
            transform.localPosition = startLocal;
            transform.localRotation = startRotation;
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }
    }
}
