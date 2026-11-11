using MissedCall.Core.Round;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Player {
    /// <summary>R을 누르면 즉시 새 판을 시작한다. 타이틀에서는 무시한다 (<see cref="GameFlow.RequestRestart"/>).</summary>
    public sealed class RestartKey : MonoBehaviour {
        [SerializeField] private GameFlow flow;

        private void Update() {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) {
                flow.RequestRestart();
            }
        }
    }
}
