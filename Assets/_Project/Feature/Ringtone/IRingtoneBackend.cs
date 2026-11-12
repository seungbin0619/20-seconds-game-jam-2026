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
        void StartVoice(int voice);
        void StopVoice(int voice);
        void SetVoice(int voice, Vector3 position, float cutoff, float gain);
        void SetListener(Vector3 position, Vector3 forward);
        void PlayOneShot(in OneShot shot);

        /// <summary>일시정지. 벨 박자를 멈추고 소리를 끈다. 풀면 벨 패턴을 처음부터 다시 울린다.</summary>
        void SetPaused(bool paused);
    }
}
