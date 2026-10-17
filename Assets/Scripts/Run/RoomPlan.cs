using System.Collections.Generic;

namespace ParryRL
{
    /// <summary>한 방의 몬스터 웨이브 구성 (가안 — 플레이해 보고 조정).</summary>
    public static class RoomPlan
    {
        public struct Wave
        {
            public int Melee, Ranged;
            /// <summary>보스(불가살이) 수 (보스방만 1).</summary>
            public int Boss;
            /// <summary>엘리트 몬스터 수 (근접·원거리 중 위 숫자에 이미 포함되지 않는 추가분이 아니라, 그 중 몇 마리가 엘리트인지).</summary>
            public int EliteMelee, EliteRanged;
        }

        public const float EliteHpMultiplier = 3f;

        /// <summary>
        /// 방을 깼을 때 얻는 경험치 (가안). 런 중엔 몬스터를 잡아도 경험치가 없고 방 단위로만 준다 — 몬스터마다 주면 한 방에 레벨이 여러 번 올라 스킬 포인트가 너무 많았다.
        /// 싸움이 없는 보상·상점방은 0. 한 판(일반 3 · 엘리트 1 · 관문 1 정도)에 약 140 → Lv.1에서 Lv.4~5.
        /// </summary>
        public static int XpReward(RoomType type, bool finalRoom) => finalRoom ? 50 : type switch
        {
            RoomType.Normal => 20,
            RoomType.Elite => 35,
            _ => 0,
        };

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
                case RoomType.Boss:
                    waves.Add(new Wave { Boss = 1 }); // 보스 혼자 (졸개 없음, 가안)
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
