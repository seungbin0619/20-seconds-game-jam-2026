using System.Collections.Generic;
using System.IO;
using MissedCall.Core.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MissedCall.Feature.Room.Editor {
    /// <summary>
    /// 방 모듈 프리팹을 하나씩 빈 씬에 놓고 굽는다. 라이트맵 텍스처는 모듈 폴더의 Lightmaps/ 아래에 남기고,
    /// 렌더러별 인덱스와 스케일 오프셋은 프리팹의 <see cref="ModuleLightmap"/>에 기록한다. 굽기용 씬은 지운다.
    /// </summary>
    public static class ModuleLightmapBaker {
        private const string ModuleFolder = "Assets/_Project/Feature/Room/Graybox";
        private const string OutputFolder = ModuleFolder + "/Lightmaps";
        private const string SettingsPath = OutputFolder + "/ModuleBake.lighting";

        /// <summary>굽기 씬의 환경광. 런타임 씬의 환경광과 맞춘다 (라이트맵이 환경광을 포함한다).</summary>
        public static readonly Color Ambient = new(0.10f, 0.10f, 0.12f);

        [MenuItem("Missed Call/Room/Bake Module Lightmaps")]
        public static void BakeAllMenu() {
            Debug.Log("[ModuleLightmapBaker] " + BakeAll());
        }

        public static string BakeAll() {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++) {
                if (EditorSceneManager.GetSceneAt(i).isDirty) {
                    return "Save open scenes before baking.";
                }
            }

            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            var report = new List<string>();
            try {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ModuleFolder })) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab.GetComponent<RoomModule>() != null) {
                        report.Add(Bake(prefab));
                    }
                }
            } finally {
                if (previous.Length > 0) {
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                } else {
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                }
            }

            return string.Join("\n", report);
        }

        private static string Bake(GameObject prefab) {
            EnsureFolder(OutputFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            Lightmapping.lightingSettings = LoadOrCreateSettings();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.GetComponent<RoomModule>().CloseAllSockets();

            // 라이트맵은 씬 이름과 같은 폴더(Lightmaps/<모듈 이름>/)에 생긴다.
            string scenePath = OutputFolder + "/" + prefab.name + ".unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            if (!Lightmapping.Bake()) {
                return prefab.name + ": bake failed";
            }

            LightmapData[] maps = LightmapSettings.lightmaps;
            var textures = new Texture2D[maps.Length];
            for (int i = 0; i < maps.Length; i++) {
                textures[i] = maps[i].lightmapColor;
            }

            var entries = new List<ModuleLightmap.Entry>();
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true)) {
                int index = renderer.lightmapIndex;
                if (index < 0 || index >= maps.Length) {
                    continue;
                }

                entries.Add(new ModuleLightmap.Entry {
                    renderer = renderer,
                    index = index,
                    scaleOffset = renderer.lightmapScaleOffset,
                });
            }

            ModuleLightmap data = instance.GetComponent<ModuleLightmap>();
            if (data == null) {
                data = instance.AddComponent<ModuleLightmap>();
            }

            data.EditorSetup(textures, entries.ToArray());
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);

            // 굽기용 씬과 LightingData는 필요 없다. 텍스처만 남긴다.
            LightingDataAsset lightingData = Lightmapping.lightingDataAsset;
            string lightingDataPath = lightingData != null ? AssetDatabase.GetAssetPath(lightingData) : null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(lightingDataPath)) {
                AssetDatabase.DeleteAsset(lightingDataPath);
            }

            AssetDatabase.DeleteAsset(scenePath);

            long bytes = 0;
            foreach (Texture2D texture in textures) {
                string texturePath = AssetDatabase.GetAssetPath(texture);
                if (!string.IsNullOrEmpty(texturePath) && File.Exists(texturePath)) {
                    bytes += new FileInfo(texturePath).Length;
                }
            }

            string size = textures.Length > 0 ? $"{textures[0].width}x{textures[0].height}" : "-";
            return $"{prefab.name}: {textures.Length} lightmap(s) {size}, {entries.Count} renderers, source {bytes / 1024} KB";
        }

        private static LightingSettings LoadOrCreateSettings() {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (settings != null) {
                return settings;
            }

            settings = new LightingSettings {
                name = "ModuleBake",
                bakedGI = true,
                realtimeGI = false,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                directionalityMode = LightmapsMode.NonDirectional,
                // 회색 박스 기준 낮은 해상도. 빌드 크기를 보며 조정한다.
                lightmapResolution = 10f,
                lightmapPadding = 2,
                lightmapMaxSize = 512,
                lightmapCompression = LightmapCompression.NormalQuality,
                ao = true,
                aoMaxDistance = 0.6f,
                directSampleCount = 32,
                indirectSampleCount = 256,
                environmentSampleCount = 64,
                maxBounces = 2,
                filteringMode = LightingSettings.FilterMode.Auto,
            };
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
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
