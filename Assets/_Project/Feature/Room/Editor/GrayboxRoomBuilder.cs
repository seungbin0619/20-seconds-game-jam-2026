using System.Collections.Generic;
using MissedCall.Core.Rooms;
using UnityEditor;
using UnityEngine;

namespace MissedCall.Feature.Room.Editor {
    /// <summary>
    /// 방 모듈 프리팹을 코드로 만든다. 다시 실행하면 같은 경로의 프리팹을 덮어쓴다 (GUID는 유지된다).
    /// 가구마다 충돌체(보이지 않는 상자)와 겉모습(충돌 없는 부품 여러 개)을 따로 둔다. 충돌체 좌표는 회색 박스 시절과
    /// 같아서 숨는 자리, 통로 검증이 그대로 유효하다. 겉모습은 스케치 셰이더가 부품 경계마다 선을 긋는 것을 전제로 단순하게 만든다.
    /// </summary>
    public static class GrayboxRoomBuilder {
        private const string Root = "Assets/_Project/Feature/Room/Graybox";
        private const string MaterialFolder = Root + "/Materials";

        public const float CeilingHeight = 2.6f;
        public const float WallThickness = 0.1f;
        public const float DoorWidth = 0.9f;
        public const float DoorHeight = 2.1f;
        // 푹신한 겉면을 충돌체보다 높이는 양(m). 위에 놓인 물건이 이만큼 파묻혀 보인다.
        private const float SoftSink = 0.04f;

        // 바닥과 벽
        private static Material floorWood;
        private static Material floorTile;
        private static Material ceilingMat;
        private static Material wallBath;
        private static Material wallKitchen;
        private static Material wallBed;
        private static Material wallLiving;
        private static Material wallDressing;
        private static Material trimMat;

        // 가구
        private static Material wood;
        private static Material woodDark;
        private static Material white;
        private static Material ceramic;
        private static Material metal;
        private static Material black;
        private static Material fabricBlue;
        private static Material fabricTeal;
        private static Material fabricRose;
        private static Material fabricGray;
        private static Material sheet;
        private static Material rugRed;
        private static Material rugBlue;

        // 장식
        private static Material windowGlass;
        private static Material lampGlow;
        private static Material mirror;
        private static Material[] accents;

        private static PhysicsMaterial softPhysics;
        // 책, 옷 색을 고르는 데만 쓴다. 판 무작위와 상관없고 빌드할 때마다 같은 결과가 나오게 고정 시드를 쓴다.
        private static System.Random decorRandom;

        [MenuItem("Missed Call/Room/Build Graybox Modules")]
        public static void BuildAll() {
            EnsureFolder(Root);
            EnsureFolder(MaterialFolder);
            CreateMaterials();
            decorRandom = new System.Random(20261113);

            BuildBathroom();
            BuildBathroomB();
            BuildDressingRoom();
            BuildBedroom();
            BuildBedroomB();
            BuildKitchen();
            BuildKitchenB();
            BuildLivingRoom();
            BuildLivingRoomB();
            AssetDatabase.SaveAssets();
            Debug.Log("[GrayboxRoomBuilder] Built 9 room modules in " + Root + ". Bake lightmaps again.");
        }

        // 아트 방향: 집은 채도를 낮춘 청회색 계열, 노랑은 폰과 UI 전용이라 쓰지 않는다.
        private static void CreateMaterials() {
            floorWood = Mat("Room_FloorWood", new Color(0.44f, 0.38f, 0.33f));
            floorTile = Mat("Room_FloorTile", new Color(0.66f, 0.70f, 0.72f));
            ceilingMat = Mat("Room_Ceiling", new Color(0.86f, 0.87f, 0.88f));
            wallBath = Mat("Room_WallBath", new Color(0.74f, 0.82f, 0.84f));
            wallKitchen = Mat("Room_WallKitchen", new Color(0.82f, 0.82f, 0.77f));
            wallBed = Mat("Room_WallBed", new Color(0.71f, 0.74f, 0.82f));
            wallLiving = Mat("Room_WallLiving", new Color(0.79f, 0.78f, 0.76f));
            wallDressing = Mat("Room_WallDressing", new Color(0.80f, 0.76f, 0.79f));
            trimMat = Mat("Room_Trim", new Color(0.90f, 0.90f, 0.88f));

            wood = Mat("Room_Wood", new Color(0.60f, 0.50f, 0.40f));
            woodDark = Mat("Room_WoodDark", new Color(0.33f, 0.27f, 0.23f));
            white = Mat("Room_White", new Color(0.90f, 0.91f, 0.90f));
            ceramic = Mat("Room_Ceramic", new Color(0.93f, 0.95f, 0.96f), 0.55f);
            metal = Mat("Room_Metal", new Color(0.56f, 0.59f, 0.63f), 0.5f);
            black = Mat("Room_Black", new Color(0.09f, 0.10f, 0.12f), 0.6f);
            fabricBlue = Mat("Room_FabricBlue", new Color(0.33f, 0.41f, 0.55f));
            fabricTeal = Mat("Room_FabricTeal", new Color(0.29f, 0.48f, 0.50f));
            fabricRose = Mat("Room_FabricRose", new Color(0.62f, 0.43f, 0.45f));
            fabricGray = Mat("Room_FabricGray", new Color(0.52f, 0.54f, 0.58f));
            sheet = Mat("Room_Sheet", new Color(0.86f, 0.88f, 0.92f));
            rugRed = Mat("Room_RugRed", new Color(0.52f, 0.31f, 0.32f));
            rugBlue = Mat("Room_RugBlue", new Color(0.28f, 0.34f, 0.46f));

            // 창밖은 푸른 저녁. 굽기에는 참여시키지 않아 방 밝기를 바꾸지 않는다.
            windowGlass = Mat("Room_WindowGlass", new Color(0.16f, 0.24f, 0.40f), 0.8f, new Color(0.10f, 0.17f, 0.32f));
            lampGlow = Mat("Room_LampGlow", new Color(0.92f, 0.95f, 1f), 0.2f, new Color(0.85f, 0.9f, 1f));
            mirror = Mat("Room_Mirror", new Color(0.62f, 0.70f, 0.76f), 0.9f);
            accents = new[] {
                Mat("Room_AccentA", new Color(0.36f, 0.47f, 0.62f)),
                Mat("Room_AccentB", new Color(0.58f, 0.40f, 0.42f)),
                Mat("Room_AccentC", new Color(0.40f, 0.55f, 0.50f)),
                Mat("Room_AccentD", new Color(0.78f, 0.78f, 0.80f)),
                Mat("Room_AccentE", new Color(0.30f, 0.30f, 0.38f)),
            };

            softPhysics = SoftPhysics();
        }

        // 소켓 앞 통로(문 폭 + 양쪽 0.25m, 벽에서 0.8m)에는 가구를 두지 않는다. 가구 사이 통로는 0.6m 이상 둔다
        // (플레이어 캡슐 지름 0.52m + 스킨). 좁으면 방이 둘로 갈려 문 하나로는 반대편에 갈 수 없다. 좌표는 모두 모듈 로컬.
        // 아래 충돌체 좌표(B)와 숨는 자리는 회색 박스 시절 값 그대로다. 바꾸면 level-generation.md의 검증을 다시 한다.

        // S 3 x 3. 문 소켓은 남, 서. 욕조는 동쪽 벽, 세면대와 변기는 북쪽 벽.
        private static void BuildBathroomB() {
            var b = new ModuleBuilder("Room_Bathroom_S_B", RoomKind.Bathroom, RoomSize.S, new Vector2(3f, 3f),
                floorTile, wallBath, new Color(0.88f, 0.94f, 1f));
            b.Sockets(WallSide.South, WallSide.West);

            b.Bathtub("Bathtub", new B(0.65f, 1.4f, -0.5f, 1.4f, 0f, 0.55f), WallSide.North);
            b.Toilet("Toilet", new B(-1.0f, -0.6f, 0.85f, 1.4f, 0f, 0.45f), WallSide.North);
            b.SinkCabinet("SinkCabinet", new B(-0.4f, 0.4f, 0.9f, 1.4f, 0f, 0.85f), WallSide.South);
            b.Hollow("Hamper", 0.75f, 1.35f, -1.35f, -0.75f, 0.32f, 0.04f, wood);
            b.Rug("BathMat", -0.5f, 0.3f, -0.3f, 0.4f, fabricTeal);

            b.Window(WallSide.East, 0.45f, 0.8f);
            b.Mirror(WallSide.North, 0f, 0.6f);

            b.Spot("Spot_Tub", 1.025f, 0.02f, 0.45f, ItemKind.SmallShirt, true);
            b.Spot("Spot_Hamper", 1.05f, 0.02f, -1.05f, ItemKind.SmallShirt, true);
            b.Spot("Spot_SinkTop", 0f, 0.85f, 1.15f, ItemKind.SmallShirt, false);
            b.Spot("Spot_Mat", -0.1f, 0.02f, 0.05f, ItemKind.Shirt, false);
            b.Save();
        }

