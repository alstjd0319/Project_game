using System.Collections.Generic;

namespace ParryRL
{
    /// <summary>웨이브 맵의 구역 (가로 범위). 구역 안의 바닥·발판이 스폰 지점이 된다.</summary>
    public class WaveZone
    {
        public string Name;
        public float MinX, MaxX;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public bool Contains(float x) => x >= MinX && x <= MaxX;
    }

    /// <summary>몬스터가 나오는 고정 지점. 발판이면 Platform이 있고, 그 발판 안에서만 움직인다.</summary>
    public struct WaveSpawnPoint
    {
        public float X;
        public float Height;            // 윗면 높이 (바닥 0)
        public PlatformSpec? Platform;
    }

    /// <summary>웨이브 하나의 구성 (docs/기획_웨이브모드.md 4절, 가안).</summary>
    public class WaveDef
    {
        public int Total, Concurrent;
        public int MeleeWeight = 1, RangedWeight;
        public int Tokens = 2;
        public float Stagger = 0.7f, HeavyChance = 0.3f;
        public int[] Zones;
        public string ZoneName;
        /// <summary>이 층까지의 발판에서 나온다 (0 = 바닥만) — 초반부터 7층으로 올라가게 하지 않으려고.</summary>
        public int MaxLevel;
        public bool Elite, Boss;
    }

    public static class WavePlan
    {
        public const int Plaza = 0, PitsLeft = 1, PitsRight = 2, Tower = 3, Stairs = 4;
        /// <summary>일반 몬스터 체력 배율: 수가 많은 대신 약하게 (창귀 100 → 60).</summary>
        public const float MinionHpScale = 0.6f;
        /// <summary>새 몬스터가 활성 구역에서 나올 비율 (나머지는 다른 구역 — 이동 중에도 싸움이 생기게).</summary>
        public const float SpawnInterval = 0.5f;
        public const float MinPlayerDistance = 3f;

        public static readonly WaveZone[] ZoneList =
        {
            new() { Name = "광장", MinX = -11f, MaxX = 11f },
            new() { Name = "왼쪽 구멍 지대", MinX = -23f, MaxX = -10f },
            new() { Name = "오른쪽 구멍 지대", MinX = 10f, MaxX = 23f },
            new() { Name = "탑 구역", MinX = -44f, MaxX = -24f },
            new() { Name = "계단산 구역", MinX = 24f, MaxX = 44f },
        };

        public static readonly WaveDef[] Waves =
        {
            new() { Total = 10, Concurrent = 5, Tokens = 1, Stagger = 0.8f, HeavyChance = 0.15f, Zones = new[] { Plaza }, ZoneName = "광장", MaxLevel = 0 },
            new() { Total = 12, Concurrent = 6, Tokens = 2, Stagger = 0.8f, HeavyChance = 0.2f, Zones = new[] { Plaza }, ZoneName = "광장", MaxLevel = 0 },
            new() { Total = 16, Concurrent = 7, MeleeWeight = 3, RangedWeight = 1, Stagger = 0.7f, HeavyChance = 0.25f, Zones = new[] { Tower }, ZoneName = "탑 구역", MaxLevel = 4 },
            new() { Total = 18, Concurrent = 8, MeleeWeight = 2, RangedWeight = 1, Stagger = 0.7f, HeavyChance = 0.3f, Zones = new[] { Tower }, ZoneName = "탑 구역", MaxLevel = 4 },
            new() { Total = 5, Concurrent = 5, Elite = true, Stagger = 0.7f, HeavyChance = 0.3f, Zones = new[] { PitsLeft, PitsRight }, ZoneName = "구멍 지대", MaxLevel = 2 },
            new() { Total = 22, Concurrent = 9, MeleeWeight = 2, RangedWeight = 1, Stagger = 0.6f, HeavyChance = 0.3f, Zones = new[] { Stairs }, ZoneName = "계단산 구역", MaxLevel = 7 },
            new() { Total = 24, Concurrent = 10, MeleeWeight = 1, RangedWeight = 1, Stagger = 0.6f, HeavyChance = 0.35f, Zones = new[] { Stairs }, ZoneName = "계단산 구역", MaxLevel = 7 },
            new() { Total = 28, Concurrent = 11, MeleeWeight = 1, RangedWeight = 1, Tokens = 3, Stagger = 0.6f, HeavyChance = 0.35f, Zones = new[] { Tower, Stairs }, ZoneName = "탑 구역 · 계단산 구역", MaxLevel = 7 },
            new() { Total = 30, Concurrent = 12, MeleeWeight = 1, RangedWeight = 1, Tokens = 3, Stagger = 0.5f, HeavyChance = 0.4f, Zones = new[] { Tower, Stairs }, ZoneName = "탑 구역 · 계단산 구역", MaxLevel = 7 },
            new() { Total = 1, Concurrent = 1, Boss = true, Zones = new[] { Plaza }, ZoneName = "광장" },
        };

        /// <summary>구역의 스폰 지점: 바닥은 구멍을 피해 약 5칸마다, 발판은 폭 3칸 이상이고 maxLevel 층까지.</summary>
        public static List<WaveSpawnPoint> Points(int zone, int maxLevel)
        {
            var result = new List<WaveSpawnPoint>();
            var z = ZoneList[zone];
            var map = RoomLayouts.WaveMap;
            foreach (var (x0, x1) in RoomLayouts.GroundIntervals(map, RoomLayouts.WaveArenaHalf - 1f))
            {
                float a = System.Math.Max(x0 + 1.5f, z.MinX), b = System.Math.Min(x1 - 1.5f, z.MaxX);
                for (float x = a; x <= b; x += 5f) result.Add(new WaveSpawnPoint { X = x });
            }
            foreach (var p in map.Platforms)
            {
                if (p.Width < 3f || !z.Contains(p.X)) continue;
                if (RoomLayouts.Top(p) > RoomLayouts.Level(maxLevel) + 0.01f) continue;
                result.Add(new WaveSpawnPoint { X = p.X, Height = RoomLayouts.Top(p), Platform = p });
            }
            return result;
        }
    }
}
