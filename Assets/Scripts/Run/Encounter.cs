using System.Collections.Generic;

namespace ParryRL
{
    /// <summary>몬스터가 서는 자리의 기준: 방 좌표(플레이어는 왼쪽 끝 x≈−19에서 시작, 문은 오른쪽 끝) · 플레이어 앞 · 플레이어 뒤.</summary>
    public enum SpawnAnchor { Room, AheadOfPlayer, BehindPlayer }

    /// <summary>몬스터 한 마리의 등장: 종류 · 자리 · 높이(도깨비불만 — 땅에서 이만큼 떠서 등장) · 엘리트 · 웨이브 시작 뒤 늦게 나오는 시간.</summary>
    public struct SpawnSpec
    {
        public EnemyKind Kind;
        public float X;
        public SpawnAnchor Anchor;
        public float Height;
        public bool Elite;
        public float Delay;
        public bool Boss;

        public static SpawnSpec Melee(float x, SpawnAnchor anchor = SpawnAnchor.Room, float delay = 0f, bool elite = false) =>
            new() { Kind = EnemyKind.Melee, X = x, Anchor = anchor, Delay = delay, Elite = elite };

        public static SpawnSpec Ranged(float x, float height = 0f, SpawnAnchor anchor = SpawnAnchor.Room, float delay = 0f, bool elite = false) =>
            new() { Kind = EnemyKind.Ranged, X = x, Height = height, Anchor = anchor, Delay = delay, Elite = elite };

        public static SpawnSpec BossSpec() => new() { Boss = true };

        /// <summary>난이도 점수: 창귀 2 · 도깨비불 3 · 엘리트 +4 (가안).</summary>
        public int Cost => Boss ? 0 : (Kind == EnemyKind.Melee ? 2 : 3) + (Elite ? 4 : 0);
    }

    /// <summary>
    /// 직접 짠 전투 장면 하나. 몬스터 수를 공식으로 늘리던 방식 대신, 의도(무엇을 읽게 할지)가 있는 장면을 골라 쓴다.
    /// 장면마다 어울리는 지형이 정해져 있다 (예: 높이 뜬 도깨비불은 발판이 있는 지형에서만).
    /// </summary>
    public class Encounter
    {
        public string Id;
        public string Name;
        /// <summary>이 장면이 플레이어에게 시키는 것 (기획 메모 — 화면에 내보내지 않는다).</summary>
        public string Intent;
        public RoomType Type = RoomType.Normal;
        public int MinZone = 1, MaxZone = 3;
        /// <summary>첫 방 전용 등 방 번호를 정할 때 (0 = 제한 없음).</summary>
        public int OnlyRoom;
        /// <summary>어울리는 지형 (<see cref="RoomLayouts"/> 번호).</summary>
        public int[] Layouts;
        public SpawnSpec[][] Waves;

        public int Cost
        {
            get
            {
                int c = 0;
                foreach (var wave in Waves) foreach (var s in wave) c += s.Cost;
                return c;
            }
        }
    }

    public static class EncounterCatalog
    {
        private const SpawnAnchor Ahead = SpawnAnchor.AheadOfPlayer, Behind = SpawnAnchor.BehindPlayer;
        private static SpawnSpec M(float x, SpawnAnchor a = SpawnAnchor.Room, float delay = 0f) => SpawnSpec.Melee(x, a, delay);
        private static SpawnSpec ME(float x, SpawnAnchor a = SpawnAnchor.Room) => SpawnSpec.Melee(x, a, 0f, true);
        private static SpawnSpec R(float x, float h = 0f, SpawnAnchor a = SpawnAnchor.Room, float delay = 0f) => SpawnSpec.Ranged(x, h, a, delay);
        private static SpawnSpec RE(float x, float h = 0f) => SpawnSpec.Ranged(x, h, SpawnAnchor.Room, 0f, true);

        private const int Basic = RoomLayouts.Basic, Stairs = RoomLayouts.Stairs, Zigzag = RoomLayouts.Zigzag, Tower = RoomLayouts.Tower, Roof = RoomLayouts.Roof;