        // S 3 x 3. 문 소켓은 남, 서. 북쪽 벽에 옷장과 선반, 동쪽 벽에 신발장과 옷걸이 봉.
        private static void BuildDressingRoom() {
            var b = new ModuleBuilder("Room_DressingRoom_S_A", RoomKind.DressingRoom, RoomSize.S, new Vector2(3f, 3f),
                floorWood, wallDressing, new Color(0.92f, 0.92f, 1f));
            b.Sockets(WallSide.South, WallSide.West);

            b.Cabinet("Wardrobe", new B(-1.4f, 0.6f, 0.85f, 1.4f, 0f, 2.0f), WallSide.South, white, white, 3, 1);
            b.OpenShelf("Shelf", new B(0.7f, 1.4f, 0.95f, 1.4f, 0f, 1.2f), WallSide.South, wood, 3, false);
            b.Cabinet("ShoeBench", new B(1.0f, 1.4f, -0.6f, 0.7f, 0f, 0.4f), WallSide.West, wood, wood, 3, 1);
            b.HangingRail("HangingRail", new B(1.15f, 1.25f, -0.6f, 0.7f, 1.6f, 1.65f));
            b.Ottoman("Ottoman", new B(-0.4f, 0.4f, -0.1f, 0.4f, 0f, 0.45f), fabricRose);
            b.Hollow("LaundryBasket", -1.35f, -0.75f, -1.35f, -0.75f, 0.32f, 0.04f, wood);

            b.Picture(WallSide.East, -1.1f, 0.45f, 1.3f, 1.75f);

            b.Spot("Spot_Shelf", 1.05f, 1.2f, 1.17f, ItemKind.Book, false);
            b.Spot("Spot_ShoeBench", 1.2f, 0.4f, 0.05f, ItemKind.Book, false);
            b.Spot("Spot_Ottoman", 0f, 0.45f, 0.15f, ItemKind.Shirt, false);
            b.Spot("Spot_Basket", -1.05f, 0.02f, -1.05f, ItemKind.SmallShirt, true);
            b.Spot("Spot_FloorCorner", 1.0f, 0f, -1.05f, ItemKind.Shirt, false);
            b.Save();
        }

        // M 4 x 3.5. 문 소켓은 동, 서 (지나가는 방). 침대는 북쪽 벽 가운데, 서랍장과 책상은 남쪽 벽.
        private static void BuildBedroomB() {
            var b = new ModuleBuilder("Room_Bedroom_M_B", RoomKind.Bedroom, RoomSize.M, new Vector2(4f, 3.5f),
                floorWood, wallBed, new Color(0.86f, 0.88f, 1f));
            b.Sockets(WallSide.East, WallSide.West);

            b.Bed("Bed", new B(-0.7f, 0.7f, -0.35f, 1.65f, 0f, 0.3f), new B(-0.65f, 0.65f, -0.3f, 1.6f, 0.3f, 0.45f),
                WallSide.North, fabricTeal);
            b.Cabinet("Nightstand", new B(-1.25f, -0.8f, 1.2f, 1.65f, 0f, 0.5f), WallSide.South, wood, wood, 1, 2);
            b.Cabinet("Dresser", new B(-1.6f, -0.4f, -1.65f, -1.2f, 0f, 0.9f), WallSide.North, wood, wood, 2, 3);
            b.Desk("Desk", new B(0.6f, 1.8f, -1.65f, -1.05f, 0f, 0.75f), WallSide.North);
            b.Hollow("LaundryBasket", 1.25f, 1.85f, 1.05f, 1.65f, 0.32f, 0.04f, wood);
            b.Rug("Rug", -0.9f, 0.9f, -1.0f, -0.4f, rugBlue);

            b.Window(WallSide.North, 0f, 1.0f);
            b.Window(WallSide.South, 0.1f, 0.8f);
            b.Picture(WallSide.South, -1.0f, 0.5f, 1.3f, 1.75f);

            b.Spot("Spot_BedHead", -0.3f, 0.45f, 0.9f, ItemKind.Shirt, false);
            b.Spot("Spot_BedFoot", 0.3f, 0.45f, 0.1f, ItemKind.Blanket, false);
            b.Spot("Spot_Nightstand", -1.02f, 0.5f, 1.42f, ItemKind.Book, false);
            b.Spot("Spot_Dresser", -1.0f, 0.9f, -1.42f, ItemKind.SmallShirt, false);
            b.Spot("Spot_Desk", 1.4f, 0.75f, -1.35f, ItemKind.SmallShirt, false);
            b.Spot("Spot_Basket", 1.55f, 0.02f, 1.35f, ItemKind.SmallShirt, true);
            b.Spot("Spot_Rug", 0.4f, 0.02f, -0.7f, ItemKind.Shirt, false);
            b.Save();
        }

        // M 4 x 3.5. 문 소켓은 남, 동, 서. 조리대는 북쪽 벽, 냉장고는 북서 모서리, 식탁은 가운데.
        private static void BuildKitchen() {
            var b = new ModuleBuilder("Room_Kitchen_M_A", RoomKind.Kitchen, RoomSize.M, new Vector2(4f, 3.5f),
                floorTile, wallKitchen, new Color(0.9f, 0.95f, 1f));
            b.Sockets(WallSide.South, WallSide.East, WallSide.West);

            b.Fridge("Fridge", new B(-1.9f, -1.25f, 1.0f, 1.65f, 0f, 1.8f), WallSide.South);
            b.Counter("Counter", new B(-1.2f, 1.9f, 1.05f, 1.65f, 0f, 0.9f), WallSide.South, 4);
            b.Table("Table", new B(-0.6f, 0.6f, -0.7f, 0f, 0f, 0.75f), wood, woodDark);
            b.Chair("Chair", new B(-0.2f, 0.2f, 0.1f, 0.45f, 0f, 0.45f), WallSide.North);
            b.Hollow("Bin", 1.25f, 1.85f, -1.6f, -1.0f, 0.5f, 0.04f, metal);
            b.Cabinet("UpperCabinet", new B(0.2f, 1.9f, 1.3f, 1.65f, 1.5f, 2.2f), WallSide.South, white, white, 3, 1);

            b.Window(WallSide.North, -0.55f, 0.8f);

            b.Spot("Spot_CounterLeft", -0.4f, 0.9f, 1.35f, ItemKind.SmallShirt, false);
            b.Spot("Spot_CounterRight", 1.4f, 0.9f, 1.35f, ItemKind.Book, false);
            b.Spot("Spot_Table", 0.25f, 0.75f, -0.35f, ItemKind.Shirt, false);
            b.Spot("Spot_Chair", 0f, 0.45f, 0.27f, ItemKind.Cushion, false);
            b.Spot("Spot_Bin", 1.55f, 0.02f, -1.3f, ItemKind.SmallShirt, true);
            b.Spot("Spot_FloorCorner", -1.5f, 0f, -1.3f, ItemKind.Carton, false);
            b.Save();
        }

        // M 4 x 3.5. 문 소켓은 남, 서. ㄱ자 조리대(북, 동), 냉장고는 북서 모서리, 아일랜드는 가운데.
        private static void BuildKitchenB() {
            var b = new ModuleBuilder("Room_Kitchen_M_B", RoomKind.Kitchen, RoomSize.M, new Vector2(4f, 3.5f),
                floorTile, wallKitchen, new Color(0.9f, 0.95f, 1f));
            b.Sockets(WallSide.South, WallSide.West);

            b.Fridge("Fridge", new B(-1.9f, -1.25f, 1.0f, 1.65f, 0f, 1.8f), WallSide.South);
            b.Counter("CounterNorth", new B(-1.2f, 1.9f, 1.05f, 1.65f, 0f, 0.9f), WallSide.South, 4);
            b.Counter("CounterEast", new B(1.3f, 1.9f, -1.65f, 1.0f, 0f, 0.9f), WallSide.West, 4);
            b.Counter("Island", new B(-0.6f, 0.5f, -0.3f, 0.35f, 0f, 0.9f), WallSide.South, 2);
            b.Hollow("Bin", -1.85f, -1.25f, -1.6f, -1.0f, 0.5f, 0.04f, metal);
            b.Cabinet("UpperCabinetNorth", new B(-1.2f, 0.0f, 1.3f, 1.65f, 1.5f, 2.2f), WallSide.South, white, white, 2, 1);
            b.Cabinet("UpperCabinetEast", new B(1.55f, 1.9f, -1.6f, -0.2f, 1.5f, 2.2f), WallSide.West, white, white, 2, 1);

            b.Window(WallSide.North, 0.7f, 0.8f);

            b.Spot("Spot_CounterNorth", 0.5f, 0.9f, 1.35f, ItemKind.SmallShirt, false);
            b.Spot("Spot_CounterEast", 1.6f, 0.9f, -0.6f, ItemKind.Book, false);
            b.Spot("Spot_Island", -0.05f, 0.9f, 0.02f, ItemKind.Shirt, false);
            b.Spot("Spot_Bin", -1.55f, 0.02f, -1.3f, ItemKind.SmallShirt, true);
            b.Spot("Spot_FloorCorner", 0.95f, 0f, -1.3f, ItemKind.Carton, false);
            b.Save();
        }

