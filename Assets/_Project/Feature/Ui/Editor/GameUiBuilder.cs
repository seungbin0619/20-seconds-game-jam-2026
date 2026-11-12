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

        // 디자인 폰트(Black Han Sans, IBM Plex Sans)를 넣기 전까지 둘 다 LiberationSans를 쓴다.
        private const string DisplayFontPath = "Assets/_Project/Shared/Fonts/UiFont_LiberationSans SDF.asset";
        private const string BodyFontPath = "Assets/_Project/Shared/Fonts/UiFont_LiberationSans SDF.asset";

        private const float HandsetWidth = 360f;

        private static TMP_FontAsset displayFont;
        private static TMP_FontAsset bodyFont;
        private static Material displayShadow;
        private static Material bodyShadow;
        private static Sprite circle;
        private static Sprite ring;
        private static Sprite panel;
        private static Sprite panelFrame;
        private static Texture2D cursorTexture;

        // 브라우저 CSS 커서는 32px를 넘으면 무시될 수 있다.
        private const int CursorSize = 32;

        [MenuItem("Missed Call/Ui/Build Game Ui")]
        public static void Build() {
            EnsureFolder(SpriteFolder);
            circle = MakeSprite("Ui_Circle", 64, 32f, 0f, 31);
            ring = MakeSprite("Ui_Ring", 32, 16f, 4.5f, 0);
            panel = MakeSprite("Ui_Panel", 96, 36f, 0f, 40);
            panelFrame = MakeSprite("Ui_PanelFrame", 96, 36f, 3f, 40);
            cursorTexture = MakeCursor("Ui_Cursor");

            displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            if (displayFont == null || bodyFont == null) {
                Debug.LogError("[GameUiBuilder] Font asset not found");
                return;
            }

            displayShadow = ShadowMaterial(displayFont, DisplayShadowPath);
            bodyShadow = ShadowMaterial(bodyFont, BodyShadowPath);

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
            var ui = root.AddComponent<GameUi>();

            Transform parent = root.transform;
            HudView hud = BuildHud(parent);
            AimView aim = BuildAim(parent);
            GameObject title = BuildTitle(parent, out TMP_Text titleClock, out Image startButton);
            AnswerSlide answer = BuildAnswer(parent);
            ResultView result = BuildResult(parent, out TMP_Text resultClock, out Image retryButton);

            var so = new SerializedObject(ui);
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
            var hud = rect.gameObject.AddComponent<HudView>();
            TMP_Text clock = Text(rect, "Clock", "20.0", 72f, UiPalette.Ring, displayFont, displayShadow);
            Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 80f));
            TMP_Text hint = Text(rect, "Hint", "Follow the sound and clear the piles", 20f, UiPalette.Paper, bodyFont, bodyShadow);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(900f, 34f));
            Wire(hud, ("clock", clock), ("hint", hint));
            return hud;
        }

        private static AimView BuildAim(Transform parent) {
            RectTransform rect = Stretch(Node("Aim", parent));
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
            Backdrop(rect, UiPalette.Night);
            RectTransform handset = Handset(rect);
            clock = Text(handset, "LockTime", "00:00", 15f, UiPalette.Muted, bodyFont);
            Text(handset, "Heading", "Missed Call", 54f, UiPalette.Ring, displayFont);
            Text(handset, "Lead", "Your phone is ringing somewhere. Find it and pick up before it stops.", 18f, UiPalette.Paper, bodyFont);
            string key = "<color=#FFD23F><b>{0}</b></color>  {1}";
            TMP_Text keys = Text(handset, "Keys",
                string.Format(key, "WASD", "Walk") + "\n"
                + string.Format(key, "Mouse", "Look around") + "\n"
                + string.Format(key, "Hold click", "Grab, swing and release to throw"),
                15f, UiPalette.Paper, bodyFont, null, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            keys.lineSpacing = 8f;
            button = Pill(handset, "Button", "Ring It");
            Text(handset, "Note", "Headphones make the direction much easier to hear.", 15f, UiPalette.Muted, bodyFont);
            return rect.gameObject;
        }

        private static AnswerSlide BuildAnswer(Transform parent) {
            RectTransform rect = Stretch(Node("Answer", parent));
            var slide = rect.gameObject.AddComponent<AnswerSlide>();
            RectTransform handset = Handset(rect);
            Text(handset, "LockTime", "Incoming call", 15f, UiPalette.Muted, bodyFont);
            TMP_Text caller = Text(handset, "Caller", "Mom", 20f, UiPalette.Paper, bodyFont, null, FontStyles.Bold);
            Text(handset, "Heading", "Pick up!", 40f, UiPalette.Answer, displayFont);

            Image track = Img(handset, "Track", circle, new Color(1f, 1f, 1f, 0.14f), Image.Type.Sliced);
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
            TMP_Text hint = Text(track.rectTransform, "Label", "Slide to answer", 15f, UiPalette.Muted, bodyFont);
            Stretch(hint.rectTransform);
            Image thumb = Img(track.rectTransform, "Thumb", circle, UiPalette.Answer, Image.Type.Simple);
            Place(thumb.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(54f, 54f), new Vector2(0f, 0.5f));

            Wire(slide, ("track", track.rectTransform), ("thumb", thumb.rectTransform), ("caller", caller));
            return slide;
        }

        private static ResultView BuildResult(Transform parent, out TMP_Text clock, out Image button) {
            RectTransform rect = Stretch(Node("Result", parent));
            var view = rect.gameObject.AddComponent<ResultView>();
            Backdrop(rect, new Color(8f / 255f, 11f / 255f, 14f / 255f, 0.34f));
            RectTransform handset = Handset(rect);
            clock = Text(handset, "LockTime", "00:00", 15f, UiPalette.Muted, bodyFont);
            TMP_Text head = Text(handset, "Heading", "Hello?", 40f, UiPalette.Answer, displayFont);
            TMP_Text caller = Text(handset, "Caller", "Mom", 20f, UiPalette.Paper, bodyFont, null, FontStyles.Bold);
            TMP_Text quote = Text(handset, "Quote", "\"Have you eaten yet?\"", 17f, UiPalette.Muted, bodyFont);
            TMP_Text stat = Text(handset, "Stat", "Answered in 0.0s", 15f, UiPalette.Muted, bodyFont);
            button = Pill(handset, "Button", "Try Again");
            Text(handset, "Note", "Press R to restart", 15f, UiPalette.Muted, bodyFont);
            Wire(view, ("head", head), ("caller", caller), ("quote", quote), ("stat", stat));
            return view;
        }

        // ---------- 부품 ----------

        private static RectTransform Handset(Transform parent) {
            RectTransform rect = Node("Handset", parent);
            Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HandsetWidth, 0f), new Vector2(0.5f, 0.5f));
            var fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = panel;
            fill.type = Image.Type.Sliced;
            fill.color = UiPalette.Glass;
            fill.raycastTarget = false;

            Image frame = Img(rect, "Frame", panelFrame, UiPalette.Frame, Image.Type.Sliced);
            Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 22);
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

        // 프로토타입의 button. 누름 판정은 GameUi가 이 이미지의 사각형으로 한다.
        private static Image Pill(Transform parent, string name, string label) {
            Image pill = Img(parent, name, circle, UiPalette.Ring, Image.Type.Sliced);
            pill.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            TMP_Text text = Text(pill.rectTransform, "Label", label, 19f, UiPalette.Night, bodyFont, null, FontStyles.Bold);
            Stretch(text.rectTransform);
            return pill;
        }

        private static void Backdrop(RectTransform parent, Color color) {
            Image image = Img(parent, "Backdrop", null, color, Image.Type.Simple);
            Stretch(image.rectTransform);
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