        public static readonly Encounter[] All =
        {
            // ── 1구간: 창귀만 — 패링 자체를 배운다 ──
            new() { Id = "first", Name = "첫 만남", Intent = "창귀 하나로 노랑 → 빨강 느낌표와 패링을 익힌다", OnlyRoom = 1, MaxZone = 1,
                Layouts = new[] { Basic },
                Waves = new[] { new[] { M(-8f) }, new[] { M(-4f), M(2f) } } },
            new() { Id = "pass", Name = "길목", Intent = "앞에서 하나씩 늦게 다가오는 창귀 — 연속 패링의 박자", MaxZone = 1,
                Layouts = new[] { Stairs, Roof },
                Waves = new[] { new[] { M(-6f), M(0f, delay: 1.5f) }, new[] { M(2f), M(8f), M(12f, delay: 2f) } } },
            new() { Id = "stagger", Name = "시간차", Intent = "셋이 차례로 들어와 공격 박자가 엇갈린다", MaxZone = 1,
                Layouts = new[] { Stairs, Zigzag, Roof },
                Waves = new[] { new[] { M(-5f), M(1f, delay: 1.2f), M(7f, delay: 2.4f) }, new[] { M(0f), M(6f) } } },

            // ── 1~3구간 공통: 협공 ──
            new() { Id = "pincer", Name = "협공", Intent = "앞뒤에서 창귀 — 방향을 바꿔 막는다", MaxZone = 3,
                Layouts = new[] { Stairs, Tower, Roof },
                Waves = new[] { new[] { M(6f, Ahead), M(5f, Behind, 2.5f) }, new[] { M(6f, Ahead), M(6f, Behind, 2f), R(10f) } } },

            // ── 2구간: 도깨비불과 높이가 더해진다 ──
            new() { Id = "embers", Name = "불빛 사이", Intent = "도깨비불 둘이 거리를 두고 쏜다 — 날아오는 불과 다가오는 창귀를 함께 본다", MinZone = 2,
                Layouts = new[] { Zigzag, Stairs },
                Waves = new[] { new[] { R(-4f), R(6f) }, new[] { M(5f, Ahead), M(5f, Behind, 2f), R(10f, 2.5f) } } },
            new() { Id = "cover", Name = "엄호 사격", Intent = "높이 뜬 도깨비불이 위에서 쏘는 동안 바닥의 창귀를 막는다 — 부적이나 발판으로 위를 처리", MinZone = 2,
                Layouts = new[] { Tower, Roof, Stairs },
                Waves = new[] { new[] { R(-5.5f, 3.5f), M(-6f), M(4f) }, new[] { R(-8f, 3f), R(8f, 3f), M(0f) } } },
            new() { Id = "brink", Name = "구멍 앞 압박", Intent = "구멍 건너편 창귀와 위의 도깨비불 — 밀리면 떨어진다", MinZone = 2,
                Layouts = new[] { Zigzag, Roof },
                Waves = new[] { new[] { M(-1f), M(9f), R(-12f, 2f) }, new[] { M(0f), M(10f), R(4f, 3f) } } },

            // ── 3구간: 섞어서 몰아친다 ──
            new() { Id = "siege", Name = "포위", Intent = "앞뒤 창귀 + 위의 도깨비불 — 가장 바쁜 장면", MinZone = 3,
                Layouts = new[] { Tower, Stairs, Roof },
                Waves = new[] { new[] { M(5f, Ahead), M(5f, Behind, 1.5f), R(0f, 4f) }, new[] { M(4f, Ahead), M(4f, Behind, 1f), R(-8f, 3f), R(8f, 3f) } } },
            new() { Id = "surge", Name = "파상 공세", Intent = "세 번 밀려오는 파도 — 마지막엔 앞뒤로 셋", MinZone = 3,
                Layouts = new[] { Zigzag, Roof, Stairs },
                Waves = new[] { new[] { M(-6f), M(0f) }, new[] { R(-6f), R(6f, 3f), M(3f) }, new[] { M(5f, Ahead), M(5f, Behind, 1f), M(8f, Ahead, 2f) } } },

            // ── 엘리트방 ──
            new() { Id = "elite_changgwi", Name = "우두머리 창귀", Intent = "체력 많은 창귀 하나를 끝까지 막아 낸다", Type = RoomType.Elite, MaxZone = 2,
                Layouts = new[] { Stairs, Roof },
                Waves = new[] { new[] { ME(2f), M(8f) } } },
            new() { Id = "elite_wisp", Name = "도깨비불 무리", Intent = "높이 뜬 엘리트 도깨비불 + 창귀 둘", Type = RoomType.Elite,
                Layouts = new[] { Tower, Roof },
                Waves = new[] { new[] { RE(4f, 2.5f), M(-4f), M(8f) } } },
            new() { Id = "elite_pair", Name = "두 우두머리", Intent = "엘리트 창귀와 엘리트 도깨비불이 차례로", Type = RoomType.Elite, MinZone = 3,
                Layouts = new[] { Tower, Stairs },
                Waves = new[] { new[] { ME(5f, Ahead), R(-6f, 3f) }, new[] { RE(6f, 3f), M(5f, Behind, 1.5f) } } },
        };

