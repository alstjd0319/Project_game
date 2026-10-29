using System.Collections.Generic;

namespace ParryRL
{
    /// <summary>발판 하나의 배치: 가운데 x · 가운데 y(두께 0.4) · 폭. 바닥이 y=0이고 점프 한 번 약 1.56, 이단 점프 약 3.1 — 한 층은 윗면 StepHeight(1.5)씩.</summary>
    public struct PlatformSpec
    {
        public float X, Y, Width;
        public PlatformSpec(float x, float y, float width) { X = x; Y = y; Width = width; }
    }

    /// <summary>바닥에 뚫린 구멍: 가운데 x · 폭. 빠지면 피해를 입고 떨어지기 전 자리로 돌아온다.</summary>
    public struct PitSpec
    {
        public float X, Width;
        public PitSpec(float x, float width) { X = x; Width = width; }
        public float Left => X - Width * 0.5f;
        public float Right => X + Width * 0.5f;
    }

    /// <summary>방의 지형 배치 (발판 구성). 좌우로만 달리는 게 아니라 위·아래로도 움직이게 방마다 다르게 깐다.</summary>
    public class RoomLayout
    {
        public string Name;
        public PlatformSpec[] Platforms;
        public PitSpec[] Pits = new PitSpec[0];
    }

    public static class RoomLayouts
    {
        /// <summary>발판 오브젝트는 빌더가 이만큼 만들어 둔다 (레이아웃이 쓰는 개수가 이보다 적으면 나머지는 꺼 둔다).</summary>
        public const int MaxPlatforms = 6;
        /// <summary>바닥 조각도 빌더가 이만큼 만들어 둔다 (구멍 n개 → 조각 n+1개).</summary>
        public const int MaxPits = 2;
        public const float GroundHalfExtent = 25f; // 바닥 판정이 놓인 가로 범위 (벽 안쪽 여유 포함)
        /// <summary>구멍은 점프로 건널 수 있어야 한다 (이단 점프가 아니어도 한 번 점프로 약 4.5, 여유를 둔 값).</summary>
        public const float MaxPitWidth = 3.6f;
        /// <summary>구멍은 시작 자리(왼쪽 끝)와 문 앞(오른쪽 끝)에서 떨어져 있다.</summary>
        public const float PitZone = 12f;

        /// <summary>구멍을 뺀 바닥 구간들 (왼쪽부터).</summary>
        public static List<(float x0, float x1)> GroundIntervals(RoomLayout layout)
        {
            var result = new List<(float, float)>();
            var pits = new List<PitSpec>(layout.Pits);
            pits.Sort((a, b) => a.X.CompareTo(b.X));
            float x = -GroundHalfExtent;
            foreach (var pit in pits)
            {
                if (pit.Left > x) result.Add((x, pit.Left));
                x = pit.Right;
            }
            if (x < GroundHalfExtent) result.Add((x, GroundHalfExtent));
            return result;
        }
        /// <summary>다음 방 문(오른쪽 끝 x≈20 · 16, 글자 폭 약 3.4)을 가리지 않도록 발판이 바깥(오른쪽)으로 닿을 수 있는 한계. 좌우 대칭으로 두어 왼쪽도 같은 한계 안쪽이다.</summary>
        public const float MaxGateReach = 14.2f;

        public const int Basic = 0, Stairs = 1, Zigzag = 2, Tower = 3, Roof = 4, Flat = 5;

        /// <summary>
        /// 층 높이 규칙: 바닥이나 옆 발판에서 한 층 = 윗면 <see cref="StepHeight"/>씩 (점프 한 번 약 1.56 · 이단 점프 약 3.1 — 이단 점프로 넉넉히, 최대로 뛰지 않아도),
        /// 가로로 겹치는 발판끼리는 두 층 이상 떨어뜨린다 (아래 발판에 선 몸 1.4가 위 발판에 걸리지 않게). 윗면 = Y + 0.2.
        /// 2026-10-29: 처음 1.6~2.2 → "너무 높다"로 1.2 → "좀 더 높게"로 1.5.
        /// </summary>
        public const float StepHeight = 1.5f;

