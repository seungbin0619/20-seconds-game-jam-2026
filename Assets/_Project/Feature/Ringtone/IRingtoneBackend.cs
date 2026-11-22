using System;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 실제 소리를 내는 쪽. Web 빌드는 Web Audio(jslib), 그 외(에디터 포함)는 Unity 오디오 근사치를 쓴다.
    /// 좌표는 Unity 월드 좌표로 넘긴다. 좌표계 변환은 구현이 맡는다.
    /// </summary>
    internal interface IRingtoneBackend : IDisposable {
        /// <summary>소리를 낼 수 있는 상태인가. Web에서는 사용자 입력 전까지 false다.</summary>
        bool IsReady { get; }

        string Status { get; }

        void Init(float rolloff);
        int CreateVoice(RingtonePattern pattern);

        /// <summary>샘플을 반복 재생하는 음원. 벨 음원처럼 위치, 먹먹함, 볼륨을 받는다.</summary>
        int CreateLoopVoice(float[] data, int sampleRate);
        void StartVoice(int voice);
        void StopVoice(int voice);
        void SetVoice(int voice, Vector3 position, float cutoff, float gain);
        void SetListener(Vector3 position, Vector3 forward);
        void PlayOneShot(in OneShot shot);

        /// <summary>효과음 샘플을 올려 둔다. 반환값은 <see cref="PlaySample"/>에 넘기는 번호.</summary>
        int CreateSample(float[] data, int sampleRate);

        /// <param name="rate">재생 속도. 음높이도 같이 바뀐다</param>
        /// <param name="spatial">true면 position에서 들린다 (거리 감쇠, 방향)</param>
        void PlaySample(int sample, float gain, float rate, bool spatial, Vector3 position);

        /// <summary>일시정지. 벨 박자를 멈추고 소리를 끈다. 풀면 벨 패턴을 처음부터 다시 울린다.</summary>
        void SetPaused(bool paused);
    }
}
