using System.Collections.Generic;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 물건 인스턴스 풀. 판마다 전부 돌려받고 앞에서부터 다시 꺼낸다. 모자라면 늘리고 줄이지 않는다.
    /// Web에서 판마다 대량 Instantiate/Destroy를 하면 GC 끊김이 생긴다.
    /// </summary>
    public sealed class ClutterPool {
        private readonly ClutterItem prefab;
        private readonly Transform root;
        private readonly List<ClutterItem> items = new();
        private int used;

        public ClutterPool(ClutterItem prefab, Transform root, int prewarm) {
            this.prefab = prefab;
            this.root = root;
            for (int i = 0; i < prewarm; i++) {
                items.Add(Create());
            }
        }

        public int Capacity => items.Count;
        public int Used => used;

        /// <summary>이번 판에 꺼낸 물건 (앞에서부터 <see cref="Used"/>개).</summary>
        public IReadOnlyList<ClutterItem> Items => items;

        public ClutterItem Take() {
            if (used == items.Count) {
                items.Add(Create());
            }

            return items[used++];
        }

        public void ReleaseAll() {
            for (int i = 0; i < used; i++) {
                items[i].Despawn();
            }

            used = 0;
        }

        private ClutterItem Create() {
            ClutterItem item = Object.Instantiate(prefab, root);
            item.name = $"{prefab.name}_{items.Count:00}";
            item.gameObject.SetActive(false);
            return item;
        }
    }
}
