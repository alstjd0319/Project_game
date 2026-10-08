using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 궁수 · 등장: 화살비 — 궁수로 등장할 때 머리 위에서 화살 5발이 가까운 적들에게 차례로 쏟아진다
    /// (각 기본 6, 궁수 거리 보너스 적용). 적이 여럿이면 가까운 순서로 돌아가며 나눠 맞힌다 — "먼 적까지".
    /// 12유닛 안에 적이 없으면 바라보는 방향으로 날아간다.
    /// </summary>
    public class ArrowRainEntryAugment : AugmentEffect
    {
        public const int ArrowCount = 5;
        public const int BaseDamage = 6;
        public const float SearchRange = 12f;
        private const float Interval = 0.05f;
        private const float Speed = 16f;

        private static readonly Color ArrowColor = new(0.7f, 1f, 0.75f);

        private void OnEnable() => Party.SwapEntered += OnSwap;
        private void OnDisable() => Party.SwapEntered -= OnSwap;

        private void OnSwap(CharacterKind kind, SwapCause cause)
        {
            if (kind != CharacterKind.Archer) return;
            StartCoroutine(Rain());
        }

        private IEnumerator Rain()
        {
            Vector2 from = Pos;
            var targets = FindObjectsByType<EnemyController>()
                .Where(e => !e.IsDead && Mathf.Abs(e.transform.position.x - from.x) <= SearchRange)
                .OrderBy(e => Mathf.Abs(e.transform.position.x - from.x))
                .ToList();

            for (int i = 0; i < ArrowCount; i++)
            {
                EnemyController target = PickTarget(targets, i);
                Vector2 origin = (Vector2)transform.position + new Vector2((i - 2) * 0.35f, 2.2f);
                Vector2 dir = target != null ? (Vector2)target.transform.position - origin : new Vector2(Motor.Facing, -0.25f);
                var hit = HitInfo.Create(DamageSource.SwapEntry, CharacterKind.Archer, transform.position);
                PlayerProjectile.Spawn(origin, dir, target, true, Speed, SearchRange + 6f, BaseDamage, hit,
                    ArrowColor, new Vector2(0.45f, 0.14f));
                yield return new WaitForSeconds(Interval);
            }
        }

        /// <summary>가까운 순서로 돌아가며 나눈다. 앞선 화살에 죽은 적은 건너뛴다.</summary>
        private static EnemyController PickTarget(List<EnemyController> targets, int index)
        {
            targets.RemoveAll(e => e == null || e.IsDead);
            return targets.Count == 0 ? null : targets[index % targets.Count];
        }
    }
}
