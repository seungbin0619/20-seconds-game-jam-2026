using System.Collections.Generic;
using MissedCall.Core;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Layout {
    /// <summary>
    /// 템플릿을 고르고 슬롯마다 방 모듈을 골라 배치한 뒤, 맞닿은 문 소켓을 열고 <see cref="RoomGraph"/>를 만든다.
    /// 모듈 인스턴스는 프리팹마다 하나씩 풀에 두고 재사용한다 (한 집에 같은 방 종류는 하나뿐이다).
    /// 무작위는 호출자가 넘긴 <see cref="System.Random"/>에서만 뽑는다.
    /// </summary>
    public sealed class HouseBuilder {
        /// <summary>소리 경로에 쓰는 문 지점 높이(m). 귀 높이에 가깝게 둔다.</summary>
        public const float DoorPointHeight = 1.2f;
        private const float SocketMatchDistance = 0.3f;

        private readonly RoomCatalog catalog;
        private readonly Transform root;
        private readonly Dictionary<RoomModule, RoomModule> pool = new Dictionary<RoomModule, RoomModule>();
        private readonly List<RoomModule> active = new List<RoomModule>();

        public HouseBuilder(RoomCatalog catalog, Transform root) {
            this.catalog = catalog;
            this.root = root;
        }

        /// <summary>마지막으로 고른 템플릿 (디버그 표시용).</summary>
        public LayoutTemplate LastTemplate { get; private set; }

        /// <summary>새 집을 만든다. 이전 집의 모듈은 꺼서 풀로 돌린다. 만들 수 없으면 null.</summary>
        public RoomGraph Build(System.Random rng, GameTuning tuning) {
            foreach (RoomModule module in active) {
                module.gameObject.SetActive(false);
            }

            active.Clear();

            var candidates = new List<LayoutTemplate>();
            foreach (LayoutTemplate template in catalog.templates) {
                if (template.RoomCount >= tuning.roomCount.x && template.RoomCount <= tuning.roomCount.y) {
                    candidates.Add(template);
                }
            }

            Shuffle(candidates, rng);
            foreach (LayoutTemplate template in candidates) {
                var picks = new Pick[template.slots.Length];
                if (Assign(template, 0, picks, new HashSet<RoomKind>(), rng)) {
                    LastTemplate = template;
                    return Place(template, picks);
                }

                Debug.LogWarning($"[HouseBuilder] No module fits template {template.name}.");
            }

            Debug.LogError("[HouseBuilder] Could not build a house from any template.");
            return null;
        }

        private struct Pick {
            public RoomModule prefab;
            public int quarterTurns;
        }

        // 슬롯 순서대로 모듈과 회전을 고른다. 막히면 되돌아간다. 슬롯이 4개 이하라 충분히 빠르다.
        private bool Assign(LayoutTemplate template, int slot, Pick[] picks, HashSet<RoomKind> used, System.Random rng) {
            if (slot == template.slots.Length) {
                return true;
            }

            LayoutTemplate.Slot data = template.slots[slot];
            List<WallSide> required = RequiredSides(template, slot);
            var modules = new List<RoomModule>();
            foreach (RoomModule module in catalog.modules) {
                if (module.SizeClass == data.size && !used.Contains(module.Kind)) {
                    modules.Add(module);
                }
            }

            Shuffle(modules, rng);
            var turns = new List<int>(data.rotated ? new[] { 1, 3 } : new[] { 0, 2 });
            foreach (RoomModule module in modules) {
                Shuffle(turns, rng);
                foreach (int turn in turns) {
                    if (!Covers(module, turn, required)) {
                        continue;
                    }

                    picks[slot] = new Pick { prefab = module, quarterTurns = turn };
                    used.Add(module.Kind);
                    if (Assign(template, slot + 1, picks, used, rng)) {
                        return true;
                    }

                    used.Remove(module.Kind);
                }
            }

            return false;
        }

        private RoomGraph Place(LayoutTemplate template, Pick[] picks) {
            int count = template.slots.Length;
            var rooms = new RoomModule[count];
            var footprints = new Rect[count];
            for (int i = 0; i < count; i++) {
                LayoutTemplate.Slot slot = template.slots[i];
                RoomModule module = Get(picks[i].prefab);
                module.transform.SetPositionAndRotation(new Vector3(slot.center.x, 0f, slot.center.y),
                    Quaternion.Euler(0f, 90f * picks[i].quarterTurns, 0f));
                module.CloseAllSockets();
                module.gameObject.SetActive(true);
                active.Add(module);
                rooms[i] = module;

                Vector2 size = picks[i].quarterTurns % 2 == 0 ? module.Size : new Vector2(module.Size.y, module.Size.x);
                footprints[i] = new Rect(slot.center - size * 0.5f, size);
            }

            var doors = new List<RoomDoor>();
            foreach (LayoutTemplate.Link link in template.links) {
                if (TryMatch(rooms[link.a], rooms[link.b], out DoorSocket socketA, out DoorSocket socketB)) {
                    socketA.SetOpen(true);
                    socketB.SetOpen(true);
                    Vector3 position = (socketA.transform.position + socketB.transform.position) * 0.5f;
                    position.y = DoorPointHeight;
                    doors.Add(new RoomDoor(link.a, link.b, position, socketA, socketB));
                } else {
                    Debug.LogError($"[HouseBuilder] Template {template.name}: sockets of slot {link.a} and {link.b} do not meet.");
                }
            }

            return new RoomGraph(rooms, footprints, doors.ToArray());
        }

        private RoomModule Get(RoomModule prefab) {
            if (!pool.TryGetValue(prefab, out RoomModule instance) || instance == null) {
                // 생성과 재사용 모두 OnEnable에서 라이트맵이 적용된다. 라이트맵은 위치와 무관하다.
                instance = Object.Instantiate(prefab, root);
                instance.name = prefab.name;
                pool[prefab] = instance;
            }

            return instance;
        }

        private static bool TryMatch(RoomModule a, RoomModule b, out DoorSocket socketA, out DoorSocket socketB) {
            foreach (DoorSocket sa in a.Sockets) {
                foreach (DoorSocket sb in b.Sockets) {
                    Vector3 delta = sa.transform.position - sb.transform.position;
                    delta.y = 0f;
                    if (delta.magnitude < SocketMatchDistance) {
                        socketA = sa;
                        socketB = sb;
                        return true;
                    }
                }
            }

            socketA = null;
            socketB = null;
            return false;
        }

        // 슬롯에서 연결된 이웃 쪽을 향하는 월드 방향 목록.
        private static List<WallSide> RequiredSides(LayoutTemplate template, int slot) {
            var sides = new List<WallSide>();
            foreach (LayoutTemplate.Link link in template.links) {
                int other;
                if (link.a == slot) {
                    other = link.b;
                } else if (link.b == slot) {
                    other = link.a;
                } else {
                    continue;
                }

                Vector2 delta = template.slots[other].center - template.slots[slot].center;
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) {
                    sides.Add(delta.x > 0f ? WallSide.East : WallSide.West);
                } else {
                    sides.Add(delta.y > 0f ? WallSide.North : WallSide.South);
                }
            }

            return sides;
        }

        // y축으로 90도씩 돌리면 North → East → South → West 순으로 바뀐다.
        private static bool Covers(RoomModule module, int quarterTurns, List<WallSide> required) {
            foreach (WallSide side in required) {
                bool found = false;
                foreach (DoorSocket socket in module.Sockets) {
                    if ((WallSide)(((int)socket.Side + quarterTurns) % 4) == side) {
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    return false;
                }
            }

            return true;
        }

        private static void Shuffle<T>(List<T> list, System.Random rng) {
            for (int i = list.Count - 1; i > 0; i--) {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
