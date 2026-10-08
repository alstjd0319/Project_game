using System;
using UnityEngine;

namespace ParryRL
{
    [Serializable]
    public class CharacterProfile
    {
        public CharacterKind kind;
        public string displayName;
        [Tooltip("방어 기믹 명칭 (판정 로직은 동일)")] public string defenseName;
        public Color color;
    }

    /// <summary>
    /// 2인 파티: 현재 캐릭터, 스왑(강제/수동), 스왑 무적, 공유 체력 (기획서 4.3).
    /// 두 캐릭터는 같은 허트박스(같은 오브젝트)를 쓰고, 색과 전투 방식만 바뀐다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerParty : MonoBehaviour
    {
        [SerializeField] private CharacterProfile warrior = new()
            { kind = CharacterKind.Warrior, displayName = "전사", defenseName = "패링", color = new Color(0.95f, 0.38f, 0.32f) };
        [SerializeField] private CharacterProfile archer = new()
            { kind = CharacterKind.Archer, displayName = "궁수", defenseName = "회피", color = new Color(0.32f, 0.85f, 0.48f) };

        [Header("도트 아트 (비워두면 흰 네모) — 원본은 왼쪽을 본다. 판정-시각 일치를 위해 몸 크기(1×1 × 스케일)에 맞춰 늘려 그린다")]
        [SerializeField] private Sprite warriorSprite;
        [SerializeField] private Sprite warriorParrySprite;
        [SerializeField] private Sprite archerSprite;
        [SerializeField] private Sprite archerDodgeSprite;

        [Header("체력 (두 캐릭터 공유)")]
        [SerializeField] private int maxHp = 100;

        [Header("스왑")]
        [SerializeField] private float swapInvulnerableTime = 0.3f;
        [SerializeField] private int manualSwapCost = 10;

        public CharacterProfile Current { get; private set; }
        public CharacterProfile Other => Current == warrior ? archer : warrior;
        public int Hp { get; private set; }
        public int MaxHp => maxHp;
        public int ManualSwapCost => manualSwapCost;
        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public bool IsDead => Hp <= 0;
        /// <summary>마지막 스왑 시각 (Time.time).</summary>
        public float LastSwapTime { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// 스왑 진입 효과(등장기) 훅. 실제 효과는 증강 설계 후 여기에 붙인다 (기획서 4.3.1 / 5장).
        /// </summary>
        public event Action<CharacterKind, SwapCause> SwapEntered;
        public event Action<int> Damaged;
        /// <summary>수동 스왑 실패(게이지 부족·방어 활성 중). UI 피드백용.</summary>
        public event Action SwapDenied;

        private SpriteRenderer _sr;
        private PlayerMotor _motor;
        private bool _hasArt;
        private CharacterKind _poseKind;
        private float _poseTimer;
        private PlayerDefense _defense;
        private SkillGauge _gauge;
        private float _invulnerableTimer;
        private float _hurtFlash;
        private float _swapFlash;
        private float _dodgeGhost;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _motor = GetComponent<PlayerMotor>();
            _defense = GetComponent<PlayerDefense>();
            _gauge = GetComponent<SkillGauge>();
            Current = warrior;
            Hp = maxHp;

            _hasArt = warriorSprite != null && archerSprite != null;
            if (_hasArt)
            {
                _sr.drawMode = SpriteDrawMode.Sliced;
                _sr.size = Vector2.one;
                // 바라보는 방향 표시용 눈은 도트 아트가 대신한다
                foreach (var child in GetComponentsInChildren<SpriteRenderer>())
                    if (child != _sr && child.name != "Art") child.enabled = false; // 도트 애니메이션(PlayerSprite)의 Art는 제외
            }
        }

        private void Update()
        {
            if (_invulnerableTimer > 0f) _invulnerableTimer -= Time.deltaTime;
            _hurtFlash = Mathf.Max(0f, _hurtFlash - Time.unscaledDeltaTime * 5f);
            _swapFlash = Mathf.Max(0f, _swapFlash - Time.unscaledDeltaTime * 6f);
            _dodgeGhost = Mathf.Max(0f, _dodgeGhost - Time.unscaledDeltaTime);
            _poseTimer = Mathf.Max(0f, _poseTimer - Time.unscaledDeltaTime);

            if (!GameManager.InputBlocked && Input.GetKeyDown(Controls.Swap)) TryManualSwap();
        }

        private void LateUpdate() => UpdateVisual();

        /// <summary>수동 스왑 (게이지 10칸). 방어 활성 중에는 불가.</summary>
        public bool TryManualSwap()
        {
            if (IsDead) return false;
            if (_defense != null && _defense.State == DefenseState.Active)
            {
                SwapDenied?.Invoke();
                return false;
            }
            if (_gauge.TrySpend(manualSwapCost))
            {
                Swap(SwapCause.Manual);
                return true;
            }
            SwapDenied?.Invoke();
            Sfx.Play(SfxId.Denied, 0.6f);
            Hud.WorldText(transform.position + Vector3.up, $"게이지 부족 ({manualSwapCost})", new Color(0.7f, 0.7f, 0.7f));
            return false;
        }

        public void Swap(SwapCause cause)
        {
            Current = Other;
            LastSwapTime = Time.time;
            _invulnerableTimer = swapInvulnerableTime;
            _swapFlash = 1f;

            // 등장 연출 (공통). 증강별 등장기는 SwapEntered 구독 쪽에서 추가.
            Vector2 pos = transform.position;
            bool heavy = cause == SwapCause.HeavyParry;
            Sfx.Play(SfxId.Swap);
            Fx.Ring(pos, Current.color, 0.6f, heavy ? 4.5f : 3f, heavy ? 0.22f : 0.14f, heavy ? 0.4f : 0.3f);
            Fx.Sparks(pos, Current.color, heavy ? 18 : 10, heavy ? 10f : 7f, 0.16f, 0.4f);
            if (cause == SwapCause.Manual) Hud.Popup($"수동 스왑 → {Current.displayName}", Current.color, false);

            SwapEntered?.Invoke(Current.kind, cause);
        }

        /// <summary>
        /// 피격. 맞는 순간 나와 있는 캐릭터의 방어 패시브로 줄인다 (전사: 가안 30% 감소, 최소 1).
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

        /// <summary>방어 성공 직후 막는 자세(방패/웅크린 구르기)를 실시간 초 동안 유지. 도트 아트가 있을 때만 보인다.</summary>
        public void HoldDefensePose(CharacterKind kind, float seconds)
        {
            _poseKind = kind;
            _poseTimer = Mathf.Max(_poseTimer, seconds);
        }

        /// <summary>회피 성공 연출: 잠깐 반투명해져 공격이 몸을 스쳐 지나간 느낌 (실시간 초). 판정·무적과는 무관한 연출.</summary>
        public void PlayDodgeGhost(float seconds) => _dodgeGhost = Mathf.Max(_dodgeGhost, seconds);

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

        private void UpdateSprite()
        {
            bool active = _defense != null && _defense.State == DefenseState.Active;
            CharacterKind kind = Current.kind;
            bool pose = active;
            if (_poseTimer > 0f)
            {
                kind = _poseKind;
                pose = true;
            }
            bool warriorLook = kind == CharacterKind.Warrior;
            Sprite normal = warriorLook ? warriorSprite : archerSprite;
            Sprite action = warriorLook ? warriorParrySprite : archerDodgeSprite;
            _sr.sprite = pose && action != null ? action : normal;
            if (_motor != null) _sr.flipX = _motor.Facing > 0;
        }

        /// <summary>도트 애니메이션(PlayerSprite)에 곱할 색 (흰색 = 원본). 피격·무적·쿨타임 표시는 네모와 같다.</summary>
        public Color ArtTint { get; private set; } = Color.white;

        private void UpdateVisual()
        {
            if (_hasArt) UpdateSprite();
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
            c = Color.Lerp(c, Color.white, _swapFlash);
            c = Color.Lerp(c, new Color(1f, 0.15f, 0.15f), _hurtFlash);
            if (_dodgeGhost > 0f)
            {
                // 회피: 깜빡임이 아니라 부드러운 반투명 (스왑 무적의 깜빡임과 구분)
                c = Color.Lerp(c, Color.white, 0.25f);
                c.a = 0.4f;
            }
            else if (IsInvulnerable) c.a = Mathf.PingPong(Time.unscaledTime * 20f, 1f) > 0.5f ? 1f : 0.35f;
            return c;
        }
    }
}
