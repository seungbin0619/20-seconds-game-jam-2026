using MissedCall.Core;
using MissedCall.Core.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Layout.Sandbox {
    /// <summary>
    /// 집 조립과 방 그래프 확인용 테스트 씬. N으로 새 시드, P로 폰 자리를 바꾸고, T로 위에서 내려다본다.
    /// 화면에 듣는 사람 방, 폰 방, 소리 경로(첫 문, 벽 수, 경로 길이)를 표시한다. 게임 코드가 아니다.
    /// </summary>
    public sealed class LayoutSandbox : MonoBehaviour {
        [SerializeField] private RoomCatalog catalog;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private Transform viewer;
        [SerializeField] private Transform phone;
        [SerializeField] private Transform firstDoorMarker;
        [SerializeField] private Transform afterDoorMarker;
        [SerializeField] private int seed = 1;
        [SerializeField] private float moveSpeed = 3f;

        private const float EyeHeight = 1.52f;

        private HouseBuilder builder;
        private RoomGraph graph;
        private System.Random rng;
        private float yaw;
        private float pitch = 10f;
        private bool overview;
        private int viewerRoom;
        private int phoneRoom;
        private GraphRoute route;

        public RoomGraph Graph => graph;

        private void Start() {
            builder = new HouseBuilder(catalog, transform);
            Rebuild();
        }

        private void Rebuild() {
            rng = new System.Random(seed);
            graph = builder.Build(rng, tuning);
            if (graph == null) {
                return;
            }

            PlacePhone();
            // 폰이 없는 방의 가운데에서 시작한다.
            int start = (phoneRoom + 1 + rng.Next(graph.Rooms.Count - 1)) % graph.Rooms.Count;
            Rect footprint = graph.Footprint(start);
            viewer.position = new Vector3(footprint.center.x, EyeHeight, footprint.center.y);
            viewerRoom = start;
            SetOverview(overview);
        }

        private void PlacePhone() {
            int room = rng.Next(graph.Rooms.Count);
            HideSpot[] spots = graph.Rooms[room].HideSpots;
            phone.position = spots[rng.Next(spots.Length)].transform.position + Vector3.up * 0.05f;
            phoneRoom = room;
        }

        private void Update() {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null) {
                if (keyboard.nKey.wasPressedThisFrame) {
                    seed++;
                    Rebuild();
                }

                if (keyboard.pKey.wasPressedThisFrame && graph != null) {
                    PlacePhone();
                }

                if (keyboard.tKey.wasPressedThisFrame) {
                    SetOverview(!overview);
                }
            }

            if (graph == null) {
                return;
            }

            if (!overview) {
                if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * 0.15f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -80f, 80f);
                }

                if (keyboard != null) {
                    float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                    float strafe = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                    Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(strafe, 0f, forward);
                    viewer.position += move * (moveSpeed * Time.deltaTime);
                }

                viewer.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            int room = graph.RoomAt(viewer.position);
            if (room >= 0) {
                viewerRoom = room;
            }

            route = graph.Route(viewer.position, viewerRoom, phone.position, phoneRoom);
            firstDoorMarker.gameObject.SetActive(!route.SameRoom);
            afterDoorMarker.gameObject.SetActive(!route.SameRoom);
            if (!route.SameRoom) {
                firstDoorMarker.position = route.FirstDoor;
                afterDoorMarker.position = route.AfterDoor;
            }
        }

        private void SetOverview(bool on) {
            overview = on;
            if (graph == null) {
                return;
            }

            // 위에서 볼 때만 천장을 숨긴다.
            foreach (RoomModule module in graph.Rooms) {
                Transform ceiling = module.transform.Find("Shell/Ceiling");
                if (ceiling != null) {
                    ceiling.gameObject.SetActive(!on);
                }
            }

            if (on) {
                Rect bounds = graph.Footprint(0);
                for (int i = 1; i < graph.Rooms.Count; i++) {
                    Rect f = graph.Footprint(i);
                    bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, f.xMin), Mathf.Min(bounds.yMin, f.yMin),
                        Mathf.Max(bounds.xMax, f.xMax), Mathf.Max(bounds.yMax, f.yMax));
                }

                float height = Mathf.Max(bounds.width, bounds.height) * 1.1f + 4f;
                viewer.SetPositionAndRotation(new Vector3(bounds.center.x, height, bounds.center.y), Quaternion.Euler(90f, 0f, 0f));
            } else {
                Rect footprint = graph.Footprint(viewerRoom);
                viewer.position = new Vector3(footprint.center.x, EyeHeight, footprint.center.y);
            }
        }

        private void OnGUI() {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(10f, 10f, 420f, 200f), GUI.skin.box);
            GUILayout.Label("Layout sandbox");
            if (graph == null) {
                GUILayout.Label("Build failed. See console.");
            } else {
                GUILayout.Label($"Seed {seed} / template {builder.LastTemplate.name} / rooms {graph.Rooms.Count} / doors {graph.Doors.Count}");
                GUILayout.Label($"You: {graph.Rooms[viewerRoom].Kind} / Phone: {graph.Rooms[phoneRoom].Kind}");
                GUILayout.Label(route.SameRoom
                    ? "Route: same room"
                    : $"Route: walls {route.Walls}, door -> phone {route.PathFromDoor:0.0} m");
            }

            GUILayout.Label("N: new seed / P: move phone / T: top view\nWASD: walk / Drag: look (red = first door, yellow = next point)");
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (graph == null) {
                return;
            }

            Gizmos.color = Color.cyan;
            foreach (RoomDoor door in graph.Doors) {
                Rect a = graph.Footprint(door.RoomA);
                Rect b = graph.Footprint(door.RoomB);
                Gizmos.DrawLine(new Vector3(a.center.x, 1.2f, a.center.y), door.Position);
                Gizmos.DrawLine(door.Position, new Vector3(b.center.x, 1.2f, b.center.y));
            }
        }
#endif
    }
}
