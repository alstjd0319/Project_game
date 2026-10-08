using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 공용 · 연쇄 방어 — 헛스윙·피격 없이 방어 3연속 성공마다 게이지 +2칸 (끊기면 처음부터).
    /// 패턴을 정확히 읽을수록 보상 → 핵심 가치와 직결.
    /// </summary>
    public class ChainDefenseAugment : AugmentEffect
    {
        public const int ChainLength = 3;
        public const int GaugeReward = 2;

        private static readonly Color ChainColor = new(0.55f, 0.95f, 1f);

        public int Count { get; private set; }

        private void OnEnable()
        {
            Combat.DefenseSucceeded += OnSuccess;
            Defense.Whiffed += Break;
            Party.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            Combat.DefenseSucceeded -= OnSuccess;
            Defense.Whiffed -= Break;
            Party.Damaged -= OnDamaged;
        }

        private void OnSuccess(DefenseSuccess s)
        {
            Count++;
            if (Count < ChainLength)
            {
                Say($"연쇄 {Count}/{ChainLength}", ChainColor, 2.3f, 0.7f);
                return;
            }

            Count = 0;
            Gauge.Add(GaugeReward);
            Say($"연쇄 방어!  게이지 +{GaugeReward}", ChainColor, 2.3f, 0.95f);
            Fx.Ring(Pos, ChainColor, 0.5f, 2.5f, 0.1f, 0.3f);
        }

        private void OnDamaged(int _) => Break();

        private void Break()
        {
            if (Count > 0) Say("연쇄 끊김", new Color(0.6f, 0.6f, 0.65f), 2.3f, 0.7f);
            Count = 0;
        }
    }
}