        // L 5 x 4. 문 소켓은 네 벽 모두. 소파는 남서 모서리, TV 장은 북서, 낮은 장은 북동.
        private static void BuildLivingRoomB() {
            var b = new ModuleBuilder("Room_LivingRoom_L_B", RoomKind.LivingRoom, RoomSize.L, new Vector2(5f, 4f),
                floorWood, wallLiving, new Color(0.9f, 0.92f, 1f));
            b.Sockets(WallSide.North, WallSide.East, WallSide.South, WallSide.West);

            b.Sofa("Sofa", new B(-2.4f, -0.9f, -1.9f, -1.2f, 0f, 0.45f), new B(-2.4f, -0.9f, -1.9f, -1.72f, 0.45f, 0.85f),
                WallSide.South, 2, fabricBlue, true);
            b.Ottoman("Chaise", new B(-2.4f, -1.7f, -1.2f, -0.8f, 0f, 0.45f), fabricBlue);
            b.Table("CoffeeTable", new B(-1.3f, -0.5f, -0.6f, 0f, 0f, 0.4f), woodDark, woodDark);
            b.TvStand("TvStand", new B(-2.2f, -1.0f, 1.45f, 1.9f, 0f, 0.5f), WallSide.South);
            b.Cabinet("Cabinet", new B(1.0f, 2.2f, 1.55f, 1.9f, 0f, 0.9f), WallSide.South, wood, wood, 3, 1);
            b.Beanbag("Beanbag", new B(1.4f, 2.1f, -1.7f, -1.0f, 0f, 0.35f), fabricRose);
            // 폭 0.4m는 안쪽이 좁아 폰(0.22m)이 비스듬히 놓이면 옆벽을 뚫었다 (11/13). 빈백(x 1.4~)과 닿지 않게 넓힌다.
            b.Hollow("ToyBox", 0.82f, 1.38f, -1.85f, -1.25f, 0.4f, 0.04f, accents[0]);
            b.Rug("Rug", -1.6f, 0.3f, -0.9f, 0.4f, rugRed);

            b.Window(WallSide.South, -1.65f, 0.9f);
            b.Picture(WallSide.North, 1.6f, 0.7f, 1.25f, 1.75f);
            b.Picture(WallSide.East, -1.3f, 0.5f, 1.3f, 1.7f);

            b.Spot("Spot_SofaLeft", -1.95f, 0.45f, -1.45f, ItemKind.Blanket, false);
            b.Spot("Spot_SofaRight", -1.2f, 0.45f, -1.45f, ItemKind.Cushion, false);
            b.Spot("Spot_CoffeeTable", -0.9f, 0.4f, -0.3f, ItemKind.Shirt, false);
            b.Spot("Spot_TvStand", -1.6f, 0.5f, 1.67f, ItemKind.SmallShirt, false);
            b.Spot("Spot_Cabinet", 1.6f, 0.9f, 1.72f, ItemKind.Book, false);
            b.Spot("Spot_Beanbag", 1.75f, 0.35f, -1.35f, ItemKind.Cushion, false);
            b.Spot("Spot_ToyBox", 1.1f, 0.02f, -1.55f, ItemKind.Book, true);
            b.Save();
        }

        // S 3 x 3. 문 소켓은 남쪽, 동쪽. 가구는 북쪽과 서쪽 벽에 붙인다.
        private static void BuildBathroom() {
            var b = new ModuleBuilder("Room_Bathroom_S_A", RoomKind.Bathroom, RoomSize.S, new Vector2(3f, 3f),
                floorTile, wallBath, new Color(0.88f, 0.94f, 1f));
            b.Sockets(WallSide.South, WallSide.East);

            b.Bathtub("Bathtub", new B(-1.4f, 0.2f, 0.65f, 1.4f, 0f, 0.55f), WallSide.West);
            b.PedestalSink("Sink", new B(-1.4f, -0.9f, -0.5f, 0.3f, 0f, 0.85f), WallSide.West);
            b.Toilet("Toilet", new B(0.6f, 1.1f, 0.85f, 1.4f, 0f, 0.45f), WallSide.North);
            b.Hollow("LaundryBasket", -1.35f, -0.75f, -1.3f, -0.7f, 0.32f, 0.04f, wood);

            b.Window(WallSide.North, -0.6f, 0.9f);
            b.Mirror(WallSide.West, -0.1f, 0.55f);
            b.TowelRail(WallSide.West, -1.0f);

            b.Spot("Spot_Tub", -0.6f, 0.02f, 1.025f, ItemKind.SmallShirt, true);
            b.Spot("Spot_Basket", -1.05f, 0.02f, -1.0f, ItemKind.SmallShirt, true);
            b.Spot("Spot_SinkTop", -1.15f, 0.85f, -0.1f, ItemKind.SmallShirt, false);
            b.Spot("Spot_FloorCorner", 1.05f, 0f, -1.05f, ItemKind.Blanket, false);
            b.Save();
        }

        // M 4 x 3.5. 문 소켓은 남, 동, 서. 침대는 북쪽 벽 가운데.
        private static void BuildBedroom() {
            var b = new ModuleBuilder("Room_Bedroom_M_A", RoomKind.Bedroom, RoomSize.M, new Vector2(4f, 3.5f),
                floorWood, wallBed, new Color(0.86f, 0.88f, 1f));
            b.Sockets(WallSide.South, WallSide.East, WallSide.West);

            b.Bed("Bed", new B(-0.8f, 0.8f, -0.4f, 1.65f, 0f, 0.3f), new B(-0.75f, 0.75f, -0.35f, 1.6f, 0.3f, 0.45f),
                WallSide.North, fabricRose);
            b.Cabinet("NightstandL", new B(-1.4f, -0.9f, 1.2f, 1.65f, 0f, 0.5f), WallSide.South, woodDark, woodDark, 1, 2);
            b.Cabinet("NightstandR", new B(0.9f, 1.4f, 1.2f, 1.65f, 0f, 0.5f), WallSide.South, woodDark, woodDark, 1, 2);
            b.Cabinet("Wardrobe", new B(-1.9f, -1.45f, 0.75f, 1.65f, 0f, 2.0f), WallSide.East, woodDark, woodDark, 2, 1);
            b.Desk("Desk", new B(0.9f, 1.9f, -1.65f, -1.1f, 0f, 0.75f), WallSide.North);
            b.Rug("Rug", -1.0f, 1.0f, -1.2f, -0.45f, rugRed);

            b.Window(WallSide.North, 0f, 1.0f);
            b.Picture(WallSide.South, 1.4f, 0.5f, 1.25f, 1.65f);

            b.Spot("Spot_BedLeft", -0.4f, 0.45f, 0.5f, ItemKind.Shirt, false);
            b.Spot("Spot_BedRight", 0.4f, 0.45f, 0.1f, ItemKind.Shirt, false);
            b.Spot("Spot_Nightstand", 1.15f, 0.5f, 1.42f, ItemKind.SmallShirt, false);
            b.Spot("Spot_Desk", 1.4f, 0.75f, -1.375f, ItemKind.Shirt, false);
            b.Spot("Spot_FloorCorner", -1.4f, 0f, -1.2f, ItemKind.Blanket, false);
            b.Save();
        }

        // L 5 x 4. 문 소켓은 네 벽 모두. 소파는 방 가운데에서 북쪽에 띄워 둔다.
        private static void BuildLivingRoom() {
            var b = new ModuleBuilder("Room_LivingRoom_L_A", RoomKind.LivingRoom, RoomSize.L, new Vector2(5f, 4f),
                floorWood, wallLiving, new Color(0.9f, 0.92f, 1f));
            b.Sockets(WallSide.North, WallSide.East, WallSide.South, WallSide.West);

            b.Sofa("Sofa", new B(-1.0f, 1.0f, 0.3f, 1.0f, 0f, 0.45f), new B(-1.0f, 1.0f, 0.82f, 1.0f, 0.45f, 0.85f),
                WallSide.North, 3, fabricGray, true);
            b.Table("CoffeeTable", new B(-0.5f, 0.5f, -0.6f, -0.05f, 0f, 0.4f), woodDark, woodDark);
            b.TvStand("TvStand", new B(-2.2f, -0.9f, -1.9f, -1.45f, 0f, 0.5f), WallSide.North);
            b.Sofa("Armchair", new B(1.7f, 2.4f, 1.2f, 1.9f, 0f, 0.42f), new B(1.7f, 2.4f, 1.72f, 1.9f, 0.42f, 0.8f),
                WallSide.North, 1, fabricTeal, false);
            b.OpenShelf("Bookshelf", new B(1.6f, 2.4f, -1.9f, -1.55f, 0f, 1.6f), WallSide.North, woodDark, 4, true);
            b.Rug("Rug", -1.3f, 1.3f, -1.0f, 0.2f, rugBlue);

            b.Window(WallSide.North, -1.6f, 0.9f);
            b.Picture(WallSide.East, 1.45f, 0.5f, 1.25f, 1.7f);
            b.Picture(WallSide.West, -1.2f, 0.6f, 1.3f, 1.75f);

            b.Spot("Spot_SofaLeft", -0.5f, 0.45f, 0.55f, ItemKind.Blanket, false);
            b.Spot("Spot_SofaRight", 0.5f, 0.45f, 0.55f, ItemKind.Cushion, false);
            b.Spot("Spot_CoffeeTable", 0f, 0.4f, -0.32f, ItemKind.Shirt, false);
            b.Spot("Spot_Rug", 0.9f, 0.02f, -0.6f, ItemKind.Blanket, false);
            b.Spot("Spot_Armchair", 2.05f, 0.42f, 1.45f, ItemKind.SmallShirt, false);
            b.Spot("Spot_TvStand", -1.55f, 0.5f, -1.67f, ItemKind.SmallShirt, false);
            b.Save();
        }

