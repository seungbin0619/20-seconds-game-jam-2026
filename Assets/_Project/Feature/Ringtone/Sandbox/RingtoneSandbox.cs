using MissedCall.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Ringtone.Sandbox {
    /// <summary>
    /// 오디오 기술 검증용 테스트 씬. 방 두 개(x &lt; 0: A, x &gt; 0: B)와 그 사이의 문 하나를 가정한다.
    /// 다른 방에 있으면 벽 1겹을 적용하고 음원을 문 위치로 옮긴다. 게임 코드가 아니다.
    /// </summary>
    public sealed class RingtoneSandbox : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private RingtonePlayer player;
        [SerializeField] private RingtonePattern phonePattern;
        [SerializeField] private RingtonePattern decoyPattern;
        [SerializeField] private Transform viewer;
        [SerializeField] private Transform phone;
        [SerializeField] private Transform decoy;
        [SerializeField] private Transform door;
        [SerializeField] private Vector2 halfSize = new(4f, 2.5f);
        [SerializeField] private float doorHalfWidth = 0.5f;

        private const float BodyRadius = 0.3f;
        private const float EyeHeight = 1.52f;

        private int phoneVoice = -1;
        private int decoyVoice = -1;
        private bool ringing;
        private bool decoyOn;
        private bool clear;
        private int covers;
        private float elapsed01;
        private float yaw;
        private float pitch = 10f;

        private bool Ready => phoneVoice >= 0 && decoyVoice >= 0;

        private void Start() {
            // 슬라이더로 튜닝을 바꿔도 원본 에셋이 변하지 않도록 복사본을 쓴다.
            tuning = Instantiate(tuning);
            player.Tuning = tuning;

            phoneVoice = player.CreateVoice(phonePattern);
            decoyVoice = player.CreateVoice(decoyPattern);
            yaw = viewer.eulerAngles.y;
            Apply();
            player.SnapVoice(phoneVoice);
            player.SnapVoice(decoyVoice);
        }

        private void Update() {
            if (!Ready) {
                return;
            }

            Look();
            Walk();
            Apply();
        }

        private static int Room(Vector3 position) => position.x < 0f ? 0 : 1;

        private void Apply() {
            player.SetVoice(phoneVoice, Route(phone.position), covers, elapsed01, clear);
            player.SetVoice(decoyVoice, Route(decoy.position), 0, 0f);
        }

        // 방이 두 개뿐이라 경로는 항상 "문 하나를 지나 바로 음원"이다. 실제 게임에서는 RoomGraph가 계산한다.
        private SoundRoute Route(Vector3 source) {
            if (Room(viewer.position) == Room(source)) {
                return SoundPath.Direct(source);
            }

            Vector3 doorPoint = door.position;
            return SoundPath.ThroughDoor(viewer.position, doorPoint, source,
                Vector3.Distance(doorPoint, source), 1, tuning.doorBlendRadius);
        }

        private void Look() {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            float dt = Time.deltaTime;
            if (keyboard != null) {
                if (keyboard.leftArrowKey.isPressed) yaw -= 100f * dt;
                if (keyboard.rightArrowKey.isPressed) yaw += 100f * dt;
                if (keyboard.upArrowKey.isPressed) pitch -= 60f * dt;
                if (keyboard.downArrowKey.isPressed) pitch += 60f * dt;
            }

            if (mouse != null && mouse.rightButton.isPressed) {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.15f;
                pitch -= delta.y * 0.15f;
            }

            pitch = Mathf.Clamp(pitch, -60f, 80f);
            viewer.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void Walk() {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) {
                return;
            }

            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float strafe = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 move = heading * new Vector3(strafe, 0f, forward);
            if (move.sqrMagnitude > 1f) {
                move.Normalize();
            }

            Vector3 position = viewer.position;
            Vector3 next = position + move * (tuning.walkSpeed * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, -halfSize.x + BodyRadius, halfSize.x - BodyRadius);
            next.z = Mathf.Clamp(next.z, -halfSize.y + BodyRadius, halfSize.y - BodyRadius);

            // 가운데 벽은 문 구간에서만 지나갈 수 있다.
            bool insideWall = Mathf.Abs(next.x) < BodyRadius;
            bool atDoor = Mathf.Abs(next.z - door.position.z) < doorHalfWidth - 0.1f;
            if (insideWall && !atDoor) {
                if (Mathf.Abs(position.x) >= BodyRadius) {
                    next.x = position.x;
                } else {
                    next.z = position.z;
                }
            }

            next.y = EyeHeight;
            viewer.position = next;
        }

        private void PlacePhone(int room) {
            float x = room == 0 ? Random.Range(-halfSize.x + 0.5f, -0.6f) : Random.Range(0.6f, halfSize.x - 0.5f);
            float z = Random.Range(-halfSize.y + 0.5f, halfSize.y - 0.5f);
            phone.position = new Vector3(x, phone.position.y, z);
            Apply();
            player.SnapVoice(phoneVoice);
        }

        private void OnGUI() {
            if (!Ready) {
                return;
            }

            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(10f, 10f, 330f, Screen.height / scale - 20f), GUI.skin.box);
            GUILayout.Label("<b>Ringtone sandbox</b>", Rich());
            GUILayout.Label("Audio: " + player.Status);
            if (!player.IsReady) {
                GUILayout.Label("<color=#FFD23F>Click anywhere to enable audio.</color>", Rich());
            }

            int viewerRoom = Room(viewer.position);
            int phoneRoom = Room(phone.position);
            GUILayout.Label($"You: room {(viewerRoom == 0 ? "A" : "B")} / Phone: room {(phoneRoom == 0 ? "A" : "B")} / walls {(viewerRoom == phoneRoom ? 0 : 1)}");
            SoundRoute route = Route(phone.position);
            GUILayout.Label($"Phone cutoff: {player.GetCutoff(phoneVoice):0} Hz / walls {route.Walls:0.00}");
            Vector3 emit = player.GetEmitPosition(phoneVoice);
            GUILayout.Label($"Phone sound from: ({emit.x:0.0}, {emit.y:0.0}, {emit.z:0.0})");

            GUILayout.Space(6f);
            if (GUILayout.Button(ringing ? "Stop ring" : "Start ring")) {
                ringing = !ringing;
                if (ringing) {
                    player.StartVoice(phoneVoice);
                } else {
                    player.StopVoice(phoneVoice);
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Phone -> A")) PlacePhone(0);
            if (GUILayout.Button("Phone -> B")) PlacePhone(1);
            GUILayout.EndHorizontal();

            GUILayout.Label($"Covers: {covers}");
            covers = Mathf.RoundToInt(GUILayout.HorizontalSlider(covers, 0f, tuning.maxCovers));
            GUILayout.Label($"Time elapsed: {elapsed01 * tuning.duration:0.0}s (louder later)");
            elapsed01 = GUILayout.HorizontalSlider(elapsed01, 0f, 1f);
            clear = GUILayout.Toggle(clear, " Answer screen (no muffle)");

            bool nextDecoy = GUILayout.Toggle(decoyOn, " Decoy alarm (room A corner)");
            if (nextDecoy != decoyOn) {
                decoyOn = nextDecoy;
                if (decoyOn) {
                    player.StartVoice(decoyVoice);
                } else {
                    player.StopVoice(decoyVoice);
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Wall tuning</b>", Rich());
            GUILayout.Label($"Wall cutoff scale: {tuning.wallCutoffScale:0.00} (one wall = {tuning.clearCutoff * tuning.wallCutoffScale:0} Hz)");
            tuning.wallCutoffScale = GUILayout.HorizontalSlider(tuning.wallCutoffScale, 0.05f, 1f);
            GUILayout.Label($"Wall volume scale: {tuning.wallGainScale:0.00}");
            tuning.wallGainScale = GUILayout.HorizontalSlider(tuning.wallGainScale, 0.1f, 1f);
            GUILayout.Label($"Min cutoff: {tuning.minCutoff:0} Hz");
            tuning.minCutoff = GUILayout.HorizontalSlider(tuning.minCutoff, 50f, 1000f);

            GUILayout.Space(6f);
            GUILayout.Label("Sound effects");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Throw")) player.Play(Sfx.Throw);
            if (GUILayout.Button("Grab")) player.Play(Sfx.Grab);
            if (GUILayout.Button("Win")) player.Play(Sfx.Win);
            if (GUILayout.Button("Miss")) player.Play(Sfx.Miss);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("WASD: walk / Arrows or right-drag: look\nThe doorway is at the far end of the middle wall.");
            GUILayout.EndArea();
        }

        private static GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true };
    }
}
