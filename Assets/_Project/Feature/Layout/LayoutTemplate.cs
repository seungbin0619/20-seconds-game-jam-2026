using System;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Layout {
    /// <summary>
    /// 집 배치 틀. 방 슬롯(크기 등급, 중심, 방향)과 슬롯 사이의 문 연결을 정한다.
    /// 문 소켓이 벽 가운데에 있으므로, 연결된 두 슬롯은 맞닿은 벽의 가운데가 서로 겹치게 놓는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Missed Call/Layout Template", fileName = "LayoutTemplate_")]
    public sealed class LayoutTemplate : ScriptableObject {
        [Serializable]
        public struct Slot {
            public RoomSize size;
            [Tooltip("모듈 원점(바닥 중심)의 월드 XZ 위치(m)")]
            public Vector2 center;
            [Tooltip("켜면 모듈을 90도 돌려 놓는다 (너비가 z축을 따른다)")]
            public bool rotated;
        }

        [Serializable]
        public struct Link {
            public int a;
            public int b;
        }

        public Slot[] slots = new Slot[0];
        public Link[] links = new Link[0];

        public int RoomCount => slots.Length;
    }
}
