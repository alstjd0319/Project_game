using System;
using UnityEngine;

namespace ParryRL
{
    [Serializable]
    public class CharacterProfile
    {
        public CharacterKind kind;
        public string displayName;
        [Tooltip("방어 기믹 명칭")] public string defenseName;
        public Color color;
    }

    /// <summary>
    /// 플레이어 캐릭터(플레이어) 한 명의 체력·피격 표시. 궁수와 스왑 시스템은 폐기됐다 (2026-10-08).
    /// 이름은 PlayerParty로 남겨 다른 코드의 참조를 유지한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerParty : MonoBehaviour
    {
        [SerializeField] private CharacterProfile player = new()
            { kind = CharacterKind.Player, displayName = "플레이어", defenseName = "패링", color = new Color(0.95f, 0.38f, 0.32f) };

        [Header("체력")]
        [SerializeField] private int maxHp = 100;

        public CharacterProfile Current { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp => maxHp;
        public bool IsDead => Hp <= 0;

        public event Action<int> Damaged;

        private SpriteRenderer _sr;
        private PlayerDefense _defense;
        private float _hurtFlash;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _defense = GetComponent<PlayerDefense>();
            Current = player;
            Hp = maxHp;
        }

        private void Update() => _hurtFlash = Mathf.Max(0f, _hurtFlash - Time.unscaledDeltaTime * 5f);

        private void LateUpdate() => UpdateVisual();

        /// <summary>
        /// 피격. 플레이어 방어 패시브로 줄인다 (가안 30% 감소, 최소 1).
        /// </summary>
        /// <returns>실제로 받은 데미지</returns>
        public int TakeDamage(int rawDamage)
        {
            if (IsDead) return 0;
            float reduction = GameTuning.Current.DamageReduction(Current.kind);
            int damage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * (1f - reduction)));
            int blocked = rawDamage - damage;

            if (!GameTuning.Current.godMode) Hp = Mathf.Max(0, Hp - damage); // 연습 스위치: 피격 연출은 그대로
            _hurtFlash = 1f;

            Sfx.Play(SfxId.Hurt);
            if (HitFeel.Instance != null) HitFeel.Instance.PlayerHurt();
            Fx.Sparks(transform.position, new Color(1f, 0.25f, 0.25f), 8, 5f);
            Hud.ScreenFlash(new Color(1f, 0f, 0f, 0.25f));
            string text = blocked > 0 ? $"-{damage} <size=20><color=#AFC4FF>(방어 {blocked})</color></size>" : $"-{damage}";
            Hud.WorldText(transform.position + Vector3.up, text, new Color(1f, 0.35f, 0.35f), 1.1f);
            Damaged?.Invoke(damage);

            if (IsDead && GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
            return damage;
        }

        /// <returns>실제로 회복한 양 (최대 체력 초과분 제외)</returns>
        public int Heal(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int healed = Mathf.Min(amount, maxHp - Hp);
            Hp += healed;
            if (healed > 0) Hud.WorldText(transform.position + Vector3.up * 1.3f, $"+{healed}", new Color(0.45f, 1f, 0.55f), 0.95f);
            return healed;
        }

        /// <summary>연습용 (튜닝 패널).</summary>
        public void RestoreFullHp() => Hp = maxHp;

        /// <summary>도트 애니메이션(PlayerSprite)에 곱할 색 (흰색 = 원본). 피격·쿨타임 표시는 네모와 같다.</summary>
        public Color ArtTint { get; private set; } = Color.white;

        private void UpdateVisual()
        {
            _sr.color = Tinted(Current.color);
            ArtTint = Tinted(Color.white);
        }

        private Color Tinted(Color c)
        {
            if (_defense != null)
            {
                if (_defense.State == DefenseState.Active) c = Color.Lerp(c, Color.white, 0.7f);
                else if (_defense.State == DefenseState.Cooldown) c = Color.Lerp(c, new Color(0.35f, 0.35f, 0.4f), 0.6f);
            }
            return Color.Lerp(c, new Color(1f, 0.15f, 0.15f), _hurtFlash);
        }
    }
}
