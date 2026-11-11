using MissedCall.Core.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Room.Sandbox {
    /// <summary>
    /// 라이트맵 런타임 복원 검증용 테스트 씬. 구운 모듈 3개를 런타임에 생성해 문으로 이어 붙이고,
    /// L 키로 라이트맵을 끄고 켜서 비교한다. 게임 코드가 아니다.
    /// </summary>
    public sealed class LightmapSandbox : MonoBehaviour {
        [SerializeField] private RoomModule bathroom;
        [SerializeField] private RoomModule bedroom;
        [SerializeField] private RoomModule livingRoom;
        [SerializeField] private Transform viewer;
        [SerializeField] private float moveSpeed = 2.5f;

        private ModuleLightmap[] lightmaps;
        private bool lightmapsOn = true;
        private float yaw = 90f;
        private float pitch = 10f;

        private void Start() {
            // 욕실(동) - 침실(서, 동) - 거실(서) 순서로 문이 맞닿게 놓는다.
            RoomModule bath = Place(bathroom, new Vector3(-3.5f, 0f, 0f));
            RoomModule bed = Place(bedroom, Vector3.zero);
            RoomModule living = Place(livingRoom, new Vector3(4.5f, 0f, 0f));
            Open(bath, WallSide.East);
            Open(bed, WallSide.West);
            Open(bed, WallSide.East);
            Open(living, WallSide.West);
            lightmaps = new[] {
                bath.GetComponent<ModuleLightmap>(),
                bed.GetComponent<ModuleLightmap>(),
                living.GetComponent<ModuleLightmap>(),
            };
            viewer.SetPositionAndRotation(new Vector3(-1.2f, 1.52f, -0.9f), Quaternion.Euler(pitch, yaw, 0f));
        }

        private static RoomModule Place(RoomModule prefab, Vector3 position) {
            return Instantiate(prefab, position, Quaternion.identity);
        }

        private static void Open(RoomModule module, WallSide side) {
            foreach (DoorSocket socket in module.Sockets) {
                if (socket.Side == side) {
                    socket.SetOpen(true);
                }
            }
        }

        private void Update() {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.lKey.wasPressedThisFrame) {
                lightmapsOn = !lightmapsOn;
                foreach (ModuleLightmap map in lightmaps) {
                    if (lightmapsOn) {
                        map.Apply();
                    } else {
                        map.Clear();
                    }
                }
            }

            if (mouse != null && (mouse.rightButton.isPressed || mouse.leftButton.isPressed)) {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.15f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -80f, 80f);
            }

            if (keyboard == null) {
                return;
            }

            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float strafe = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float rise = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(strafe, 0f, forward) + Vector3.up * rise;
            viewer.SetPositionAndRotation(viewer.position + move * (moveSpeed * Time.deltaTime), rotation);
        }

        private void OnGUI() {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(10f, 10f, 360f, 120f), GUI.skin.box);
            GUILayout.Label("Lightmap sandbox");
            GUILayout.Label($"Lightmaps: {(lightmapsOn ? "ON" : "OFF")} / registered {LightmapSettings.lightmaps.Length}");
            GUILayout.Label("L: toggle lightmaps / WASD, Q/E: move / Drag: look");
            GUILayout.EndArea();
        }
    }
}