        /// <summary>모듈 로컬 상자. 인자 순서는 회색 박스 시절 Box(minX, maxX, minZ, maxZ, minY, maxY)와 같다.</summary>
        private readonly struct B {
            public readonly float X0, X1, Z0, Z1, Y0, Y1;

            public B(float x0, float x1, float z0, float z1, float y0, float y1) {
                X0 = Mathf.Min(x0, x1);
                X1 = Mathf.Max(x0, x1);
                Z0 = Mathf.Min(z0, z1);
                Z1 = Mathf.Max(z0, z1);
                Y0 = Mathf.Min(y0, y1);
                Y1 = Mathf.Max(y0, y1);
            }

            public Vector3 Center => new((X0 + X1) * 0.5f, (Y0 + Y1) * 0.5f, (Z0 + Z1) * 0.5f);
            public Vector3 Size => new(X1 - X0, Y1 - Y0, Z1 - Z0);
            public bool AlongX => X1 - X0 >= Z1 - Z0;

            public B WithY(float y0, float y1) => new(X0, X1, Z0, Z1, y0, y1);
            public B Inset(float xz) => new(X0 + xz, X1 - xz, Z0 + xz, Z1 - xz, Y0, Y1);

            /// <summary>그 면 쪽으로 depth만큼 얇게 자른 상자 (면 안쪽).</summary>
            public B Slice(WallSide side, float depth) {
                switch (side) {
                    case WallSide.North: return new B(X0, X1, Z1 - depth, Z1, Y0, Y1);
                    case WallSide.South: return new B(X0, X1, Z0, Z0 + depth, Y0, Y1);
                    case WallSide.East: return new B(X1 - depth, X1, Z0, Z1, Y0, Y1);
                    default: return new B(X0, X0 + depth, Z0, Z1, Y0, Y1);
                }
            }

            /// <summary>그 면에서 depth만큼 잘라낸 나머지.</summary>
            public B Cut(WallSide side, float depth) {
                switch (side) {
                    case WallSide.North: return new B(X0, X1, Z0, Z1 - depth, Y0, Y1);
                    case WallSide.South: return new B(X0, X1, Z0 + depth, Z1, Y0, Y1);
                    case WallSide.East: return new B(X0, X1 - depth, Z0, Z1, Y0, Y1);
                    default: return new B(X0 + depth, X1, Z0, Z1, Y0, Y1);
                }
            }

            /// <summary>긴 축(또는 지정 축)을 따라 [a0, a1] 비율 구간.</summary>
            public B Span(bool alongX, float a0, float a1) {
                if (alongX) {
                    return new B(Mathf.Lerp(X0, X1, a0), Mathf.Lerp(X0, X1, a1), Z0, Z1, Y0, Y1);
                }

                return new B(X0, X1, Mathf.Lerp(Z0, Z1, a0), Mathf.Lerp(Z0, Z1, a1), Y0, Y1);
            }
        }

        private sealed class ModuleBuilder {
            private readonly string name;
            private readonly RoomKind kind;
            private readonly RoomSize sizeClass;
            private readonly Vector2 size;
            private readonly Material floor;
            private readonly Material wall;
            private readonly GameObject root;
            private readonly Transform shell;
            private readonly Transform sockets;
            private readonly Transform furniture;
            private readonly Transform decor;
            private readonly Transform spots;
            private readonly List<DoorSocket> socketList = new();
            private readonly List<HideSpot> spotList = new();
            private readonly List<PropSpot> propList = new();
            private Light mainLight;

            public ModuleBuilder(string name, RoomKind kind, RoomSize sizeClass, Vector2 size,
                Material floor, Material wall, Color lightColor) {
                this.name = name;
                this.kind = kind;
                this.sizeClass = sizeClass;
                this.size = size;
                this.floor = floor;
                this.wall = wall;
                root = new GameObject(name);
                shell = Child(root.transform, "Shell");
                sockets = Child(root.transform, "Sockets");
                furniture = Child(root.transform, "Furniture");
                decor = Child(root.transform, "Decor");
                spots = Child(root.transform, "HideSpots");

                float w = size.x;
                float d = size.y;
                Cube(shell, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(w, 0.1f, d), floor, true, true);
                Cube(shell, "Ceiling", new Vector3(0f, CeilingHeight + 0.05f, 0f), new Vector3(w, 0.1f, d), ceilingMat, true, true);
                AddLight(lightColor);
            }

            /// <summary>나열한 벽에는 가운데에 문 소켓을 두고, 나머지 벽은 통짜로 만든다.</summary>
            public void Sockets(params WallSide[] withSocket) {
                var set = new HashSet<WallSide>(withSocket);
                foreach (WallSide side in new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West }) {
                    BuildWall(side, set.Contains(side));
                }
            }

            // ---------- 기본 부품 ----------

            /// <summary>보이지 않는 충돌체. 가구의 물리와 숨는 자리 맞추기는 이 상자만 본다.</summary>
            private GameObject Solid(Transform group, string partName, B b, PhysicsMaterial physics = null) {
                var go = new GameObject(partName);
                go.transform.SetParent(group, false);
                go.transform.localPosition = b.Center;
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.size = b.Size;
                box.sharedMaterial = physics;
                return go;
            }

            /// <summary>충돌 없는 겉모습 부품.</summary>
            private GameObject Part(Transform parent, string partName, B b, Material mat,
                PrimitiveType primitive = PrimitiveType.Cube) {
                GameObject go = Cube(parent, partName, b.Center - Offset(parent), b.Size, mat, true, false, primitive);
                return go;
            }

            /// <summary>
            /// 바닥에 피벗을 둔 푹신한 부품. SoftSurface가 이 피벗의 크기를 바꿔 누른다.
            /// 겉면은 충돌체보다 <see cref="SoftSink"/>만큼 높게 만들어, 위에 놓인 물건이 쿠션에 파묻혀 보이게 한다.
            /// </summary>
            private Transform SoftPart(Transform parent, string partName, B b, Material mat,
                PrimitiveType primitive = PrimitiveType.Cube) {
                b = b.WithY(b.Y0, b.Y1 + SoftSink);
                Transform pivot = Child(parent, partName);
                pivot.localPosition = new Vector3(b.Center.x, b.Y0, b.Center.z) - Offset(parent);
                Part(pivot, "Mesh", b, mat, primitive);
                return pivot;
            }

            // 부모(그룹, 피벗)는 회전 없이 옮기기만 하므로 위치 차이만 빼면 모듈 로컬 좌표가 부모 로컬이 된다.
            private Vector3 Offset(Transform parent) {
                Vector3 offset = Vector3.zero;
                for (Transform t = parent; t != null && t != root.transform; t = t.parent) {
                    offset += t.localPosition;
                }

                return offset;
            }

            private Transform Group(string groupName) => Child(furniture, groupName);

            /// <summary>
            /// 충돌체에 SoftSurface를 붙인다. depth는 가장 깊이 꺼지는 깊이(m), stiffness가 클수록 단단하고 빨리 돌아온다.
            /// 꺼지는 메시는 런타임에 정점을 바꾸므로 라이트맵을 받지 않고 실시간 조명과 환경광만 받는다 (벽과 같은 방식).
            /// </summary>
            private void Soft(GameObject solid, float depth, float stiffness, float damping, params Transform[] parts) {
                var filters = new List<MeshFilter>();
                foreach (Transform part in parts) {
                    foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>()) {
                        filter.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                        filters.Add(filter);
                    }
                }

                SoftSurface surface = solid.AddComponent<SoftSurface>();
                surface.EditorSetup(filters.ToArray(), depth, stiffness, damping);
            }

            /// <summary>앞면에 붙는 얇은 판 (문짝, 서랍). a0~a1은 앞면 너비 비율, y는 절대 높이.</summary>
            private void Panel(Transform group, string partName, B body, WallSide front, float a0, float a1,
                float y0, float y1, float depth, Material mat) {
                bool alongX = front == WallSide.North || front == WallSide.South;
                B span = body.Span(alongX, a0, a1).WithY(y0, y1);
                Part(group, partName, Outside(span, front, depth), mat);
            }

            /// <summary>상자의 그 면 바깥으로 depth만큼 나온 얇은 판.</summary>
            private static B Outside(B b, WallSide side, float depth) {
                switch (side) {
                    case WallSide.North: return new B(b.X0, b.X1, b.Z1, b.Z1 + depth, b.Y0, b.Y1);
                    case WallSide.South: return new B(b.X0, b.X1, b.Z0 - depth, b.Z0, b.Y0, b.Y1);
                    case WallSide.East: return new B(b.X1, b.X1 + depth, b.Z0, b.Z1, b.Y0, b.Y1);
                    default: return new B(b.X0 - depth, b.X0, b.Z0, b.Z1, b.Y0, b.Y1);
                }
            }

            // ---------- 가구 ----------

            /// <summary>문짝이나 서랍이 격자로 달린 장. cols x rows 칸마다 판과 손잡이.</summary>
            public void Cabinet(string groupName, B b, WallSide front, Material body, Material doors, int cols, int rows) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                bool floorStanding = b.Y0 < 0.01f;
                float plinth = floorStanding ? 0.06f : 0f;
                Part(g, "Body", b, body);
                if (floorStanding) {
                    // 걸레받이처럼 들어간 받침: 앞면 아래에 어두운 띠
                    Panel(g, "Plinth", b, front, 0.02f, 0.98f, b.Y0, b.Y0 + plinth, 0.004f, woodDark);
                }

