namespace ParryRL
{
    /// <summary>
    /// 전사 · 피의 패링 — 전사가 패링에 성공하면 체력 +2 (강공격 +6), 중첩 2.
    /// 공유 체력에서 전사 = 회복 담당. 강공격은 막은 직후 궁수로 바뀌지만 막은 건 전사라 회복된다.
    /// </summary>
    public class BloodParryAugment : AugmentEffect
    {
        public const int NormalHeal = 2;
        public const int HeavyHeal = 6;

        private void OnEnable() => Combat.DefenseSucceeded += OnSuccess;
        private void OnDisable() => Combat.DefenseSucceeded -= OnSuccess;

        private void OnSuccess(DefenseSuccess s)
        {
            if (s.Defender != CharacterKind.Warrior) return;
            Party.Heal((s.Heavy ? HeavyHeal : NormalHeal) * Stacks);
        }
    }
}
