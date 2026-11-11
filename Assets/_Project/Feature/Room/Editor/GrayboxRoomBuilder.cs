using System.Collections.Generic;
using MissedCall.Core.Rooms;
using UnityEditor;
using UnityEngine;

namespace MissedCall.Feature.Room.Editor {
    /// <summary>
    /// 기술 검증용 회색 박스 방 모듈 프리팹을 코드로 만든다. 다시 실행하면 같은 경로의 프리팹을 덮어쓴다.
    /// 실제 가구 에셋으로 바꿀 때는 이 도구로 만든 프리팹을 복제해 손으로 고친다.
    /// </summary>
    public static class GrayboxRoomBuilder {
        private const string Root = "Assets/_Project/Feature/Room/Graybox";
        private const string MaterialFolder = Root + "/Materials";

        public const float CeilingHeight = 2.6f;
        public const float WallThickness = 0.1f;
        public const float DoorWidth = 0.9f;
        public const float DoorHeight = 2.1f;

        private static Material floorMat;
        private static Material wallMat;
        private static Material woodMat;
        private static Material fabricMat;
        private static Material whiteMat;

        [MenuItem("Missed Call/Room/Build Graybox Modules")]
        public static void BuildAll() {
            EnsureFolder(Root);
            EnsureFolder(MaterialFolder);
            floorMat = LoadOrCreateMaterial("Graybox_Floor", new Color(0.55f, 0.52f, 0.48f));
            wallMat = LoadOrCreateMaterial("Graybox_Wall", new Color(0.82f, 0.81f, 0.78f));
            woodMat = LoadOrCreateMaterial("Graybox_Wood", new Color(0.50f, 0.42f, 0.34f));
            fabricMat = LoadOrCreateMaterial("Graybox_Fabric", new Color(0.42f, 0.48f, 0.56f));
            whiteMat = LoadOrCreateMaterial("Graybox_White", new Color(0.92f, 0.92f, 0.90f));

            BuildBathroom();
            BuildBedroom();
            BuildLivingRoom();
            AssetDatabase.SaveAssets();
            Debug.Log("[GrayboxRoomBuilder] Built 3 graybox room modules in " + Root);
        }

        // S 3 x 3. 문 소켓은 남쪽, 동쪽. 가구는 북쪽과 서쪽 벽에 붙인다.
        private static void BuildBathroom() {
            var b = new ModuleBuilder("Room_Bathroom_S_A", RoomKind.Bathroom, RoomSize.S, new Vector2(3f, 3f));
            b.Sockets(WallSide.South, WallSide.East);

            // 욕조: 북쪽 벽을 따라 놓은 속이 빈 상자
            b.Hollow("Bathtub", -1.4f, 0.2f, 0.65f, 1.4f, 0.55f, 0.08f, whiteMat);
            b.Box("Sink", -1.4f, -0.9f, -0.5f, 0.3f, 0f, 0.85f, whiteMat);
            b.Box("Toilet", 0.6f, 1.1f, 0.85f, 1.4f, 0f, 0.45f, whiteMat);
            b.Hollow("LaundryBasket", -1.35f, -0.75f, -1.3f, -0.7f, 0.32f, 0.04f, woodMat);

            b.Spot("Spot_Tub", -0.6f, 0.02f, 1.025f, ItemKind.Blanket, true);
            b.Spot("Spot_Basket", -1.05f, 0.02f, -1.0f, ItemKind.SmallShirt, true);
            b.Spot("Spot_SinkTop", -1.15f, 0.85f, -0.1f, ItemKind.SmallShirt, false);
            b.Spot("Spot_FloorCorner", 1.05f, 0f, -1.05f, ItemKind.Blanket, false);
            b.Save();
        }

