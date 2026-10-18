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
        [SerializeField, Tooltip("맞은 뒤 잠깐 무적 — 이 시간 동안은 또 맞지 않고(공격은 몸을 통과), 몸이 깜빡인다. 계속 맞는 느낌을 없앤다")] private float hurtInvulnerableTime = 1.0f;

        public CharacterProfile Current { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp => maxHp + BonusMaxHp;
        /// <summary>스킬 트리로 올린 최대 체력 (<see cref="SkillTreeState"/>).</summary>
        public int BonusMaxHp { get; private set; }
        public bool IsDead => Hp <= 0;

        public event Action<int> Damaged;

        private float _invulnerableUntil = float.NegativeInfinity;
        /// <summary>피격 직후 무적 중인가 (적 공격이 통과한다).</summary>
        public bool IsHurtInvulnerable => Time.time < _invulnerableUntil;
        public float HurtInvulnerableTime => hurtInvulnerableTime;

        /// <summary>무적 시간을 준다 (추락 뒤 되돌아왔을 때 등). 이미 더 길게 남았으면 그대로.</summary>
        public void GrantInvulnerability(float seconds) => _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        /// <summary>무적을 바로 끈다 (테스트용).</summary>
        public void ClearInvulnerability() => _invulnerableUntil = float.NegativeInfinity;

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
        /// <param name="ignoreInvulnerability">무적 중에도 받는다 (추락 같은 환경 피해)</param>
        /// <param name="ignoreReduction">받는 피해 감소 패시브를 적용하지 않는다 (환경 피해)</param>
        /// <summary>이 피해 이상이면 큰 피격음.</summary>
        public const int HeavyHurtDamage = 15;

        public int TakeDamage(int rawDamage, bool ignoreInvulnerability = false, bool ignoreReduction = false)
        {
            if (IsDead) return 0;
            if (IsHurtInvulnerable && !ignoreInvulnerability) return 0; // 방금 맞았다 — 잠깐 무적
            float reduction = ignoreReduction ? 0f : GameTuning.Current.DamageReduction(Current.kind);
            int damage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * (1f - reduction)));
            int blocked = rawDamage - damage;

            if (!GameTuning.Current.godMode) Hp = Mathf.Max(0, Hp - damage); // 연습 스위치: 피격 연출은 그대로
            _hurtFlash = 1f;
            GrantInvulnerability(hurtInvulnerableTime);

            Sfx.Play(damage >= HeavyHurtDamage || ignoreReduction ? SfxId.HurtHeavy : SfxId.Hurt); // 큰 피해·추락은 더 낮고 길게
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
            int healed = Mathf.Min(amount, MaxHp - Hp);
            Hp += healed;
            if (healed > 0) Hud.WorldText(transform.position + Vector3.up * 1.3f, $"+{healed}", new Color(0.45f, 1f, 0.55f), 0.95f);
            return healed;
        }

        /// <summary>연습용 (튜닝 패널).</summary>
        public void RestoreFullHp() => Hp = MaxHp;

        /// <summary>씬을 넘어 이어받은 체력을 되돌린다 (연출 없음). 1~최대 체력으로 맞춘다.</summary>
        public void SetHp(int hp) => Hp = Mathf.Clamp(hp, 1, MaxHp);

        /// <summary>스킬 트리 보너스를 반영한다. heal이면 늘어난 만큼 지금 체력도 올린다 (사당에서 찍을 때).</summary>
        public void SetMaxHpBonus(int bonus, bool heal)
        {
            int delta = bonus - BonusMaxHp;
            BonusMaxHp = bonus;
            Hp = Mathf.Clamp(heal && delta > 0 ? Hp + delta : Hp, 1, MaxHp);
        }

        /// <summary>도트 애니메이션(PlayerSprite)에 곱할 색 (흰색 = 원본). 피격·쿨타임 표시는 네모와 같다.</summary>
        public Color ArtTint { get; private set; } = Color.white;
        /// <summary>몸이 보이는 정도 (0 = 안 보임). 포탈로 사라지고 나타나는 연출이 쓴다 — 판정은 그대로.</summary>
        public float Visibility { get; set; } = 1f;

        private void UpdateVisual()
        {
            _sr.color = Tinted(Current.color);
            ArtTint = Tinted(Color.white);
            if (IsHurtInvulnerable)
            {
                // 무적 중엔 몸이 깜빡인다 (가려지지 않을 만큼만 — 최소 알파 0.4)
                float a = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(Time.time * 28f));
                var c = _sr.color; c.a = a; _sr.color = c;
                var t = ArtTint; t.a = a; ArtTint = t;
            }
            if (Visibility < 1f)
            {
                var c = _sr.color; c.a *= Visibility; _sr.color = c;
                var t = ArtTint; t.a *= Visibility; ArtTint = t;
            }
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
