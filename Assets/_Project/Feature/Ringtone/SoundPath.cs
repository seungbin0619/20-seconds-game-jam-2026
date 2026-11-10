using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>소리를 실제로 재생할 가상 음원 위치와 벽 가림 정도.</summary>
    public readonly struct SoundRoute {
        public readonly Vector3 Position;
        /// <summary>벽 수. 문에 가까워질수록 소수로 줄어든다.</summary>
        public readonly float Walls;

        public SoundRoute(Vector3 position, float walls) {
            Position = position;
            Walls = walls;
        }
    }

    /// <summary>
    /// 다른 방의 소리를 문을 돌아오는 소리처럼 만든다.
    /// - 거리: 문을 경유하는 경로 길이를 쓴다. 문을 지나도 거리가 끊기지 않는다.
    /// - 방향: 문 근처(blendRadius 안)에서 문 쪽 방향이 문 다음 지점 방향으로 서서히 바뀐다.
    /// - 벽: 문에 다가갈수록 한 겹이 서서히 걷히고 문턱에서 다 걷힌다.
    /// </summary>
    public static class SoundPath {
        /// <summary>같은 방: 음원 그대로.</summary>
        public static SoundRoute Direct(Vector3 source) => new SoundRoute(source, 0f);

        /// <param name="firstDoor">듣는 사람의 방에서 음원 쪽으로 나가는 첫 문</param>
        /// <param name="afterDoor">첫 문 다음 지점. 벽이 1겹이면 음원, 더 많으면 다음 문</param>
        /// <param name="pathFromDoor">첫 문에서 음원까지 경로 길이</param>
        /// <param name="walls">사이에 있는 벽 수 (1 이상)</param>
        public static SoundRoute ThroughDoor(Vector3 listener, Vector3 firstDoor, Vector3 afterDoor,
            float pathFromDoor, int walls, float blendRadius) {
            // 문 근접도는 수평 거리로 잰다. 문 지점 높이와 눈높이가 달라도 문턱에서 정확히 0이 된다.
            Vector3 flat = firstDoor - listener;
            flat.y = 0f;
            float open = blendRadius > 0f ? Mathf.Clamp01(1f - flat.magnitude / blendRadius) : 0f;
            open = open * open * (3f - 2f * open);

            Vector3 toDoor = firstDoor - listener;
            float doorDistance = toDoor.magnitude;
            Vector3 toAfter = afterDoor - listener;
            float afterDistance = toAfter.magnitude;
            Vector3 afterDirection = afterDistance > 1e-4f ? toAfter / afterDistance : Vector3.forward;
            Vector3 doorDirection = doorDistance > 1e-4f ? toDoor / doorDistance : afterDirection;
            Vector3 direction = Vector3.Slerp(doorDirection, afterDirection, open).normalized;

            // 문턱(open = 1)에서는 문 다음 지점으로 곧장 가는 경로와 길이가 같아져, 문을 넘은 뒤의 값과 이어진다.
            float beyondAfter = Mathf.Max(0f, pathFromDoor - Vector3.Distance(firstDoor, afterDoor));
            float viaDoor = doorDistance + pathFromDoor;
            float viaAfter = afterDistance + beyondAfter;
            float pathLength = Mathf.Lerp(viaDoor, viaAfter, open);

            return new SoundRoute(listener + direction * pathLength, Mathf.Max(0f, walls - open));
        }
    }
}