                const float gap = 0.012f;
                bool alongX = front == WallSide.North || front == WallSide.South;
                float width = alongX ? b.Size.x : b.Size.z;
                // 앞면 너비 비율로 바꾼 틈
                float gapA = gap / width;
                float h0 = b.Y0 + plinth + gap;
                float h1 = b.Y1 - 0.03f;
                for (int c = 0; c < cols; c++) {
                    for (int r = 0; r < rows; r++) {
                        float a0 = (float)c / cols + gapA;
                        float a1 = (float)(c + 1) / cols - gapA;
                        float y0 = Mathf.Lerp(h0, h1, (float)r / rows) + (r > 0 ? gap : 0f);
                        float y1 = Mathf.Lerp(h0, h1, (float)(r + 1) / rows) - gap;
                        Panel(g, $"Door_{c}_{r}", b, front, a0, a1, y0, y1, 0.015f, doors);

                        // 서랍(rows > 1)은 가운데, 문짝은 안쪽 모서리 쪽 높이에 손잡이.
                        float hy = rows > 1 ? (y0 + y1) * 0.5f : Mathf.Lerp(y0, y1, b.Y1 > 1.2f ? 0.45f : 0.75f);
                        float inward = 0.05f / width;
                        float ha = rows > 1 || cols == 1 ? (a0 + a1) * 0.5f : (c % 2 == 0 ? a1 - inward : a0 + inward);
                        // 손잡이 반폭(m)을 너비 비율로
                        float hw = (rows > 1 ? 0.06f : 0.015f) / width;
                        float hh = rows > 1 ? 0.012f : 0.08f;
                        B handleBody = Outside(b, front, 0.015f);
                        Panel(g, $"Handle_{c}_{r}", handleBody, front, ha - hw, ha + hw, hy - hh, hy + hh, 0.02f, metal);
                    }
                }
            }

            /// <summary>조리대: 아래 장 + 살짝 내민 상판.</summary>
            public void Counter(string groupName, B b, WallSide front, int cols) {
                Cabinet(groupName, b.WithY(b.Y0, b.Y1 - 0.04f), front, white, white, cols, 1);
                Transform g = furniture.Find(groupName);
                // 상판은 충돌체 높이와 같게 끝난다. 앞쪽으로 2cm 내민다.
                B top = Outside(b.WithY(b.Y1 - 0.04f, b.Y1), front, 0.02f);
                Part(g, "Top", new B(Mathf.Min(top.X0, b.X0), Mathf.Max(top.X1, b.X1), Mathf.Min(top.Z0, b.Z0),
                    Mathf.Max(top.Z1, b.Z1), b.Y1 - 0.04f, b.Y1), woodDark);
                // 아래 장의 충돌체를 상판 높이까지 다시 맞춘다 (숨는 자리 높이와 같게).
                g.Find("Collider").GetComponent<BoxCollider>().size = b.Size;
                g.Find("Collider").localPosition = b.Center;
            }

            public void Table(string groupName, B b, Material top, Material legs) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                const float thickness = 0.04f;
                Part(g, "Top", b.WithY(b.Y1 - thickness, b.Y1), top);
                const float leg = 0.05f;
                const float inset = 0.04f;
                float[] xs = { b.X0 + inset, b.X1 - inset - leg };
                float[] zs = { b.Z0 + inset, b.Z1 - inset - leg };
                int i = 0;
                foreach (float x in xs) {
                    foreach (float z in zs) {
                        Part(g, "Leg" + i++, new B(x, x + leg, z, z + leg, b.Y0, b.Y1 - thickness), legs);
                    }
                }
            }

            /// <summary>책상: 탁자 + 한쪽 아래 서랍 장.</summary>
            public void Desk(string groupName, B b, WallSide front) {
                Table(groupName, b, wood, woodDark);
                Transform g = furniture.Find(groupName);
                bool alongX = front == WallSide.North || front == WallSide.South;
                B drawers = b.Span(alongX, 0.62f, 0.96f).WithY(b.Y0, b.Y1 - 0.04f).Inset(0.02f);
                Part(g, "DrawerBody", drawers, wood);
                for (int r = 0; r < 3; r++) {
                    float y0 = Mathf.Lerp(drawers.Y0 + 0.04f, drawers.Y1, r / 3f) + 0.01f;
                    float y1 = Mathf.Lerp(drawers.Y0 + 0.04f, drawers.Y1, (r + 1) / 3f) - 0.01f;
                    Panel(g, "Drawer" + r, drawers, front, 0.06f, 0.94f, y0, y1, 0.012f, wood);
                    B handleBody = Outside(drawers, front, 0.012f);
                    Panel(g, "Handle" + r, handleBody, front, 0.4f, 0.6f, (y0 + y1) * 0.5f - 0.01f, (y0 + y1) * 0.5f + 0.01f,
                        0.015f, metal);
                }
            }

            public void Chair(string groupName, B b, WallSide back) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                Part(g, "Seat", b.WithY(b.Y1 - 0.04f, b.Y1), wood);
                const float leg = 0.04f;
                float[] xs = { b.X0 + 0.02f, b.X1 - 0.02f - leg };
                float[] zs = { b.Z0 + 0.02f, b.Z1 - 0.02f - leg };
                int i = 0;
                foreach (float x in xs) {
                    foreach (float z in zs) {
                        Part(g, "Leg" + i++, new B(x, x + leg, z, z + leg, b.Y0, b.Y1 - 0.04f), woodDark);
                    }
                }

                // 등받이는 충돌 없이 겉모습만. 의자 자리 더미가 등받이에 걸리지 않게 한다.
                // 좌석 바깥에 붙인다. 좌석 위에 두면 의자 자리의 폰이 등받이 판에 묻힐 수 있었다 (11/13).
                Part(g, "Back", Outside(b, back, 0.04f).WithY(b.Y1 - 0.04f, b.Y1 + 0.42f), wood);
            }

            /// <summary>침대: 프레임, 푹신한 매트리스, 발치 쪽 이불, 헤드보드, 머리맡 베개 두 개(물리 소품).</summary>
            public void Bed(string groupName, B frame, B mattress, WallSide head, Material blanket) {
                Transform g = Group(groupName);
                Solid(g, "FrameCollider", frame);
                GameObject mattressCollider = Solid(g, "MattressCollider", mattress, softPhysics);
                Part(g, "Frame", frame, wood);

                Transform pad = SoftPart(g, "Mattress", mattress, sheet);
                // 이불: 발치 쪽 60%를 덮고 매트리스보다 조금 넓다. 매트리스와 함께 눌린다.
                WallSide foot = Opposite(head);
                B cover = mattress.Slice(foot, Depth(mattress, head) * 0.6f);
                cover = new B(cover.X0 - 0.02f, cover.X1 + 0.02f, cover.Z0 - 0.02f, cover.Z1 + 0.02f,
                    mattress.Y1 + SoftSink - 0.08f, mattress.Y1 + SoftSink + 0.012f);
                Part(pad, "Blanket", cover, blanket);
                Soft(mattressCollider, 0.035f, 220f, 16f, pad);

                B board = frame.Slice(head, 0.06f).WithY(frame.Y0, 0.95f);
                Solid(g, "HeadboardCollider", board);
                Part(g, "Headboard", board, woodDark);

                // 베개는 물리 소품. 매트리스 머리 쪽, 좌우로 나란히.
                bool alongX = head == WallSide.North || head == WallSide.South;
                B headEnd = mattress.Slice(head, 0.55f);
                for (int i = 0; i < 2; i++) {
                    B half = headEnd.Span(alongX, i * 0.5f, (i + 1) * 0.5f);
                    Prop($"Pillow{i}", new Vector3(half.Center.x, mattress.Y1, half.Center.z), Facing(head), ItemKind.Cushion);
                }
            }

            /// <summary>소파(안락의자 포함): 받침, 푹신한 좌석 쿠션, 등받이 쿠션. pillows면 양 끝에 쿠션 소품.</summary>
            public void Sofa(string groupName, B seat, B back, WallSide backSide, int seats, Material cloth, bool pillows) {
                Transform g = Group(groupName);
                GameObject seatCollider = Solid(g, "SeatCollider", seat, softPhysics);
                Solid(g, "BackCollider", back);

                const float baseHeight = 0.22f;
                Part(g, "Base", seat.WithY(seat.Y0, seat.Y0 + baseHeight), woodDark);
                B cushions = seat.Cut(backSide, Depth(back, backSide)).WithY(seat.Y0 + baseHeight, seat.Y1);
                bool alongX = backSide == WallSide.North || backSide == WallSide.South;
                var parts = new List<Transform>();
                for (int i = 0; i < seats; i++) {
                    B one = cushions.Span(alongX, (float)i / seats, (float)(i + 1) / seats).Inset(0.01f);
                    parts.Add(SoftPart(g, "SeatCushion" + i, one, cloth));
                }

                Soft(seatCollider, 0.08f, 120f, 10f, parts.ToArray());
                // 등받이: 받침과 같은 천의 뒤판 + 앞쪽 쿠션
                B backFrame = back.Slice(backSide, Depth(back, backSide) * 0.45f).WithY(seat.Y0 + baseHeight, back.Y1);
                Part(g, "BackFrame", backFrame, cloth);
                B backCushions = back.Cut(backSide, Depth(back, backSide) * 0.45f);
                for (int i = 0; i < seats; i++) {
                    B one = backCushions.Span(alongX, (float)i / seats, (float)(i + 1) / seats).Inset(0.012f);
                    Part(g, "BackCushion" + i, one, cloth);
                }

                if (pillows) {
                    for (int i = 0; i < 2; i++) {
                        float a = i == 0 ? 0.14f : 0.86f;
                        B end = cushions.Span(alongX, a - 0.01f, a + 0.01f).Slice(backSide, 0.3f);
                        Prop($"Pillow{i}", new Vector3(end.Center.x, seat.Y1, end.Center.z), Facing(backSide), ItemKind.Cushion);
                    }
                }
            }

            public void Ottoman(string groupName, B b, Material cloth) {
                Transform g = Group(groupName);
                GameObject collider = Solid(g, "Collider", b, softPhysics);
                Part(g, "Base", b.WithY(b.Y0, b.Y0 + 0.06f).Inset(0.03f), woodDark);
                Transform pad = SoftPart(g, "Cushion", b.WithY(b.Y0 + 0.06f, b.Y1), cloth);
                Soft(collider, 0.06f, 130f, 11f, pad);
            }

            public void Beanbag(string groupName, B b, Material cloth) {
                Transform g = Group(groupName);
                GameObject collider = Solid(g, "Collider", b, softPhysics);
                // 공을 눌러 놓은 모양. 아래 절반은 바닥에 묻힌다.
                B ball = new(b.X0, b.X1, b.Z0, b.Z1, b.Y0 - b.Size.y * 0.35f, b.Y1);
                Transform pad = SoftPart(g, "Bag", ball, cloth, PrimitiveType.Sphere);
                pad.localPosition = new Vector3(pad.localPosition.x, b.Y0, pad.localPosition.z);
                pad.GetChild(0).localPosition += Vector3.down * (b.Size.y * 0.35f);
                Soft(collider, 0.09f, 80f, 8f, pad);
            }

            public void Fridge(string groupName, B b, WallSide front) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                Part(g, "Body", b, white);
                float split = Mathf.Lerp(b.Y0, b.Y1, 0.68f);
                Panel(g, "DoorLow", b, front, 0.02f, 0.98f, b.Y0 + 0.04f, split - 0.01f, 0.03f, white);
                Panel(g, "DoorHigh", b, front, 0.02f, 0.98f, split + 0.01f, b.Y1 - 0.02f, 0.03f, white);
                B handleBody = Outside(b, front, 0.03f);
                Panel(g, "HandleLow", handleBody, front, 0.84f, 0.88f, split - 0.45f, split - 0.08f, 0.03f, metal);
                Panel(g, "HandleHigh", handleBody, front, 0.84f, 0.88f, split + 0.08f, split + 0.3f, 0.03f, metal);
            }

            public void Toilet(string groupName, B b, WallSide back) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                float depth = Depth(b, back);
                B tank = b.Slice(back, depth * 0.32f).WithY(b.Y0 + 0.3f, 0.8f);
                Solid(g, "TankCollider", tank);
                Part(g, "Tank", tank, ceramic);
                B bowl = b.Cut(back, depth * 0.12f).Inset(0.03f).WithY(b.Y0, b.Y1 - 0.05f);
                Part(g, "Bowl", bowl, ceramic);
                Part(g, "Seat", b.Cut(back, depth * 0.1f).Inset(0.01f).WithY(b.Y1 - 0.05f, b.Y1), white);
            }

            public void SinkCabinet(string groupName, B b, WallSide front) {
                Cabinet(groupName, b.WithY(b.Y0, b.Y1 - 0.05f), front, white, white, 2, 1);
                Transform g = furniture.Find(groupName);
                g.Find("Collider").GetComponent<BoxCollider>().size = b.Size;
                g.Find("Collider").localPosition = b.Center;
                Part(g, "Basin", b.WithY(b.Y1 - 0.05f, b.Y1), ceramic);
                Faucet(g, b, Opposite(front));
            }

            public void PedestalSink(string groupName, B b, WallSide back) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                Part(g, "Basin", b.WithY(b.Y1 - 0.16f, b.Y1), ceramic);
                B c = b.Slice(back, Depth(b, back) * 0.5f).Inset(0.08f);
                bool alongX = back == WallSide.North || back == WallSide.South;
                Part(g, "Pedestal", c.Span(!alongX, 0.25f, 0.75f).WithY(b.Y0, b.Y1 - 0.16f), ceramic);
                Faucet(g, b, back);
            }

            private void Faucet(Transform g, B b, WallSide back) {
                B spot = b.Slice(back, 0.08f);
                Vector3 c = spot.Center;
                Part(g, "Faucet", new B(c.x - 0.02f, c.x + 0.02f, c.z - 0.02f, c.z + 0.02f, b.Y1, b.Y1 + 0.14f), metal);
            }

            public void Bathtub(string groupName, B b, WallSide tapSide) {
                Hollow(groupName, b.X0, b.X1, b.Z0, b.Z1, b.Y1, 0.08f, ceramic);
                Transform g = furniture.Find(groupName);
                // 수도꼭지: 욕조 한쪽 끝 테두리 위
                B end = b.Slice(tapSide, 0.08f);
                Vector3 c = end.Center;
                Part(g, "Tap", new B(c.x - 0.03f, c.x + 0.03f, c.z - 0.03f, c.z + 0.03f, b.Y1, b.Y1 + 0.12f), metal);
            }

            public void TvStand(string groupName, B b, WallSide front) {
                Cabinet(groupName, b, front, woodDark, wood, 2, 1);
                Transform g = furniture.Find(groupName);
                // TV는 장 뒤 벽에 건다 (충돌 없음, 벽에 붙어 있어 물건이 지나갈 일이 없다).
                WallSide wallSide = Opposite(front);
                Vector3 c = b.Center;
                float along = wallSide == WallSide.North || wallSide == WallSide.South ? c.x : c.z;
                B tv = WallBox(wallSide, along, 1.1f, 0.85f, 1.5f, 0.05f);
                Part(g, "TvBezel", tv, black);
                Part(g, "TvScreen", Shrink(Outside(tv, front, 0.004f), wallSide, 0.04f), accents[4]);
            }

            /// <summary>
            /// 열린 선반: 옆판, 뒤판, 칸막이 선반. 판마다 충돌체가 있어 물건이 칸 안에 들어간다.
            /// books면 칸마다 책을 세워 꽂는다 (만질 수 있는 물건, PropSpot Standing). 아니면 개킨 옷을 장식으로 둔다.
            /// </summary>
            public void OpenShelf(string groupName, B b, WallSide front, Material mat, int shelves, bool books) {
                Transform g = Group(groupName);
                WallSide back = Opposite(front);
                const float board = 0.03f;
                B backPanel = b.Slice(back, 0.02f);
                Solid(g, "BackCollider", backPanel);
                Part(g, "Back", backPanel, mat);
                bool alongX = front == WallSide.North || front == WallSide.South;
                float width = alongX ? b.Size.x : b.Size.z;
                float sideFrac = board / width;
                B sideA = b.Span(alongX, 0f, sideFrac);
                B sideB = b.Span(alongX, 1f - sideFrac, 1f);
                Solid(g, "SideACollider", sideA);
                Solid(g, "SideBCollider", sideB);
                Part(g, "SideA", sideA, mat);
                Part(g, "SideB", sideB, mat);
                // 맨 아래 받침(바닥에서 첫 판까지)
                B plinth = b.WithY(b.Y0, b.Y0 + 0.04f);
                Solid(g, "PlinthCollider", plinth);
                Part(g, "Plinth", plinth, mat);
                for (int s = 0; s <= shelves; s++) {
                    float y = Mathf.Lerp(b.Y0 + 0.04f, b.Y1 - board, (float)s / shelves);
                    B boardBox = b.WithY(y, y + board);
                    Solid(g, "BoardCollider" + s, boardBox);
                    Part(g, "Board" + s, boardBox, mat);
                    if (s == shelves) {
                        break;
                    }

                    float nextY = Mathf.Lerp(b.Y0 + 0.04f, b.Y1 - board, (float)(s + 1) / shelves);
                    B row = b.Cut(back, 0.02f);
                    if (books) {
                        PlaceBooks(groupName, row, front, alongX, sideFrac, y + board, s);
                    } else {
                        FillShelf(g, row, alongX, sideFrac, y + board, nextY - 0.02f, s);
                    }
                }
            }

            // 칸 하나에 책을 세워 꽂는다. 칸 폭의 55~80%를 채우고, 묶음 사이에 틈을 둔다. 책 두께는 물건 종류(Book)의 두께다.
            private void PlaceBooks(string groupName, B row, WallSide front, bool alongX, float sideFrac, float y, int shelf) {
                const float bookThickness = 0.05f;
                const float gap = 0.006f;
                float width = alongX ? row.Size.x : row.Size.z;
                float step = (bookThickness + gap) / width;
                float start = sideFrac + 0.01f + Next(0f, 0.15f);
                float end = start + Next(0.55f, 0.8f);
                Quaternion facing = Facing(Opposite(front));
                int i = 0;
                for (float a = start + step * 0.5f; a < Mathf.Min(end, 1f - sideFrac - step * 0.5f); a += step) {
                    // 가끔 한 권 빼 둔다.
                    if (Next(0f, 1f) < 0.12f) {
                        continue;
                    }

                    B slot = row.Span(alongX, a, a);
                    Prop($"Book_{groupName}_{shelf}_{i++}", new Vector3(slot.Center.x, y, slot.Center.z), facing, ItemKind.Book,
                        PropSpot.Pose.Standing, 3f);
                }
            }

            private void FillShelf(Transform g, B row, bool alongX, float sideFrac, float y0, float yMax, int shelf) {
                float a = sideFrac + 0.02f;
                int i = 0;
                while (a < 1f - sideFrac - 0.05f) {
                    float w = Next(0.18f, 0.26f);
                    float width = alongX ? row.Size.x : row.Size.z;
                    float frac = w / width;
                    if (a + frac > 1f - sideFrac - 0.01f) {
                        break;
                    }

                    // 가끔 비워 둔다.
                    if (Next(0f, 1f) < 0.12f) {
                        a += frac * 2f;
                        continue;
                    }

                    float h = Next(0.05f, 0.14f);
                    B item = row.Span(alongX, a, a + frac).WithY(y0, y0 + Mathf.Min(h, yMax - y0));
                    // 앞뒤로만 조금 들여 선반 앞 모서리보다 안쪽에 둔다.
                    const float m = 0.02f;
                    item = alongX
                        ? new B(item.X0, item.X1, item.Z0 + m, item.Z1 - m, item.Y0, item.Y1)
                        : new B(item.X0 + m, item.X1 - m, item.Z0, item.Z1, item.Y0, item.Y1);
                    Part(g, $"Item_{shelf}_{i++}", item, accents[decorRandom.Next(accents.Length)]);
                    a += frac + 0.03f;
                }
            }

            /// <summary>
            /// 옷걸이 봉. 수건(셔츠 크기 천) 한 장을 반으로 접어 널어 둔다 (만질 수 있는 물건, PropSpot Hanging).
            /// 수건의 로컬 x가 봉 방향이다. 셔츠 여러 벌은 어수선했고, 두 장은 붙어서 봉을 다 덮었다 (11/13).
            /// </summary>
            public void HangingRail(string groupName, B b) {
                Transform g = Group(groupName);
                Solid(g, "Collider", b);
                Part(g, "Rail", b, metal);
                bool alongX = b.AlongX;
                Quaternion along = Quaternion.LookRotation(alongX ? Vector3.forward : Vector3.right, Vector3.up);
                float[] places = { 0.35f };
                for (int i = 0; i < places.Length; i++) {
                    Vector3 c = b.Span(alongX, places[i], places[i]).Center;
                    Prop($"Towel_{groupName}_{i}", new Vector3(c.x, b.Y1, c.z), along, ItemKind.Shirt, PropSpot.Pose.Hanging, 0f);
                }
            }

            /// <summary>바닥판과 네 테두리로 된 속이 빈 상자 (욕조, 바구니). 테두리마다 충돌체가 있다.</summary>
            public void Hollow(string boxName, float minX, float maxX, float minZ, float maxZ, float height, float rim, Material mat) {
                Transform group = Group(boxName);
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

            /// <summary>얇은 깔개. 물건이 깔개 위에 놓이도록 충돌체를 둔다. 테두리에 한 겹 더 깐다.</summary>
            public void Rug(string rugName, float minX, float maxX, float minZ, float maxZ, Material mat) {
                Transform g = Group(rugName);
                Cube(g, "Rug", new Vector3((minX + maxX) * 0.5f, 0.01f, (minZ + maxZ) * 0.5f),
                    new Vector3(maxX - minX, 0.02f, maxZ - minZ), mat, true, true);
                var inner = new B(minX + 0.08f, maxX - 0.08f, minZ + 0.08f, maxZ - 0.08f, 0.02f, 0.023f);
                Part(g, "Inner", inner, accents[3]);
            }

            // ---------- 벽 장식 (충돌 없음) ----------

            /// <summary>벽 안쪽 면에 붙은 상자. pos는 남북 벽이면 x, 동서 벽이면 z.</summary>
            private B WallBox(WallSide side, float pos, float width, float y0, float y1, float depth) {
                float inner = Inner(side);
                float w = width * 0.5f;
                switch (side) {
                    case WallSide.North: return new B(pos - w, pos + w, inner - depth, inner, y0, y1);
                    case WallSide.South: return new B(pos - w, pos + w, inner, inner + depth, y0, y1);
                    case WallSide.East: return new B(inner - depth, inner, pos - w, pos + w, y0, y1);
                    default: return new B(inner, inner + depth, pos - w, pos + w, y0, y1);
                }
            }

            private float Inner(WallSide side) {
                switch (side) {
                    case WallSide.North: return size.y * 0.5f - WallThickness;
                    case WallSide.South: return -(size.y * 0.5f - WallThickness);
                    case WallSide.East: return size.x * 0.5f - WallThickness;
                    default: return -(size.x * 0.5f - WallThickness);
                }
            }

            public void Window(WallSide side, float pos, float width) {
                Transform g = Child(decor, "Window" + side);
                const float y0 = 1.1f;
                const float y1 = 2.0f;
                Part(g, "Glass", WallBox(side, pos, width, y0, y1, 0.01f), windowGlass);
                const float frame = 0.05f;
                Part(g, "FrameTop", WallBox(side, pos, width + frame * 2f, y1, y1 + frame, 0.04f), trimMat);
                Part(g, "Sill", WallBox(side, pos, width + frame * 3f, y0 - frame, y0, 0.07f), trimMat);
                Part(g, "FrameL", WallBox(side, pos - width * 0.5f - frame * 0.5f, frame, y0, y1, 0.04f), trimMat);
                Part(g, "FrameR", WallBox(side, pos + width * 0.5f + frame * 0.5f, frame, y0, y1, 0.04f), trimMat);
                Part(g, "Mullion", WallBox(side, pos, 0.03f, y0, y1, 0.03f), trimMat);
                Part(g, "Transom", WallBox(side, pos, width, (y0 + y1) * 0.5f - 0.015f, (y0 + y1) * 0.5f + 0.015f, 0.03f), trimMat);
            }

            public void Picture(WallSide side, float pos, float width, float y0, float y1) {
                Transform g = Child(decor, "Picture" + side);
                Part(g, "Frame", WallBox(side, pos, width, y0, y1, 0.025f), woodDark);
                Part(g, "Canvas", WallBox(side, pos, width - 0.08f, y0 + 0.04f, y1 - 0.04f, 0.03f),
                    accents[decorRandom.Next(accents.Length)]);
            }

            public void Mirror(WallSide side, float pos, float width) {
                Transform g = Child(decor, "Mirror" + side);
                Part(g, "Frame", WallBox(side, pos, width + 0.04f, 1.13f, 1.77f, 0.02f), metal);
                Part(g, "Glass", WallBox(side, pos, width, 1.15f, 1.75f, 0.025f), mirror);
            }

            public void TowelRail(WallSide side, float pos) {
                Transform g = Child(decor, "TowelRail" + side);
                Part(g, "Bar", WallBox(side, pos, 0.5f, 1.2f, 1.22f, 0.06f), metal);
                Part(g, "Towel", WallBox(side, pos, 0.4f, 0.8f, 1.22f, 0.05f), fabricTeal);
            }

            // ---------- 자리 ----------

            public void Spot(string spotName, float x, float y, float z, ItemKind baseKind, bool enclosed) {
                Transform t = Child(spots, spotName);
                t.localPosition = new Vector3(x, y, z);
                HideSpot spot = t.gameObject.AddComponent<HideSpot>();
                spot.EditorSetup(baseKind, new Vector2Int(2, 4), enclosed);
                spotList.Add(spot);
            }

            private void Prop(string propName, Vector3 position, Quaternion rotation, ItemKind itemKind,
                PropSpot.Pose pose = PropSpot.Pose.Lying, float spin = 15f) {
                Transform t = Child(spots, propName);
                t.localPosition = position;
                t.localRotation = rotation;
                PropSpot prop = t.gameObject.AddComponent<PropSpot>();
                prop.EditorSetup(itemKind, spin, pose);
                propList.Add(prop);
            }

            public void Save() {
                PressSoftPockets();
                RoomModule module = root.AddComponent<RoomModule>();
                module.EditorSetup(kind, sizeClass, size, socketList.ToArray(), spotList.ToArray(), propList.ToArray(), mainLight);
                module.CloseAllSockets();

                string path = Root + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }

            // 푹신한 가구 윗면에 있는 숨는 자리를 SoftSurface에 알려, 그 자리만 처음부터 오목하게 눌러 두게 한다.
            // 겉면이 충돌체보다 SoftSink만큼 높아서 그대로 두면 폰이 쿠션 속에 묻힌다.
            private void PressSoftPockets() {
                foreach (SoftSurface surface in root.GetComponentsInChildren<SoftSurface>()) {
                    var box = surface.GetComponent<BoxCollider>();
                    // 충돌체의 모듈 로컬 중심 (부모들은 회전 없이 옮기기만 한다)
                    Vector3 center = Offset(surface.transform);
                    Vector3 half = box.size * 0.5f;
                    var pockets = new List<Vector3>();
                    foreach (HideSpot spot in spotList) {
                        Vector3 p = spot.transform.localPosition;
                        Vector3 d = p - center;
                        bool onTop = Mathf.Abs(d.y - half.y) < 0.03f;
                        if (onTop && Mathf.Abs(d.x) <= half.x + 0.05f && Mathf.Abs(d.z) <= half.z + 0.05f) {
                            pockets.Add(d);
                        }
                    }

                    surface.EditorSetPockets(pockets.ToArray(), SoftSink + 0.006f);
                }
            }

            private void AddLight(Color color) {
                var go = new GameObject("MainLight");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, CeilingHeight - 0.15f, 0f);
                mainLight = go.AddComponent<Light>();
                mainLight.type = LightType.Point;
                mainLight.lightmapBakeType = LightmapBakeType.Mixed;
                mainLight.shadows = LightShadows.None;
                mainLight.color = color;
                mainLight.range = Mathf.Max(size.x, size.y) * 1.3f;
                mainLight.intensity = 2.5f;

                // 천장 등: 납작한 원판 갓과 빛나는 아랫면
                Transform lamp = Child(decor, "CeilingLamp");
                Part(lamp, "Shade", new B(-0.28f, 0.28f, -0.28f, 0.28f, CeilingHeight - 0.06f, CeilingHeight), trimMat,
                    PrimitiveType.Cylinder);
                Part(lamp, "Glow", new B(-0.24f, 0.24f, -0.24f, 0.24f, CeilingHeight - 0.075f, CeilingHeight - 0.055f), lampGlow,
                    PrimitiveType.Cylinder);
            }

            private void BuildWall(WallSide side, bool hasSocket) {
                Vector3 normal = Normal(side);
                Vector3 right = Right(side);
                bool northSouth = side == WallSide.North || side == WallSide.South;
                // 남북 벽은 모서리까지 덮고, 동서 벽은 그 사이에 끼운다.
                float length = northSouth ? size.x : size.y - WallThickness * 2f;
                float distance = (northSouth ? size.y : size.x) * 0.5f - WallThickness * 0.5f;
                Transform wallGroup = Child(shell, "Wall" + side);

                if (!hasSocket) {
                    WallPiece(wallGroup, "Solid", normal, right, distance, 0f, length, 0f, CeilingHeight, true);
                    return;
                }

                const float offset = 0f;
                float half = DoorWidth * 0.5f;
                float leftLen = (offset - half) - (-length * 0.5f);
                float rightLen = length * 0.5f - (offset + half);
                WallPiece(wallGroup, "Left", normal, right, distance, -length * 0.5f + leftLen * 0.5f, leftLen, 0f, CeilingHeight, true);
                WallPiece(wallGroup, "Right", normal, right, distance, length * 0.5f - rightLen * 0.5f, rightLen, 0f, CeilingHeight, true);
                WallPiece(wallGroup, "Lintel", normal, right, distance, offset, DoorWidth, DoorHeight, CeilingHeight, false);

                Transform socketT = Child(sockets, "Socket" + side);
                socketT.localPosition = normal * distance + right * offset;
                socketT.localRotation = Quaternion.LookRotation(normal, Vector3.up);
                // 막힘 조각과 그 걸레받이는 한 묶음으로 켜고 끈다. 다른 벽 조각처럼 라이트맵 없이 실시간 조명만 받는다.
                Transform closedGroup = Child(socketT, "Closed");
                GameObject closed = Cube(closedGroup, "Wall", new Vector3(0f, DoorHeight * 0.5f, 0f),
                    new Vector3(DoorWidth, DoorHeight, WallThickness), wall, true, true);
                UnlightmappedWall(closed);
                // 소켓 로컬에서 방 안쪽은 -z (normal이 바깥)
                Cube(closedGroup, "Baseboard", new Vector3(0f, 0.04f, -(WallThickness * 0.5f + 0.0075f)),
                    new Vector3(DoorWidth, 0.08f, 0.015f), trimMat, true, false);

                // 문이 열렸을 때만 보이는 문틀
                Transform openGroup = Child(socketT, "Open");
                float casing = 0.07f;
                float z = -(WallThickness * 0.5f + 0.01f);
                Cube(openGroup, "CasingL", new Vector3(-(half + casing * 0.5f), DoorHeight * 0.5f, z),
                    new Vector3(casing, DoorHeight, 0.02f), trimMat, true, false);
                Cube(openGroup, "CasingR", new Vector3(half + casing * 0.5f, DoorHeight * 0.5f, z),
                    new Vector3(casing, DoorHeight, 0.02f), trimMat, true, false);
                Cube(openGroup, "CasingTop", new Vector3(0f, DoorHeight + casing * 0.5f, z),
                    new Vector3(DoorWidth + casing * 2f, casing, 0.02f), trimMat, true, false);
                openGroup.gameObject.SetActive(false);

                DoorSocket socket = socketT.gameObject.AddComponent<DoorSocket>();
                socket.EditorSetup(side, offset, DoorWidth, openGroup.gameObject, closedGroup.gameObject);
                socketList.Add(socket);
            }

            private void WallPiece(Transform parent, string pieceName, Vector3 normal, Vector3 right, float distance,
                float along, float length, float minY, float maxY, bool baseboard) {
                if (length <= 0.001f) {
                    return;
                }

                Vector3 center = normal * distance + right * along + Vector3.up * ((minY + maxY) * 0.5f);
                Vector3 extent = Abs(right) * length + Abs(normal) * WallThickness + Vector3.up * (maxY - minY);
                UnlightmappedWall(Cube(parent, pieceName, center, extent, wall, true, true));
                if (baseboard) {
                    // 걸레받이: 벽 안쪽 면에서 1.5cm 나온 8cm 띠
                    const float depth = 0.015f;
                    Vector3 bCenter = normal * (distance - WallThickness * 0.5f - depth * 0.5f) + right * along + Vector3.up * 0.04f;
                    Vector3 bExtent = Abs(right) * length + Abs(normal) * depth + Vector3.up * 0.08f;
                    Cube(parent, pieceName + "Baseboard", bCenter, bExtent, trimMat, true, false);
                }
            }

            // 벽은 상자 여러 개(문 양옆, 인방, 막힘 조각)로 되어 있어서, 상자마다 라이트맵을 따로 구우면
            // 낮은 해상도에서 경계가 어긋나 보인다. 벽은 라이트맵을 받지 않고 실시간 조명과 환경광(장면에 라이트 프로브가
            // 없으므로 모두 같은 값)만 받게 해서 조각 사이가 이어져 보이게 한다. 바닥과 가구에 주는 간접광(ContributeGI)은 유지한다.
            private static void UnlightmappedWall(GameObject piece) {
                piece.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
            }
        }

        private static float Next(float min, float max) => min + (float)decorRandom.NextDouble() * (max - min);

        private static float Depth(B b, WallSide side) =>
            side == WallSide.North || side == WallSide.South ? b.Size.z : b.Size.x;

        /// <summary>그 면 방향으로 두께를 depth만큼 줄인다 (면 쪽은 유지). 판 안쪽 내용물을 앞으로 당길 때 쓴다.</summary>
        private static B Shrink(B b, WallSide keep, float margin) {
            bool z = keep == WallSide.North || keep == WallSide.South;
            return z ? new B(b.X0 + margin, b.X1 - margin, b.Z0, b.Z1, b.Y0 + margin, b.Y1 - margin)
                : new B(b.X0, b.X1, b.Z0 + margin, b.Z1 - margin, b.Y0 + margin, b.Y1 - margin);
        }

        private static WallSide Opposite(WallSide side) {
            switch (side) {
                case WallSide.North: return WallSide.South;
                case WallSide.South: return WallSide.North;
                case WallSide.East: return WallSide.West;
                default: return WallSide.East;
            }
        }

        // 그 벽 쪽에서 방 안을 바라보는 방향 (베개가 놓이는 방향).
        private static Quaternion Facing(WallSide side) => Quaternion.LookRotation(-Normal(side), Vector3.up);

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
            Material mat, bool contributeGI, bool collider, PrimitiveType primitive = PrimitiveType.Cube) {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = cubeName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            // 원기둥 기본 메시는 높이 2라서 반으로 맞춘다.
            go.transform.localScale = primitive == PrimitiveType.Cylinder
                ? new Vector3(localSize.x, localSize.y * 0.5f, localSize.z)
                : localSize;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Collider builtIn = go.GetComponent<Collider>();
            if (!collider || primitive != PrimitiveType.Cube) {
                Object.DestroyImmediate(builtIn);
            }

            // 모듈은 런타임에 배치하고 풀로 재사용하므로 BatchingStatic은 주지 않는다.
            GameObjectUtility.SetStaticEditorFlags(go, contributeGI ? StaticEditorFlags.ContributeGI : 0);
            return go;
        }

        private static Material Mat(string matName, Color color, float smoothness = 0.15f, Color? emission = null) {
            string path = MaterialFolder + "/" + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                // 에디터에서 에셋으로 저장하는 머티리얼이므로 Shader.Find를 써도 빌드에 셰이더가 포함된다.
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue) {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                // 굽기에 참여하지 않는다. 방 밝기는 메인 조명이 정한다.
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static PhysicsMaterial SoftPhysics() {
            // 코드로 만드는 물리 머티리얼은 .asset이어야 한다. .physicsMaterial로 만들면 알 수 없는 파일(DefaultAsset)로 읽혀 적용되지 않았다.
            string path = MaterialFolder + "/PhysicsMaterial_Soft.asset";
            var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (physics == null) {
                physics = new PhysicsMaterial("Soft");
                AssetDatabase.CreateAsset(physics, path);
            }

            // 푹신한 면: 튀지 않고 잘 미끄러지지 않는다.
            physics.bounciness = 0f;
            physics.bounceCombine = PhysicsMaterialCombine.Minimum;
            physics.dynamicFriction = 0.9f;
            physics.staticFriction = 0.9f;
            physics.frictionCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(physics);
            return physics;
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