        // M 4 x 3.5. 문 소켓은 남, 동, 서. 침대는 북쪽 벽 가운데.
        private static void BuildBedroom() {
            var b = new ModuleBuilder("Room_Bedroom_M_A", RoomKind.Bedroom, RoomSize.M, new Vector2(4f, 3.5f));
            b.Sockets(WallSide.South, WallSide.East, WallSide.West);

            b.Box("BedFrame", -0.8f, 0.8f, -0.4f, 1.65f, 0f, 0.3f, woodMat);
            b.Box("Mattress", -0.75f, 0.75f, -0.35f, 1.6f, 0.3f, 0.45f, fabricMat);
            b.Box("NightstandL", -1.4f, -0.9f, 1.2f, 1.65f, 0f, 0.5f, woodMat);
            b.Box("NightstandR", 0.9f, 1.4f, 1.2f, 1.65f, 0f, 0.5f, woodMat);
            b.Box("Wardrobe", -1.9f, -1.45f, 0.75f, 1.65f, 0f, 2.0f, woodMat);
            b.Box("Desk", 0.8f, 1.9f, -1.65f, -0.95f, 0f, 0.75f, woodMat);
            b.Rug("Rug", -1.0f, 1.0f, -1.2f, -0.45f, fabricMat);

            b.Spot("Spot_BedLeft", -0.4f, 0.45f, 0.5f, ItemKind.Shirt, false);
            b.Spot("Spot_BedRight", 0.4f, 0.45f, 0.1f, ItemKind.Shirt, false);
            b.Spot("Spot_Nightstand", 1.15f, 0.5f, 1.42f, ItemKind.SmallShirt, false);
            b.Spot("Spot_Desk", 1.35f, 0.75f, -1.3f, ItemKind.Shirt, false);
            b.Spot("Spot_FloorCorner", -1.4f, 0f, -1.2f, ItemKind.Blanket, false);
            b.Save();
        }

        // L 5 x 4. 문 소켓은 네 벽 모두. 소파는 방 가운데에서 북쪽에 띄워 둔다.
        private static void BuildLivingRoom() {
            var b = new ModuleBuilder("Room_LivingRoom_L_A", RoomKind.LivingRoom, RoomSize.L, new Vector2(5f, 4f));
            b.Sockets(WallSide.North, WallSide.East, WallSide.South, WallSide.West);

            b.Box("SofaSeat", -1.0f, 1.0f, 0.3f, 1.0f, 0f, 0.45f, fabricMat);
            b.Box("SofaBack", -1.0f, 1.0f, 0.82f, 1.0f, 0.45f, 0.85f, fabricMat);
            b.Box("CoffeeTable", -0.5f, 0.5f, -0.6f, -0.05f, 0f, 0.4f, woodMat);
            b.Box("TvStand", -2.2f, -0.9f, -1.9f, -1.45f, 0f, 0.5f, woodMat);
            b.Box("ArmchairSeat", 1.7f, 2.4f, 1.2f, 1.9f, 0f, 0.42f, fabricMat);
            b.Box("ArmchairBack", 1.7f, 2.4f, 1.72f, 1.9f, 0.42f, 0.8f, fabricMat);
            b.Box("Bookshelf", 1.6f, 2.4f, -1.9f, -1.55f, 0f, 1.6f, woodMat);
            b.Rug("Rug", -1.3f, 1.3f, -1.0f, 0.2f, fabricMat);

            b.Spot("Spot_SofaLeft", -0.5f, 0.45f, 0.55f, ItemKind.Blanket, false);
            b.Spot("Spot_SofaRight", 0.5f, 0.45f, 0.55f, ItemKind.Cushion, false);
            b.Spot("Spot_CoffeeTable", 0f, 0.4f, -0.32f, ItemKind.Shirt, false);
            b.Spot("Spot_Rug", 0.9f, 0.02f, -0.6f, ItemKind.Blanket, false);
            b.Spot("Spot_Armchair", 2.05f, 0.42f, 1.45f, ItemKind.SmallShirt, false);
            b.Spot("Spot_TvStand", -1.55f, 0.5f, -1.67f, ItemKind.SmallShirt, false);
            b.Save();
        }

        private sealed class ModuleBuilder {
            private readonly string name;
            private readonly RoomKind kind;
            private readonly RoomSize sizeClass;
            private readonly Vector2 size;
            private readonly GameObject root;
            private readonly Transform shell;
            private readonly Transform sockets;
            private readonly Transform furniture;
            private readonly Transform spots;
            private readonly List<DoorSocket> socketList = new();
            private readonly List<HideSpot> spotList = new();
            private Light mainLight;