        /// <summary>n층 발판의 가운데 y (윗면 = n × StepHeight).</summary>
        private static float L(int n) => n * StepHeight - 0.2f;

        /// <summary>n층 윗면 높이 (장면이 도깨비불을 세울 층을 고를 때).</summary>
        public static float Level(int n) => n * StepHeight;

        public static readonly RoomLayout[] All =
        {
            new() { Name = "기본", Platforms = new[] { new PlatformSpec(-7f, L(1), 4f), new PlatformSpec(7f, L(1), 4f), new PlatformSpec(0f, L(2), 3f) } },
            new() { Name = "계단산", Platforms = new[]
            {
                new PlatformSpec(-12f, L(1), 4f), new PlatformSpec(-6f, L(2), 4f), new PlatformSpec(0f, L(3), 4f),
                new PlatformSpec(6f, L(2), 4f), new PlatformSpec(12f, L(1), 4f),
            } },
            new() { Name = "지그재그", Platforms = new[]
            {
                new PlatformSpec(-11.5f, L(1), 5f), new PlatformSpec(0f, L(1), 5f), new PlatformSpec(11.5f, L(1), 5f),
                new PlatformSpec(-7f, L(2), 4f), new PlatformSpec(7f, L(2), 4f), new PlatformSpec(0f, L(3), 3f),
            }, Pits = new[] { new PitSpec(-5.5f, 3.4f), new PitSpec(5.5f, 3.4f) } },
            new() { Name = "탑", Platforms = new[]
            {
                new PlatformSpec(0f, L(2), 8f), new PlatformSpec(-9f, L(1), 4f), new PlatformSpec(9f, L(1), 4f),
                new PlatformSpec(-12.5f, L(2), 3f), new PlatformSpec(12.5f, L(2), 3f),
            }, Pits = new[] { new PitSpec(0f, 3.4f) } },
            new() { Name = "지붕", Platforms = new[]
            {
                new PlatformSpec(0f, L(2), 14f), new PlatformSpec(-11f, L(1), 3.5f), new PlatformSpec(11f, L(1), 3.5f),
            }, Pits = new[] { new PitSpec(-8.5f, 3f), new PitSpec(8.5f, 3f) } },
            new() { Name = "평지", Platforms = new PlatformSpec[0] },
        };

        /// <summary>발판 윗면 높이.</summary>
        public static float Top(PlatformSpec p) => p.Y + 0.2f;

        /// <summary>
        /// 도깨비불이 설 발판: 장면이 원하는 자리(x)·높이에 가장 가까운 발판 (가로 거리 + 높이 차 × 2). 발판이 없으면 null.
        /// </summary>
        public static PlatformSpec? NearestPlatform(RoomLayout layout, float x, float height)
        {
            PlatformSpec? best = null;
            float bestScore = float.MaxValue;
            foreach (var p in layout.Platforms)
            {
                float dx = System.Math.Max(0f, System.Math.Abs(x - p.X) - p.Width * 0.5f);
                float score = dx + System.Math.Abs(Top(p) - height) * 2f;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        /// <summary>테스트가 레이아웃을 고정할 때 쓴다 (−1 = 고정 안 함).</summary>
        public static int ForcedIndex = -1;

        private static readonly int[] FightPool = { Stairs, Zigzag, Tower, Roof };

        /// <summary>이 방의 레이아웃 번호. 연습 모드·첫 방은 기본, 보상·상점은 제단이 가려지지 않게 평지, 마지막 보스방은 평지, 그 밖의 싸움방은 바로 앞 방과 다르게 돌려 뽑는다.</summary>
        public static int Pick(bool run, RoomType type, int roomNumber, int seed, int previous)
        {
            if (ForcedIndex >= 0) return ForcedIndex;
            if (!run || roomNumber <= 1) return Basic;
            if (roomNumber >= RunState.TotalRooms || type == RoomType.Boss) return Flat; // 보스방은 평지로 고정
            if (type == RoomType.Reward || type == RoomType.Shop) return Flat;
            var rng = new System.Random(seed * 31 + roomNumber);
            var pool = new List<int>(FightPool);
            pool.Remove(previous);
            return pool[rng.Next(pool.Count)];
        }
    }
}
