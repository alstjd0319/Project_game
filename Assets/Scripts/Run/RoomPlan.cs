using System.Collections.Generic;

namespace ParryRL
{
    /// <summary>한 방의 몬스터 웨이브 구성 (가안 — 플레이해 보고 조정).</summary>
    public static class RoomPlan
    {
        public struct Wave
        {
            public int Melee, Ranged;
            /// <summary>엘리트 몬스터 수 (근접·원거리 중 위 숫자에 이미 포함되지 않는 추가분이 아니라, 그 중 몇 마리가 엘리트인지).</summary>
            public int EliteMelee, EliteRanged;
        }

        public const float EliteHpMultiplier = 3f;

        public static List<Wave> For(RoomType type, int roomNumber)
        {
            int n = System.Math.Max(1, roomNumber);
            var waves = new List<Wave>();
            switch (type)
            {
                case RoomType.Normal:
                    waves.Add(new Wave { Melee = System.Math.Min(4, 2 + n / 3), Ranged = System.Math.Min(3, 1 + n / 4) });
                    waves.Add(new Wave { Melee = System.Math.Min(4, 2 + n / 2), Ranged = System.Math.Min(3, 1 + n / 3) });
                    break;
                case RoomType.Elite:
                    // 엘리트 한 마리(방 번호가 홀수면 근접, 짝수면 원거리) + 졸개
                    waves.Add(new Wave
                    {
                        Melee = 1 + (n % 2 == 1 ? 1 : 0), Ranged = 1 + (n % 2 == 0 ? 1 : 0),
                        EliteMelee = n % 2 == 1 ? 1 : 0, EliteRanged = n % 2 == 0 ? 1 : 0,
                    });
                    if (n >= RunState.TotalRooms) // 마지막 관문: 엘리트 둘
                        waves.Add(new Wave { Melee = 1, Ranged = 1, EliteMelee = 1, EliteRanged = 1 });
                    break;
            }
            return waves;
        }
    }
}