            public ModuleBuilder(string name, RoomKind kind, RoomSize sizeClass, Vector2 size) {
                this.name = name;
                this.kind = kind;
                this.sizeClass = sizeClass;
                this.size = size;
                root = new GameObject(name);
                shell = Child(root.transform, "Shell");
                sockets = Child(root.transform, "Sockets");
                furniture = Child(root.transform, "Furniture");
                spots = Child(root.transform, "HideSpots");

                float w = size.x;
                float d = size.y;
                Cube(shell, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(w, 0.1f, d), floorMat, true, true);
                Cube(shell, "Ceiling", new Vector3(0f, CeilingHeight + 0.05f, 0f), new Vector3(w, 0.1f, d), wallMat, true, true);
                AddLight();
            }

            /// <summary>나열한 벽에는 가운데에 문 소켓을 두고, 나머지 벽은 통짜로 만든다.</summary>
            public void Sockets(params WallSide[] withSocket) {
                var set = new HashSet<WallSide>(withSocket);
                foreach (WallSide side in new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West }) {
                    BuildWall(side, set.Contains(side));
                }
            }

            public void Box(string boxName, float minX, float maxX, float minZ, float maxZ, float minY, float maxY, Material mat) {
                Cube(furniture, boxName, new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                    new Vector3(maxX - minX, maxY - minY, maxZ - minZ), mat, true, true);
            }

            /// <summary>바닥판과 네 테두리로 된 속이 빈 상자 (욕조, 바구니).</summary>
            public void Hollow(string boxName, float minX, float maxX, float minZ, float maxZ, float height, float rim, Material mat) {
                Transform group = Child(furniture, boxName);
                float cx = (minX + maxX) * 0.5f;
                float cz = (minZ + maxZ) * 0.5f;
                float sx = maxX - minX;
                float sz = maxZ - minZ;
                const float baseThickness = 0.02f;
                Cube(group, "Base", new Vector3(cx, baseThickness * 0.5f, cz), new Vector3(sx, baseThickness, sz), mat, true, true);
                float hy = height * 0.5f;
                Cube(group, "RimW", new Vector3(minX + rim * 0.5f, hy, cz), new Vector3(rim, height, sz), mat, true, true);
                Cube(group, "RimE", new Vector3(maxX - rim * 0.5f, hy, cz), new Vector3(rim, height, sz), mat, true, true);
                Cube(group, "RimS", new Vector3(cx, hy, minZ + rim * 0.5f), new Vector3(sx - rim * 2f, height, rim), mat, true, true);
                Cube(group, "RimN", new Vector3(cx, hy, maxZ - rim * 0.5f), new Vector3(sx - rim * 2f, height, rim), mat, true, true);
            }

            /// <summary>얇은 깔개. 물건이 깔개 위에 놓이도록 충돌체를 둔다.</summary>
            public void Rug(string rugName, float minX, float maxX, float minZ, float maxZ, Material mat) {
                Cube(furniture, rugName, new Vector3((minX + maxX) * 0.5f, 0.01f, (minZ + maxZ) * 0.5f),
                    new Vector3(maxX - minX, 0.02f, maxZ - minZ), mat, true, true);
            }

            public void Spot(string spotName, float x, float y, float z, ItemKind baseKind, bool enclosed) {
                Transform t = Child(spots, spotName);
                t.localPosition = new Vector3(x, y, z);
                HideSpot spot = t.gameObject.AddComponent<HideSpot>();
                spot.EditorSetup(baseKind, new Vector2Int(2, 4), enclosed);
                spotList.Add(spot);
            }

            public void Save() {
                RoomModule module = root.AddComponent<RoomModule>();
                module.EditorSetup(kind, sizeClass, size, socketList.ToArray(), spotList.ToArray(), mainLight);
                module.CloseAllSockets();

                string path = Root + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }

            private void AddLight() {
                var go = new GameObject("MainLight");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, CeilingHeight - 0.15f, 0f);
                mainLight = go.AddComponent<Light>();
                mainLight.type = LightType.Point;
                mainLight.lightmapBakeType = LightmapBakeType.Mixed;
                mainLight.shadows = LightShadows.None;
                mainLight.color = new Color(1f, 0.9f, 0.78f);
                mainLight.range = Mathf.Max(size.x, size.y) * 1.3f;
                mainLight.intensity = 2.5f;
            }

