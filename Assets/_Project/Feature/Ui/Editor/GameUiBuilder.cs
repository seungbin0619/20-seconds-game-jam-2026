using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MissedCall.Feature.Ui.Editor {
    /// <summary>
    /// 게임 UI 프리팹(타이틀, HUD, 조준점, 받기 슬라이드, 결과)과 UI 스프라이트를 코드로 만든다.
    /// 다시 실행하면 같은 경로의 에셋을 덮어쓴다. 모양은 프로토타입의 HTML 오버레이(handset 패널)를 따른다.
    /// 폰트를 바꿀 때는 <see cref="DisplayFontPath"/>, <see cref="BodyFontPath"/>를 고치고 다시 실행한다.
    /// </summary>
    public static class GameUiBuilder {
        private const string Root = "Assets/_Project/Feature/Ui";
        private const string SpriteFolder = Root + "/Sprites";
        private const string PrefabPath = Root + "/GameUi.prefab";
        private const string DisplayShadowPath = Root + "/UiFont_Display_Shadow.mat";
        private const string BodyShadowPath = Root + "/UiFont_Body_Shadow.mat";
        private const string BoldShadowPath = Root + "/UiFont_Bold_Shadow.mat";
        private const string ShaderPath = Root + "/UiHalftone.shader";
        private const string ShapeMaterialPath = Root + "/UiHalftone.mat";

        // Pretendard. 제목은 ExtraBold, 본문은 Medium이며 본문의 굵은 글씨는 Bold 에셋을 쓴다 (폰트 굵기 표).
        private const string DisplayFontPath = "Assets/_Project/Shared/Fonts/UiFont_Pretendard-ExtraBold SDF.asset";
        private const string BodyFontPath = "Assets/_Project/Shared/Fonts/UiFont_Pretendard-Medium SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Shared/Fonts/UiFont_Pretendard-Bold SDF.asset";

        private const float HandsetWidth = 380f;
        // 패널 모서리 깎기, 버튼 기울임(px)
        private const float CardChamfer = 22f;
        private const float ButtonSkew = 16f;

        private static TMP_FontAsset displayFont;
        private static TMP_FontAsset bodyFont;
        private static TMP_FontAsset boldFont;
        private static Material displayShadow;
        private static Material bodyShadow;
        private static Material boldShadow;
        private static Sprite ring;
        private static Material shapeMaterial;
        private static Texture2D cursorTexture;

        // 브라우저 CSS 커서는 32px를 넘으면 무시될 수 있다.
        private const int CursorSize = 32;

        [MenuItem("Missed Call/Ui/Build Game Ui")]
        public static void Build() {
            EnsureFolder(SpriteFolder);
            ring = MakeSprite("Ui_Ring", 32, 16f, 4.5f, 0);
            cursorTexture = MakeCursor("Ui_Cursor");
            shapeMaterial = ShapeMaterial();
            if (shapeMaterial == null) {
                Debug.LogError("[GameUiBuilder] UiHalftone shader not found");
                return;
            }

            displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (displayFont == null || bodyFont == null || boldFont == null) {
                Debug.LogError("[GameUiBuilder] Font asset not found");
                return;
            }

            displayShadow = ShadowMaterial(displayFont, DisplayShadowPath);
            bodyShadow = ShadowMaterial(bodyFont, BodyShadowPath);
            boldShadow = ShadowMaterial(boldFont, BoldShadowPath);

            var root = new GameObject("GameUi", typeof(RectTransform));
            try {
                BuildRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            } finally {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GameUiBuilder] Built " + PrefabPath);
        }

        private static void BuildRoot(GameObject root) {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            // UiShape가 도형 값을 넣는 채널
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
            var ui = root.AddComponent<GameUi>();

            Transform parent = root.transform;
            HudView hud = BuildHud(parent);
            AimView aim = BuildAim(parent);
            GameObject title = BuildTitle(parent, out TMP_Text titleClock, out Image startButton);
            AnswerSlide answer = BuildAnswer(parent);
            ResultView result = BuildResult(parent, out TMP_Text resultClock, out Image retryButton);
            GameObject pause = BuildPause(parent, out TMP_Text pauseTime, out Image resume, out Image restart, out Image titleButton);

            var so = new SerializedObject(ui);
            so.FindProperty("pauseScreen").objectReferenceValue = pause;
            so.FindProperty("resumeHint").objectReferenceValue = BuildResumeHint(parent);
            so.FindProperty("pauseTime").objectReferenceValue = pauseTime;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("titleButton").objectReferenceValue = titleButton;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("retryButton").objectReferenceValue = retryButton;
            so.FindProperty("cursor").objectReferenceValue = cursorTexture;
            so.FindProperty("cursorHotspot").vector2Value = new Vector2(CursorSize * 0.5f, CursorSize * 0.5f);
            so.FindProperty("titleScreen").objectReferenceValue = title;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("aim").objectReferenceValue = aim;
            so.FindProperty("answer").objectReferenceValue = answer;
            so.FindProperty("result").objectReferenceValue = result;
            SerializedProperty clocks = so.FindProperty("lockClocks");
            clocks.arraySize = 2;
            clocks.GetArrayElementAtIndex(0).objectReferenceValue = titleClock;
            clocks.GetArrayElementAtIndex(1).objectReferenceValue = resultClock;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 화면 ----------

        private static HudView BuildHud(Transform parent) {
            RectTransform rect = Stretch(Node("Hud", parent));
            Transition(rect, new Vector2(0f, 40f), 1f);
            var hud = rect.gameObject.AddComponent<HudView>();
            Image plate = Shape(rect, "ClockPlate", UiPalette.Night, 10f, 20f, 0.8f, 0f, 0.6f, 0.6f);
            Place(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(210f, 82f));
            // 초가 넘어갈 때 옆으로 밀려나는 숫자 잔상. 타이머 뒤에 둔다.
            TMP_Text ghost = Text(rect, "ClockGhost", "20.0", 72f, UiPalette.Ring, displayFont, null, FontStyles.Italic);
            Place(ghost.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 80f));
            ghost.alpha = 0f;
            TMP_Text clock = Text(rect, "Clock", "20.0", 72f, UiPalette.Ring, displayFont, displayShadow, FontStyles.Italic);
            Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 80f));
            TMP_Text hint = Text(rect, "Hint", "Follow the sound and clear the piles", 20f, UiPalette.Paper, bodyFont, bodyShadow);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(900f, 34f));
            // 판 변수 태그: 힌트 아래 기울인 강조색 띠
            Image tag = Shape(rect, "ModifierTag", UiPalette.Ring, 0f, ButtonSkew, 0.7f, 0f, -0.25f);
            tag.gameObject.AddComponent<CanvasGroup>();
            Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(340f, 36f));
            TMP_Text tagText = Text(tag.rectTransform, "Label", "Blackout", 16f, UiPalette.Night, bodyFont, null,
                FontStyles.Bold | FontStyles.Italic | FontStyles.UpperCase);
            tagText.characterSpacing = 6f;
            Stretch(tagText.rectTransform);
            tag.gameObject.SetActive(false);
            Wire(hud, ("clock", clock), ("hint", hint), ("modifierTag", tag.rectTransform), ("modifierText", tagText),
                ("clockGhost", ghost), ("clockPlate", plate));
            return hud;
        }

        private static AimView BuildAim(Transform parent) {
            RectTransform rect = Stretch(Node("Aim", parent));
            Transition(rect, Vector2.zero, 1.6f);
            var aim = rect.gameObject.AddComponent<AimView>();
            Image ringImage = Img(rect, "Ring", ring, UiPalette.Paper, Image.Type.Simple);
            Place(ringImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
            TMP_Text label = Text(rect, "Label", "Grab", 16f, UiPalette.Paper, bodyFont, bodyShadow, FontStyles.Bold);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(300f, 28f));
            Wire(aim, ("ring", ringImage.rectTransform), ("ringImage", ringImage), ("label", label));
            return aim;
        }

        private static GameObject BuildTitle(Transform parent, out TMP_Text clock, out Image button) {
            RectTransform rect = Stretch(Node("Title", parent));
            Transition(rect, new Vector2(0f, -20f), 1f);
            Backdrop(rect, UiPalette.Night, 1f, 0.55f);
            // 카드 뒤에 부재중 빨강 하프톤이 터지듯 퍼진다.
            Image burst = Shape(rect, "Burst", UiPalette.Miss, 0f, 0f, 0.62f, UiShape.Burst, UiShape.TintedDots + 0.5f, 0f);
            Place(burst.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(40f, 10f), new Vector2(980f, 980f), new Vector2(0.5f, 0.5f));
            RectTransform handset = Handset(rect);
            Image stripe = handset.Find("Stripe").GetComponent<Image>();
            stripe.color = UiPalette.Miss;
            // 부재중 알림 배지
            Image badge = Shape(handset, "Badge", UiPalette.Miss, 8f, 0f, 0.6f, 0f, -0.25f);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(10f, 14f), new Vector2(40f, 40f), new Vector2(0.5f, 0.5f));
            badge.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);
            TMP_Text count = Text(badge.rectTransform, "Count", "1", 22f, UiPalette.Ring, displayFont, null, FontStyles.Italic);
            Stretch(count.rectTransform);
            clock = Tag(handset, "LockTime", "00:00");
            // 제목: 빨간 글자를 깔고 흰 글자를 살짝 어긋나게 얹어 만화 인쇄처럼 보이게 한다.
            TMP_Text title = Heading(handset, "Missed Call", 56f, UiPalette.Miss);
            TMP_Text titleTop = Text(title.rectTransform, "Top", "Missed Call", 56f, UiPalette.Ring, displayFont, null,
                FontStyles.Italic | FontStyles.UpperCase);
            Stretch(titleTop.rectTransform);
            titleTop.rectTransform.offsetMin = new Vector2(-4f, 4f);
            titleTop.rectTransform.offsetMax = new Vector2(-4f, 4f);
            Text(handset, "Lead", "Your phone is ringing somewhere. Find it and pick up before it stops.", 18f, UiPalette.Text2, bodyFont);
            // 키 이름은 흰색 100%, 설명은 3단계 투명도. 줄마다 투명도를 다시 정한다.
            string key = "<alpha=#FF><b>{0}</b>  <alpha=#73>{1}";
            TMP_Text keys = Text(handset, "Keys",
                string.Format(key, "WASD", "Walk") + "\n"
                + string.Format(key, "Mouse", "Look around") + "\n"
                + string.Format(key, "Hold click", "Grab, swing and release to throw"),
                15f, UiPalette.Ring, bodyFont, null, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            keys.lineSpacing = 8f;
            button = Pill(handset, "Button", "Ring It");
            Text(handset, "Note", "Headphones make the direction much easier to hear.", 14f, UiPalette.Text4, bodyFont);
            return rect.gameObject;
        }

        private static AnswerSlide BuildAnswer(Transform parent) {
            RectTransform rect = Stretch(Node("Answer", parent));
            Transition(rect, new Vector2(0f, -30f), 1f);
            var slide = rect.gameObject.AddComponent<AnswerSlide>();
            RectTransform handset = Handset(rect);
            Tag(handset, "LockTime", "Incoming call");
            TMP_Text caller = Text(handset, "Caller", "Mom", 20f, UiPalette.Text2, bodyFont, null, FontStyles.Bold);
            TMP_Text answerHeading = Heading(handset, "Pick up!", 42f, UiPalette.Answer);

            Image track = Shape(handset, "Track", Color.white, 0f, ButtonSkew, 0.7f, 0f, -0.35f, 0.06f);
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
            TMP_Text hint = Text(track.rectTransform, "Label", "Slide to answer  >>", 14f, UiPalette.Text3, bodyFont, null,
                FontStyles.Italic | FontStyles.UpperCase);
            hint.characterSpacing = 6f;
            Stretch(hint.rectTransform);
            Image thumb = Shape(track.rectTransform, "Thumb", UiPalette.Answer, 8f, ButtonSkew * 0.8f, 0.8f, 0f, -0.3f);
            Place(thumb.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(66f, 54f), new Vector2(0f, 0.5f));

            Wire(slide, ("track", track.rectTransform), ("thumb", thumb.rectTransform), ("caller", caller), ("heading", answerHeading),
                ("trackLabel", hint));
            return slide;
        }

        private static ResultView BuildResult(Transform parent, out TMP_Text clock, out Image button) {
            RectTransform rect = Stretch(Node("Result", parent));
            Transition(rect, new Vector2(0f, -24f), 1f);
            var view = rect.gameObject.AddComponent<ResultView>();
            Backdrop(rect, new Color(8f / 255f, 11f / 255f, 14f / 255f), 0.34f, -0.55f);
            RectTransform handset = Handset(rect);
            clock = Tag(handset, "LockTime", "00:00");
            TMP_Text head = Heading(handset, "Hello?", 42f, UiPalette.Answer);
            TMP_Text caller = Text(handset, "Caller", "Mom", 20f, UiPalette.Text2, bodyFont, null, FontStyles.Bold);
            TMP_Text quote = Text(handset, "Quote", "\"Have you eaten yet?\"", 17f, UiPalette.Text3, bodyFont);
            TMP_Text stat = Text(handset, "Stat", "Answered in 0.0s", 15f, UiPalette.Text3, bodyFont);
            button = Pill(handset, "Button", "Try Again");
            Text(handset, "Note", "Press R to restart", 14f, UiPalette.Text4, bodyFont);
            Wire(view, ("head", head), ("caller", caller), ("quote", quote), ("stat", stat));
            return view;
        }

        private static GameObject BuildPause(Transform parent, out TMP_Text time, out Image resume, out Image restart, out Image title) {
            RectTransform rect = Stretch(Node("Pause", parent));
            Transition(rect, new Vector2(0f, -20f), 1f);
            Backdrop(rect, new Color(8f / 255f, 11f / 255f, 14f / 255f), 0.6f, -0.55f);
            RectTransform handset = Handset(rect);
            Tag(handset, "LockTime", "On hold");
            Heading(handset, "Paused", 42f, UiPalette.Ring);
            time = Text(handset, "Time", "20.0s left", 17f, UiPalette.Text2, bodyFont);
            resume = Pill(handset, "Resume", "Resume");
            restart = Pill(handset, "Restart", "Restart", false);
            title = Pill(handset, "Title", "Title", false);
            Text(handset, "Note", "Press R to restart", 14f, UiPalette.Text4, bodyFont);
            return rect.gameObject;
        }

        // 일시정지 중 Esc를 누르면 메뉴 대신 보이는 안내. 화면 아무 곳이나 클릭하면 재개한다.
        private static GameObject BuildResumeHint(Transform parent) {
            RectTransform rect = Stretch(Node("ResumeHint", parent));
            Transition(rect, Vector2.zero, 1.12f);
            Backdrop(rect, new Color(8f / 255f, 11f / 255f, 14f / 255f), 0.45f, -0.55f);
            TMP_Text head = Text(rect, "Heading", "Click to resume", 40f, UiPalette.Ring, displayFont, displayShadow,
                FontStyles.Italic | FontStyles.UpperCase);
            Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(900f, 60f), new Vector2(0.5f, 0f));
            TMP_Text note = Text(rect, "Note", "Esc for menu", 17f, UiPalette.Paper, bodyFont, bodyShadow);
            Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(900f, 30f));
            return rect.gameObject;
        }

        // ---------- 부품 ----------

        // 폰 패널. 모서리 두 곳을 깎은 어두운 카드에 하프톤 점, 점으로 된 그림자, 강조색 띠를 얹는다.
        // UiTilt가 원근감 있게 기울이고 등장 애니메이션을 한다. 바탕 도형들은 레이아웃에서 빼고 카드 크기를 따라간다.
        private static RectTransform Handset(Transform parent) {
            RectTransform rect = Node("Handset", parent);
            Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HandsetWidth, 0f), new Vector2(0.5f, 0.5f));
            rect.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            rect.gameObject.AddComponent<UiTilt>();

            Image shadow = Shape(rect, "Shadow", Color.black, CardChamfer, 0f, 1f, UiShape.Uniform, -0.75f, 0f);
            Backing(shadow.rectTransform, new Vector2(14f, -14f));
            Image fill = Shape(rect, "Fill", UiPalette.Night, CardChamfer, 0f, 0.85f, -40f, 0.6f, 0.94f);
            Backing(fill.rectTransform, Vector2.zero);
            Image edge = Shape(rect, "Edge", UiPalette.Frame, CardChamfer, 0f, 0f, 0f, 0f);
            edge.GetComponent<UiShape>().outline = 2f;
            Backing(edge.rectTransform, Vector2.zero);
            Image stripe = Shape(rect, "Stripe", UiPalette.Ring, 0f, 8f, 0f, 0f, 0f);
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(stripe.rectTransform, new Vector2(0f, 1f), new Vector2(CardChamfer + 10f, 0f), new Vector2(84f, 6f), new Vector2(0f, 0.5f));

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 26);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        // 화면을 켜고 끌 때의 전환 효과. offset만큼 밀려 들어오고 반대쪽으로 나가며, scale에서 커지거나 줄어든다.
        private static void Transition(RectTransform rect, Vector2 offset, float scale) {
            var screen = rect.gameObject.AddComponent<UiScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("offset").vector2Value = offset;
            so.FindProperty("scale").floatValue = scale;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Backing(RectTransform rect, Vector2 offset) {
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(rect);
            rect.offsetMin = offset;
            rect.offsetMax = offset;
        }

        // 기울인 버튼. 누름 판정은 GameUi가 이 이미지의 사각형으로 한다.
        // primary는 강조색 바탕(Ring It, Try Again, Resume), 아니면 반투명 바탕(Restart, Title).
        private static Image Pill(Transform parent, string name, string label, bool primary = true) {
            Image button = primary
                ? Shape(parent, name, UiPalette.Ring, 8f, ButtonSkew, 0.75f, 0f, -0.28f)
                : Shape(parent, name, UiPalette.Secondary, 6f, ButtonSkew, 0f, 0f, 0f);
            if (!primary) {
                // 보조 버튼은 테두리를 둘러 바탕을 옅게 둔다.
                Image edge = Shape(button.rectTransform, "Edge", new Color(1f, 1f, 1f, 0.35f), 6f, ButtonSkew, 0f, 0f, 0f);
                edge.GetComponent<UiShape>().outline = 1.5f;
                Stretch(edge.rectTransform);
            }

            button.gameObject.AddComponent<LayoutElement>().preferredHeight = primary ? 52f : 44f;
            TMP_Text text = Text(button.rectTransform, "Label", label, primary ? 19f : 16f, primary ? UiPalette.Night : UiPalette.Text2,
                bodyFont, null, FontStyles.Bold | FontStyles.Italic | FontStyles.UpperCase);
            text.characterSpacing = 4f;
            Stretch(text.rectTransform);
            return button;
        }

        // 화면 전체 바탕. 가장자리로 갈수록 하프톤 점이 커진다. dotTone +면 청회색 점, -면 검은 점.
        private static void Backdrop(RectTransform parent, Color color, float opacity, float dotTone) {
            Image image = Shape(parent, "Backdrop", color, 0f, 0f, 1f, UiShape.Radial, dotTone, opacity);
            Stretch(image.rectTransform);
        }

        private static TMP_Text Heading(Transform parent, string text, float size, Color color) =>
            Text(parent, "Heading", text, size, color, displayFont, null, FontStyles.Italic | FontStyles.UpperCase);

        // 작은 머리글(시각, Incoming call 등). 대문자에 자간을 넓힌다.
        private static TMP_Text Tag(Transform parent, string name, string text) {
            TMP_Text tag = Text(parent, name, text, 13f, UiPalette.Text4, bodyFont, null, FontStyles.Bold | FontStyles.UpperCase);
            tag.characterSpacing = 14f;
            return tag;
        }

        // UiHalftone으로 그리는 도형
        private static Image Shape(Transform parent, string name, Color color, float chamfer, float skew, float halftone,
            float halftoneAngle, float dotTone, float fillOpacity = 1f) {
            Image image = Img(parent, name, null, color, Image.Type.Simple);
            image.material = shapeMaterial;
            var shape = image.gameObject.AddComponent<UiShape>();
            shape.chamfer = chamfer;
            shape.skew = skew;
            shape.halftone = halftone;
            shape.halftoneAngle = halftoneAngle;
            shape.dotTone = dotTone;
            shape.fillOpacity = fillOpacity;
            return image;
        }

        private static RectTransform Node(string name, Transform parent) {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null) {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color, Image.Type type) {
            RectTransform rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(Transform parent, string name, string text, float size, Color color, TMP_FontAsset font,
            Material material = null, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center) {
            RectTransform rect = Node(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            // 본문의 굵은 글씨는 가짜 굵게 대신 Bold 폰트를 직접 쓴다. 그림자 머티리얼도 그 폰트 것으로 바꾼다.
            if (font == bodyFont && (style & FontStyles.Bold) != 0) {
                font = boldFont;
                style &= ~FontStyles.Bold;
                material = material == bodyShadow ? boldShadow : null;
            }

            tmp.font = font;
            if (material != null) {
                tmp.fontSharedMaterial = material;
            }

            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Wire(Component target, params (string field, Object value)[] fields) {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in fields) {
                SerializedProperty property = so.FindProperty(field);
                if (property == null) {
                    throw new ArgumentException($"{target.GetType().Name} has no field {field}");
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 에셋 ----------

        private static Material ShapeMaterial() {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) {
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(ShapeMaterialPath);
            if (material == null) {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ShapeMaterialPath);
            } else {
                material.shader = shader;
            }

            // 어두운 바탕 위의 밝은 점: 남색보다 조금 밝은 청회색. 강조색은 UI에만 쓴다.
            material.SetColor("_DotLight", new Color(0.15f, 0.22f, 0.29f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // HUD 글자 뒤 그림자 (프로토타입 text-shadow). 폰트 머티리얼을 복사해 밑그림을 켠다.
        private static Material ShadowMaterial(TMP_FontAsset font, string path) {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, path);
            } else {
                material.shader = font.material.shader;
                material.CopyPropertiesFromMaterial(font.material);
            }

            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.7f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.5f);
            material.SetFloat("_UnderlayDilate", 0.3f);
            material.SetFloat("_UnderlaySoftness", 0.6f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 둥근 사각형 SDF로 그린다. radius가 size/2면 원, stroke가 0보다 크면 테두리만 그린다.
        private static Sprite MakeSprite(string name, int size, float radius, float stroke, int border) {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    if (stroke > 0f) {
                        alpha *= Mathf.Clamp01(0.5f + distance + stroke);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            string path = $"{SpriteFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 가운데 조준점과 같은 링. 밝은 벽 위에서도 보이게 어두운 테두리를 두른다.
        private static Texture2D MakeCursor(string name) {
            const float radius = 4.5f;
            const float stroke = 1.6f;
            const float outline = 1.2f;
            var texture = new Texture2D(CursorSize, CursorSize, TextureFormat.RGBA32, false);
            float half = CursorSize * 0.5f;
            var pixels = new Color32[CursorSize * CursorSize];
            for (int y = 0; y < CursorSize; y++) {
                for (int x = 0; x < CursorSize; x++) {
                    float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                    // 링 중심선까지 거리
                    float d = Mathf.Abs(r - radius);
                    float white = Mathf.Clamp01(stroke * 0.5f + 0.5f - d);
                    float dark = Mathf.Clamp01(stroke * 0.5f + outline + 0.5f - d);
                    Color paper = UiPalette.Paper;
                    float alpha = Mathf.Max(white, dark * 0.75f);
                    Color color = Color.Lerp(new Color(0f, 0f, 0f), paper, alpha > 0f ? white / alpha : 0f);
                    color.a = alpha;
                    pixels[(CursorSize - 1 - y) * CursorSize + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            string path = $"{SpriteFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }

            string parentPath = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(path));
        }
    }
}
