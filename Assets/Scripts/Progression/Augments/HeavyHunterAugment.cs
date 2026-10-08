namespace ParryRL
{
    /// <summary>공용 · 강공격 사냥꾼 — 강공격 성공 시 게이지 +2칸 (기본 3 → 5). 강공격을 노리는 플레이.</summary>
    public class HeavyHunterAugment : AugmentEffect
    {
        public const int ExtraGauge = 2;

        private void OnEnable() => Combat.DefenseSucceeded += OnSuccess;
        private void OnDisable() => Combat.DefenseSucceeded -= OnSuccess;

        private void OnSuccess(DefenseSuccess s)
        {
            if (s.Heavy) Gauge.Add(ExtraGauge);
        }
    }
}
