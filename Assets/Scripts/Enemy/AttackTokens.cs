using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 다수전 공격 겹침 대책: 공격(예비 모션 ~ 베기·발사)은 "공격권"을 쥔 몬스터만 시작할 수 있다.
    /// 동시에 쥘 수 있는 수(<see cref="GameTuning.maxAttackers"/>)와 공격 시작 사이 최소 간격(<see cref="GameTuning.attackStagger"/>)을 두어,
    /// 몬스터가 많아도 베기 순간이 한꺼번에 몰리지 않는다 — 바빠질 뿐 "막을 수 없는" 상황은 생기지 않게.
    /// 공격권은 회복 동작에 들어가거나 공격이 끊기면(경직·기절·죽음) 돌려준다.
    /// </summary>
    public static class AttackTokens
    {
        private static readonly HashSet<EnemyController> Holders = new();
        private static float _lastGrant = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Holders.Clear();
            _lastGrant = float.NegativeInfinity;
        }

        /// <summary>지금 공격권을 쥔 몬스터 수.</summary>
        public static int Count
        {
            get
            {
                Holders.RemoveWhere(h => h == null || h.IsDead);
                return Holders.Count;
            }
        }

        public static bool Holds(EnemyController enemy) => Holders.Contains(enemy);

        /// <summary>공격을 시작해도 되면 공격권을 쥐고 true.</summary>
        public static bool TryTake(EnemyController enemy)
        {
            var t = GameTuning.Current;
            if (t.maxAttackers <= 0) return true;
            int count = Count;
            if (Holders.Contains(enemy)) return true;
            if (count >= t.maxAttackers) return false;
            // 누군가 막 공격을 시작했으면 조금 기다린다 (먼저 시작한 공격이 짧게 끝나 공격권을 돌려줬어도 — 원거리 예비 모션은 0.2초뿐)
            if (Time.time - _lastGrant < t.attackStagger) return false;
            Holders.Add(enemy);
            _lastGrant = Time.time;
            return true;
        }

        public static void Release(EnemyController enemy) => Holders.Remove(enemy);
    }
}
