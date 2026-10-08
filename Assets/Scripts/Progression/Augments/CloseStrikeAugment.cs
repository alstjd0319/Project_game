using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 플레이어 · 지척의 일격 — 바짝 붙은 적에게 반격하면 +50% (기획서 5.3에 따라 다른 증강 보너스와 더함).
    /// "바짝" = 플레이어 중심 ~ 적의 가까운 쪽 가장자리가 1.2 이내 (반격 박스 1.8 중 안쪽 2/3).
    /// 붙어서 패링할 이유.
    /// </summary>
    public class CloseStrikeAugment : AugmentEffect
    {
        public const float Reach = 1.2f;
        public const float Bonus = 0.5f;

        public override float DamageBonus(in HitInfo hit, EnemyController target)
        {
            if (hit.Source != DamageSource.Counter || hit.Kind != CharacterKind.Player || target == null) return 0f;
            float gap = Mathf.Abs(target.transform.position.x - hit.FiredFrom.x) - target.HalfWidth;
            return gap <= Reach ? Bonus : 0f;
        }

        public override string BonusLabel => "지척";
    }
}
