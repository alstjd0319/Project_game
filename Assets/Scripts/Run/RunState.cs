using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 한 판(런)의 진행 상태 (정적 — 방마다 씬을 다시 불러도 이어진다). 사당 포탈이 <see cref="Begin"/>으로 시작하고,
    /// 방을 깨면 문 두 개 중 하나를 골라(<see cref="RollOptions"/>) 다음 방으로 넘어간다 (스컬 방식).
    /// 마지막 방(<see cref="TotalRooms"/>번째)은 정해져 있고, 깨면 사당으로 나가는 문이 열린다.
    /// <see cref="Active"/>가 아니면(테스트·연습으로 플레이 씬을 바로 연 경우) 방 흐름 없이 예전처럼 몬스터가 계속 나온다.
    /// </summary>
    public static class RunState
    {
        /// <summary>한 판의 방 개수 (가안).</summary>
        public const int TotalRooms = 6;

        public static bool Active { get; private set; }
        /// <summary>지금 방 번호 (1부터).</summary>
        public static int RoomNumber { get; private set; }
        public static RoomType Current { get; private set; }

        public static bool IsFinalRoom => Active && RoomNumber >= TotalRooms;

        /// <summary>새 판을 시작한다 (첫 방은 일반방).</summary>
        public static void Begin()
        {
            Active = true;
            RoomNumber = 1;
            Current = RoomType.Normal;
        }

        /// <summary>문을 골라 다음 방으로 (씬을 다시 불러오기 직전에 부른다).</summary>
        public static void Enter(RoomType type)
        {
            if (!Active) return;
            RoomNumber++;
            Current = type;
        }

        /// <summary>판이 끝났다 (사당으로 나감).</summary>
        public static void End()
        {
            Active = false;
            RoomNumber = 0;
        }

        public static void Reset() => End();

        // 방 종류 뽑기 가중치 (일반이 가장 많다)
        private static readonly (RoomType type, int weight)[] Weights =
        {
            (RoomType.Normal, 45), (RoomType.Elite, 20), (RoomType.Reward, 20), (RoomType.Shop, 15),
        };

        /// <summary>다음 방으로 가는 문 두 개의 종류 (서로 다르다). 마지막 방 직전이면 문은 하나이고 정해진 방(엘리트 관문)이다.</summary>
        public static RoomType[] RollOptions(System.Random rng = null)
        {
            if (RoomNumber + 1 >= TotalRooms) return new[] { RoomType.Elite };
            rng ??= new System.Random();
            var pool = new List<(RoomType type, int weight)>(Weights);
            var result = new RoomType[2];
            for (int i = 0; i < 2; i++)
            {
                int total = 0;
                foreach (var w in pool) total += w.weight;
                int roll = rng.Next(total);
                for (int k = 0; k < pool.Count; k++)
                {
                    roll -= pool[k].weight;
                    if (roll >= 0) continue;
                    result[i] = pool[k].type;
                    pool.RemoveAt(k);
                    break;
                }
            }
            return result;
        }
    }
}
