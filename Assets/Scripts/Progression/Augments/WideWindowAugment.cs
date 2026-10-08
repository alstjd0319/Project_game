namespace ParryRL
{
    /// <summary>공용 · 넓은 판정 — 방어 활성 시간 +0.05초 (중첩 3). 숙련도 보조의 기본 성장.</summary>
    public class WideWindowAugment : AugmentEffect
    {
        public const float BonusPerStack = 0.05f;

        public override float DefenseWindowBonus => BonusPerStack * Stacks;
    }
}
