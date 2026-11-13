using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MissedCall.Feature.ScreenFx.Editor {
    /// <summary>
    /// ScreenFx 머티리얼을 만들고 URP 렌더러(PC, Mobile)에 Full Screen Pass 기능으로 넣는다.
    /// 이미 있으면 설정만 다시 맞춘다. 셰이더는 머티리얼 에셋이 참조하므로 빌드에 포함된다.
    /// </summary>
    public static class ScreenFxInstaller {
        private const string Folder = "Assets/_Project/Feature/ScreenFx";
        private const string ShaderPath = Folder + "/ScreenFx.shader";
        private const string MaterialPath = Folder + "/ScreenFx.mat";
        private const string FeatureName = "ScreenFx";

        private static readonly string[] RendererPaths = {
            "Assets/_Project/Core/Settings/PC_Renderer.asset",
            "Assets/_Project/Core/Settings/Mobile_Renderer.asset",
        };

        [MenuItem("Missed Call/Screen Fx/Install Renderer Feature")]
        public static void Install() {
            Material material = EnsureMaterial();
            foreach (string path in RendererPaths) {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) {
                    Debug.LogWarning($"[ScreenFx] Renderer not found: {path}");
                    continue;
                }

                EnsureFeature(data, material);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ScreenFx] Installed");
        }

        private static Material EnsureMaterial() {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) {
                material = new Material(shader) { name = "ScreenFx" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            } else if (material.shader != shader) {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void EnsureFeature(UniversalRendererData data, Material material) {
            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>()
                .FirstOrDefault(f => f.name == FeatureName);
            if (feature == null) {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);

                // 렌더러는 기능 목록과 함께 로컬 파일 ID 목록(m_RendererFeatureMap)을 들고 있다.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var serialized = new SerializedObject(data);
                SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // 기본 후처리(톤 매핑 등)보다 먼저 그려서 그 위에 URP 후처리가 얹히게 한다.
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(true);

            EditorUtility.SetDirty(feature);
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }
    }
}