        /// <summary>방 번호별 난이도 예산 (가안). 장면의 점수가 예산 이하이고, 예산 − <see cref="BudgetSpread"/> 이상인 것 중에서 고른다.</summary>
        public static int Budget(int roomNumber) => roomNumber switch
        {
            <= 1 => 6,
            2 => 10,
            3 => 14,
            4 => 16,
            _ => 19,
        };

        public const int BudgetSpread = 6;

        /// <summary>테스트가 장면을 고정할 때 (null = 고정 안 함).</summary>
        public static string ForcedId;

        public static Encounter Find(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        /// <summary>이 방에 맞는 장면을 고른다: 방 종류 · 구간 · 예산(점수) · 앞 방과 다른 것. 맞는 게 없으면 예산에 가장 가까운 것.</summary>
        public static Encounter Pick(RoomType type, int roomNumber, int zone, int seed, string previousId)
        {
            if (!string.IsNullOrEmpty(ForcedId)) return Find(ForcedId);
            var fit = Candidates(type, roomNumber, zone);
            if (fit.Count > 1) fit.RemoveAll(e => e.Id == previousId);
            if (fit.Count == 0) return Closest(type, roomNumber);
            var rng = new System.Random(seed * 17 + roomNumber * 7 + (int)type);
            return fit[rng.Next(fit.Count)];
        }

        public static List<Encounter> Candidates(RoomType type, int roomNumber, int zone)
        {
            int budget = Budget(roomNumber);
            var list = new List<Encounter>();
            foreach (var e in All)
            {
                if (e.Type != type) continue;
                if (e.OnlyRoom > 0 ? e.OnlyRoom != roomNumber : roomNumber <= 1 && type == RoomType.Normal) continue; // 첫 방은 "첫 만남"만
                if (zone < e.MinZone || zone > e.MaxZone) continue;
                int cost = e.Cost;
                if (cost > budget || cost < budget - BudgetSpread) continue;
                list.Add(e);
            }
            return list;
        }

        private static Encounter Closest(RoomType type, int roomNumber)
        {
            int budget = Budget(roomNumber);
            Encounter best = null;
            foreach (var e in All)
            {
                if (e.Type != type) continue;
                if (best == null || System.Math.Abs(e.Cost - budget) < System.Math.Abs(best.Cost - budget)) best = e;
            }
            return best;
        }

        /// <summary>장면이 허용한 지형 중 앞 방과 다른 것 (지형 고정 테스트 값이 있으면 그것).</summary>
        public static int PickLayout(Encounter e, int seed, int roomNumber, int previous)
        {
            if (RoomLayouts.ForcedIndex >= 0) return RoomLayouts.ForcedIndex;
            var pool = new List<int>(e.Layouts);
            if (pool.Count > 1) pool.Remove(previous);
            var rng = new System.Random(seed * 31 + roomNumber);
            return pool[rng.Next(pool.Count)];
        }
    }
}