            private void BuildWall(WallSide side, bool hasSocket) {
                Vector3 normal = Normal(side);
                Vector3 right = Right(side);
                bool northSouth = side == WallSide.North || side == WallSide.South;
                // 남북 벽은 모서리까지 덮고, 동서 벽은 그 사이에 끼운다.
                float length = northSouth ? size.x : size.y - WallThickness * 2f;
                float distance = (northSouth ? size.y : size.x) * 0.5f - WallThickness * 0.5f;
                Transform wall = Child(shell, "Wall" + side);

                if (!hasSocket) {
                    WallPiece(wall, "Solid", normal, right, distance, 0f, length, 0f, CeilingHeight, true);
                    return;
                }

                const float offset = 0f;
                float half = DoorWidth * 0.5f;
                float leftLen = (offset - half) - (-length * 0.5f);
                float rightLen = length * 0.5f - (offset + half);
                WallPiece(wall, "Left", normal, right, distance, -length * 0.5f + leftLen * 0.5f, leftLen, 0f, CeilingHeight, true);
                WallPiece(wall, "Right", normal, right, distance, length * 0.5f - rightLen * 0.5f, rightLen, 0f, CeilingHeight, true);
                WallPiece(wall, "Lintel", normal, right, distance, offset, DoorWidth, DoorHeight, CeilingHeight, true);

                Transform socketT = Child(sockets, "Socket" + side);
                socketT.localPosition = normal * distance + right * offset;
                socketT.localRotation = Quaternion.LookRotation(normal, Vector3.up);
                // 막힘 조각은 소켓 아래에 둔다. 굽기에서 제외하므로 GI 정적 플래그를 주지 않는다.
                GameObject closed = Cube(socketT, "Closed", new Vector3(0f, DoorHeight * 0.5f, 0f),
                    new Vector3(DoorWidth, DoorHeight, WallThickness), wallMat, false, true);
                DoorSocket socket = socketT.gameObject.AddComponent<DoorSocket>();
                socket.EditorSetup(side, offset, DoorWidth, null, closed);
                socketList.Add(socket);
            }

            private void WallPiece(Transform parent, string pieceName, Vector3 normal, Vector3 right, float distance,
                float along, float length, float minY, float maxY, bool gi) {
                if (length <= 0.001f) {
                    return;
                }

                Vector3 center = normal * distance + right * along + Vector3.up * ((minY + maxY) * 0.5f);
                Vector3 extent = Abs(right) * length + Abs(normal) * WallThickness + Vector3.up * (maxY - minY);
                Cube(parent, pieceName, center, extent, wallMat, gi, true);
            }
        }

        private static Vector3 Normal(WallSide side) {
            switch (side) {
                case WallSide.North: return Vector3.forward;
                case WallSide.East: return Vector3.right;
                case WallSide.South: return Vector3.back;
                default: return Vector3.left;
            }
        }

        // 방 안에서 그 벽을 바라볼 때 오른쪽 방향.
        private static Vector3 Right(WallSide side) {
            switch (side) {
                case WallSide.North: return Vector3.right;
                case WallSide.East: return Vector3.back;
                case WallSide.South: return Vector3.left;
                default: return Vector3.forward;
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static Transform Child(Transform parent, string childName) {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Cube(Transform parent, string cubeName, Vector3 localCenter, Vector3 localSize,
            Material mat, bool contributeGI, bool collider) {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = cubeName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = localSize;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) {
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            }

            // 모듈은 런타임에 배치하고 풀로 재사용하므로 BatchingStatic은 주지 않는다.
            GameObjectUtility.SetStaticEditorFlags(go, contributeGI ? StaticEditorFlags.ContributeGI : 0);
            return go;
        }

        private static Material LoadOrCreateMaterial(string matName, Color color) {
            string path = MaterialFolder + "/" + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                // 에디터에서 에셋으로 저장하는 머티리얼이므로 Shader.Find를 써도 빌드에 셰이더가 포함된다.
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }

            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
