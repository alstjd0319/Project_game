using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 공용 · 교대 공명 — 스왑 직후 3초간 반격 데미지 +30%. 스왑을 "위기 탈출"이 아니라 공격 타이밍으로.
    /// 강공격 방어 순간은 스왑 → 반격 순서라 그 반격부터 바로 적용된다. 시간은 반격을 "쏜" 시각 기준.
    /// </summary>
    public class SwapResonanceAugment : AugmentEffect
    {
        public const float Duration = 3f;
        public const float Bonus = 0.3f;

        private float _swapTime = float.NegativeInfinity;

        public bool IsActive => Time.time - _swapTime <= Duration;

        private void OnEnable() => Party.SwapEntered += OnSwap;
        private void OnDisable() => Party.SwapEntered -= OnSwap;

        private void OnSwap(CharacterKind kind, SwapCause cause)
        {
            _swapTime = Time.time;
            Say("교대 공명 3초", new Color(1f, 0.8f, 0.45f), 2.3f, 0.8f);
        }

        public override float DamageBonus(in HitInfo hit, EnemyController target)
        {
            if (hit.Source != DamageSource.Counter) return 0f;
            float since = hit.FiredTime - _swapTime;
            return since >= 0f && since <= Duration ? Bonus : 0f;
        }

        public override string BonusLabel => "공명";
    }
}
