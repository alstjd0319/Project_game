namespace ParryRL
{
    /// <summary>
    /// 궁수 · 관통 화살 — 공격 스킬 화살이 적을 관통하고, 뚫을 때마다 +20% (두 번째 적 +20%, 세 번째 +40% …).
    /// 3칸 30딜은 그대로, 줄지어 선 적을 여럿 맞힐 때만 효율이 오른다.
    /// </summary>
    public class PiercingArrowAugment : AugmentEffect
    {
        public const float BonusPerPierce = 0.2f;

        public override void PrepareHit(ref HitInfo hit)
        {
            if (hit.Source == DamageSource.AttackSkill && hit.Kind == CharacterKind.Archer) hit.Pierce = true;
        }

        public override float DamageBonus(in HitInfo hit, EnemyController target) =>
            hit.Pierce && hit.Source == DamageSource.AttackSkill ? BonusPerPierce * hit.PierceIndex : 0f;

        public override string BonusLabel => "관통";
    }
}
