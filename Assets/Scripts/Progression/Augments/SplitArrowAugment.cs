using System.Linq;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 궁수 · 갈래 화살 — 회피 반격 화살 +2발 (각 반격의 50%).
    /// 다른 적이 있으면 가까운 순서로 그 적을 노리고, 없으면 위쪽 부채꼴(12°, 24°)로 퍼진다 — 한 마리 상대로는 이득이 없고 다수전에서 빛난다.
    /// (아래쪽으로 퍼지면 화살이 바닥에 박혀 보여서 위로만)
    /// </summary>
    public class SplitArrowAugment : AugmentEffect
    {
        public const int ExtraArrows = 2;
        public const float DamageRatio = 0.5f;
        private const float FanStep = 12f;
        private const float Speed = 7f;

        private static readonly Color ArrowColor = new(0.75f, 1f, 0.8f);

        private void OnEnable() => Combat.CounterFired += OnCounter;
        private void OnDisable() => Combat.CounterFired -= OnCounter;

        private void OnCounter(CounterShot shot)
        {
            if (shot.Hit.Kind != CharacterKind.Archer) return;

            int damage = Mathf.Max(1, Mathf.RoundToInt(Combat.CounterBaseDamage * DamageRatio));
            var others = FindObjectsByType<EnemyController>()
                .Where(e => !e.IsDead && e != shot.Target)
                .OrderBy(e => Vector2.Distance(e.transform.position, shot.Origin))
                .ToList();

            Vector2 main = shot.Target != null ? (Vector2)shot.Target.transform.position - shot.Origin : new Vector2(shot.Facing, 0f);
            for (int i = 0; i < ExtraArrows; i++)
            {
                EnemyController target = i < others.Count ? others[i] : null;
                Vector2 dir = target != null
                    ? (Vector2)target.transform.position - shot.Origin
                    : (Vector2)(Quaternion.Euler(0f, 0f, FanStep * (i + 1) * Mathf.Sign(main.x == 0f ? shot.Facing : main.x)) * main);
                PlayerProjectile.Spawn(shot.Origin + new Vector2(shot.Facing * 0.4f, 0f), dir, target, target != null,
                    Speed, 25f, damage, shot.Hit, ArrowColor, new Vector2(0.4f, 0.16f));
            }
        }
    }
}
