using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 전사 · 등장: 충격파 — 전사로 등장할 때 양옆 2.5유닛 안의 적에게 피해 15 + 0.8초 기절.
    /// 판정 = 보이는 박스 (플레이어 중심, 가로 5 × 세로 2.4).
    /// </summary>
    public class ShockwaveEntryAugment : AugmentEffect
    {
        public const int BaseDamage = 15;
        public const float Radius = 2.5f;
        public const float Height = 2.4f;
        public const float StunSeconds = 0.8f;

        private static readonly Color WaveColor = new(1f, 0.55f, 0.4f, 0.35f);

        private void OnEnable() => Party.SwapEntered += OnSwap;
        private void OnDisable() => Party.SwapEntered -= OnSwap;

        private void OnSwap(CharacterKind kind, SwapCause cause)
        {
            if (kind != CharacterKind.Warrior) return;

            var hit = HitInfo.Create(DamageSource.SwapEntry, CharacterKind.Warrior, Pos);
            int hits = MeleeStrike.Spawn(Pos, new Vector2(Radius * 2f, Height), WaveColor, BaseDamage, hit,
                enemy => enemy.Stun(StunSeconds));

            Sfx.Play(SfxId.HeavyParry, 0.5f);
            Fx.Ring(Pos, new Color(1f, 0.6f, 0.45f), 0.8f, Radius * 2.2f, 0.25f, 0.3f);
            if (hits > 0 && HitFeel.Instance != null) HitFeel.Instance.SkillHit();
        }
    }
}
