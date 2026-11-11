using System;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Room {
    /// <summary>
    /// 모듈 단독으로 구운 라이트맵을 런타임에 복원한다. Unity는 프리팹에 라이트맵을 저장하지 않으므로
    /// 굽기 도구(ModuleLightmapBaker)가 렌더러별 인덱스와 스케일 오프셋을 여기에 기록한다.
    /// 켜질 때마다 적용하므로 풀에서 꺼낸 모듈도 따로 호출할 필요가 없다.
    /// </summary>
    [RequireComponent(typeof(RoomModule))]
    public sealed class ModuleLightmap : MonoBehaviour {
        [Serializable]
        public struct Entry {
            public MeshRenderer renderer;
            [Tooltip("lightmaps 배열 안의 인덱스 (모듈 로컬)")]
            public int index;
            public Vector4 scaleOffset;
        }

        // 라이트맵 없음. Unity 내부 값과 같다.
        private const int NoLightmap = 0xFFFF;

        [SerializeField] private Texture2D[] lightmaps = new Texture2D[0];
        [SerializeField] private Entry[] entries = new Entry[0];

        public bool HasData => lightmaps.Length > 0 && entries.Length > 0;

        private void OnEnable() {
            Apply();
        }

        public void Apply() {
            if (!HasData) {
                return;
            }

            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            var globalIndex = new int[lightmaps.Length];
            for (int i = 0; i < lightmaps.Length; i++) {
                globalIndex[i] = Register(lightmaps[i]);
            }

            foreach (Entry entry in entries) {
                if (entry.renderer == null) {
                    continue;
                }

                entry.renderer.lightmapIndex = globalIndex[entry.index];
                entry.renderer.lightmapScaleOffset = entry.scaleOffset;
            }

            Light light = GetComponent<RoomModule>().MainLight;
            if (light != null) {
                // Baked Indirect: 간접광은 라이트맵, 직접광은 실시간.
                light.bakingOutput = new LightBakingOutput {
                    isBaked = true,
                    lightmapBakeType = LightmapBakeType.Mixed,
                    mixedLightingMode = MixedLightingMode.IndirectOnly,
                    probeOcclusionLightIndex = -1,
                    occlusionMaskChannel = -1,
                };
            }
        }

        /// <summary>라이트맵을 떼어 실시간 조명만 보이게 한다. 비교 확인용.</summary>
        public void Clear() {
            foreach (Entry entry in entries) {
                if (entry.renderer != null) {
                    entry.renderer.lightmapIndex = NoLightmap;
                }
            }
        }

        // 같은 텍스처는 한 번만 등록한다. 모듈 종류 수만큼만 늘어난다.
        private static int Register(Texture2D color) {
            LightmapData[] current = LightmapSettings.lightmaps;
            for (int i = 0; i < current.Length; i++) {
                if (current[i].lightmapColor == color) {
                    return i;
                }
            }

            var next = new LightmapData[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = new LightmapData { lightmapColor = color };
            LightmapSettings.lightmaps = next;
            return current.Length;
        }

#if UNITY_EDITOR
        public void EditorSetup(Texture2D[] maps, Entry[] rendererEntries) {
            lightmaps = maps;
            entries = rendererEntries;
        }
#endif
    }
}
