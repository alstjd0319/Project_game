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
        // 공격권을 조금 더 쥐고 있을 몬스터 → 그때까지 (도깨비불: 불덩이가 날아가는 동안)
        private static readonly Dictionary<EnemyController, float> HoldUntil = new();
        private static float _lastGrant = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Holders.Clear();
            HoldUntil.Clear();
            _lastGrant = float.NegativeInfinity;
        }

        /// <summary>지금 공격권을 쥔 몬스터 수.</summary>
        public static int Count
        {
            get
            {
                Holders.RemoveWhere(h => h == null || h.IsDead || (HoldUntil.TryGetValue(h, out float until) && Time.time >= until));
                if (HoldUntil.Count > 0)
                {
                    var done = new List<EnemyController>();
                    foreach (var kv in HoldUntil) if (!Holders.Contains(kv.Key)) done.Add(kv.Key);
                    foreach (var e in done) HoldUntil.Remove(e);
                }
                return Holders.Count;
            }
        }

        public static bool Holds(EnemyController enemy)
        {
            _ = Count; // 쥐는 시간이 다 된 공격권을 먼저 정리
            return Holders.Contains(enemy);
        }

        /// <summary>공격을 시작해도 되면 공격권을 쥐고 true.</summary>
        public static bool TryTake(EnemyController enemy)
        {
            var t = GameTuning.Current;
            if (t.maxAttackers <= 0) return true;
            int count = Count;
            if (Holders.Contains(enemy))
            {
                HoldUntil.Remove(enemy); // 불덩이가 날아가는 중에 다시 공격 — 새 공격 동안 계속 쥔다
                return true;
            }
            if (count >= t.maxAttackers) return false;
            // 누군가 막 공격을 시작했으면 조금 기다린다 (먼저 시작한 공격이 짧게 끝나 공격권을 돌려줬어도 — 원거리 예비 모션은 0.2초뿐)
            if (Time.time - _lastGrant < t.attackStagger) return false;
            Holders.Add(enemy);
            _lastGrant = Time.time;
            return true;
        }

        /// <summary>공격이 끊겼다(경직·기절·죽음): 바로 돌려준다.</summary>
        public static void Release(EnemyController enemy)
        {
            Holders.Remove(enemy);
            HoldUntil.Remove(enemy);
        }

        /// <summary>
        /// 공격은 끝났지만 그 공격이 아직 날아가는 중 (도깨비불 불덩이): seconds 뒤에 돌려준다 — 날아오는 불덩이와 다른 몬스터의 베기가 같은 순간에 겹치지 않게.
        /// 0 이하면 바로 돌려준다.
        /// </summary>
        public static void ReleaseAfter(EnemyController enemy, float seconds)
        {
            if (seconds <= 0f || !Holders.Contains(enemy)) { Release(enemy); return; }
            HoldUntil[enemy] = Time.time + seconds;
        }
    }
}
