using System;
using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>방어 성공 한 번의 정보 (증강 구독용).</summary>
    public readonly struct DefenseSuccess
    {
        /// <summary>막은 캐릭터 — 강공격이면 곧 스왑되므로 스왑 전 캐릭터.</summary>
        public readonly CharacterKind Defender;
        public readonly bool Heavy;
        public readonly EnemyAttack Attack;
        /// <summary>방어 입력 직후 짧은 시간 안에 닿은 퍼펙트 성공 (연출 전용, 효과는 동일).</summary>
        public readonly bool Perfect;

        public DefenseSuccess(CharacterKind defender, bool heavy, EnemyAttack attack, bool perfect = false)
        {
            Defender = defender;
            Heavy = heavy;
            Attack = attack;
            Perfect = perfect;
        }
    }

    /// <summary>반격 한 번의 정보 (증강 구독용 — 예: 갈래 화살이 추가 화살을 쏜다).</summary>
    public readonly struct CounterShot
    {
        public readonly HitInfo Hit;
        public readonly Vector2 Origin;
        public readonly EnemyController Target;
        public readonly int Facing;

        public CounterShot(HitInfo hit, Vector2 origin, EnemyController target, int facing)
        {
            Hit = hit;
            Origin = origin;
            Target = target;
            Facing = facing;
        }
    }

    /// <summary>
    /// 방어 성공 처리(반격·도력·스왑)와 액티브 스킬 2개 (기획서 4.1~4.4).
    /// 스킬 내용은 프로토타입용 임시 구현. 데미지 계산은 전부 <see cref="DamageCalc"/> (기획서 5.3).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerParty), typeof(SkillGauge))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("도력 충전")]
        [SerializeField] private int normalSuccessGain = 1;
        [SerializeField] private int heavySuccessGain = 3;

        [Header("반격")]
        [SerializeField, Tooltip("기본 반격 데미지 — 캐릭터 공격력 배율(GameTuning)이 곱해짐")] private int counterDamage = 20;
        [SerializeField, Tooltip("플레이어 근접 반격 사거리 (플레이어 중심 기준)")] private float meleeCounterRange = 1.8f;
        [SerializeField] private float meleeCounterHeight = 1.6f;

        [Header("일반공격 (A) — 도력 없이 쿨타임만, 연타 방지용으로 약하게")]
        [SerializeField] private int attackSkillCost = 3;
        [SerializeField, Tooltip("켜면 A 공격 스킬이 도력을 쓰지 않는다 (쿨타임만 적용). 효율 계산용 기본 비용은 그대로 둔다")] private bool attackSkillFree = true;
        [SerializeField] private float attackSkillCooldown = 0.35f;
        [SerializeField, Tooltip("기본 일반공격(봉 1단) 데미지 — 캐릭터 공격력 배율(GameTuning)이 곱해짐. 평타만으로 너무 쉽게 잡혀 15 → 10")] private int attackSkillDamage = 10;
        [SerializeField] private Vector2 meleeSkillBox = new(2.4f, 1.6f);
        [Header("봉 일반공격 단계 (A를 이어 누르면 1 → 2 → 3단)")]
        [SerializeField, Tooltip("2단 / 3단 데미지 (1단은 위 일반공격 데미지). 1·2단은 몬스터를 경직시키지 않고 3단만 경직 + 기절")] private int comboDamage2 = 12;
        [SerializeField] private int comboDamage3 = 20;
        [SerializeField, Tooltip("2단 베기 박스 (앞쪽, 위로 올려 벰)")] private Vector2 comboBox2 = new(2.6f, 1.9f);
        [SerializeField, Tooltip("3단 마무리 박스 — 플레이어를 가운데로 양옆을 모두 휩쓴다")] private Vector2 comboBox3 = new(3.4f, 2.2f);
        [SerializeField, Tooltip("3단 마무리에 맞은 적의 기절 시간")] private float comboStun3 = 0.35f;
        [SerializeField, Tooltip("마무리(3단) 뒤 다음 공격까지 쿨타임 — 1·2단보다 길게 두어 박자를 만든다")] private float comboFinisherCooldown = 0.6f;
        [SerializeField, Tooltip("쿨타임이 끝나기 이 시간 안쪽에 A를 누르면 기억해 뒀다가 쿨타임이 끝나는 순간 바로 나간다 — 연속으로 칠 때 박자를 살짝 놓쳐도 끊기지 않게")] private float attackBuffer = 0.22f;
        [SerializeField, Tooltip("한 단을 치고 쿨타임이 끝난 뒤에도 이 시간 안에 다음 A를 누르면 다음 단으로 이어진다")] private float comboWindow = 0.6f;

        [Header("원거리 무기 (Q로 전환) — 일반공격과 반격이 화살로 바뀐다")]
        [SerializeField, Tooltip("원거리 일반공격 기본 데미지 — 경직이 약한 대신 빠르게 연사 (12 → 7)")] private int arrowDamage = 7;
        [SerializeField, Tooltip("원거리 일반공격 쿨타임 (0.45 → 0.3, 연사 속도를 올림)")] private float arrowCooldown = 0.3f;
        [SerializeField] private float arrowSpeed = 16f;
        [SerializeField] private float arrowRange = 12f;
        [SerializeField, Tooltip("원거리 반격 화살 속도 (일반 / 강공격) — 목표를 따라가는 유도")] private float counterArrowSpeedNormal = 8f;
        [SerializeField] private float counterArrowSpeedHeavy = 11f;
        [SerializeField] private float counterArrowMaxDistance = 25f;

        [Header("이동 스킬 (Shift) — 임시")]
        [SerializeField] private int moveSkillCost = 1;
        [SerializeField, Tooltip("대시를 연속으로 쓸 수 있는 횟수. 쓰면 1회분이 줄고, 쿨타임마다 1회분씩 다시 찬다 (1이면 예전처럼 한 번)")] private int moveSkillCharges = 2;
        [SerializeField, Tooltip("켜면 대시(Shift)가 도력을 쓰지 않는다 (쿨타임만 적용). 효율 계산용 기본 비용은 그대로 둔다")] private bool moveSkillFree = true;
        [SerializeField, Tooltip("기획서 4.4 — 0.1초에서 1초로 변경 (도력 1칸당 딜 효율 관리)")] private float moveSkillCooldown = 1f;

        [Header("스킬 D / F — 도력을 쓴다. 무기마다 내용이 다르다 (D 휩쓸기·부채꼴 / F 내려찍기·봉인진)")]
        [SerializeField] private int skill1Cost = 3;
        [SerializeField] private float skill1Cooldown = 3f;
        [SerializeField] private int skill2Cost = 5;
        [SerializeField] private float skill2Cooldown = 6f;
        [Header("봉 D: 휩쓸기 — 앞쪽 넓은 박스를 한 번에 쓸어 데미지 + 짧은 기절")]
        [SerializeField] private int sweepDamage = 30;
        [SerializeField] private Vector2 sweepBox = new(4f, 2.2f);
        [SerializeField] private float sweepStun = 0.5f;
        [Header("봉 F: 내려찍기 — 공중에서 내리꽂아 앞쪽 바닥 충격파, 큰 데미지 + 긴 기절")]
        [SerializeField] private int slamDamage = 45;
        [SerializeField] private Vector2 slamBox = new(3.8f, 1.6f);
        [SerializeField] private float slamStun = 1.2f;
        [SerializeField, Tooltip("모션 시작(공중 정점)에서 내리꽂는 순간까지 (판정-시각 일치)")] private float slamDelay = 0.083f;
        [Header("부적 D: 부채꼴 — 앞쪽으로 부적 여러 장을 부채꼴로 던진다")]
        [SerializeField] private int fanDamage = 10;
        [SerializeField] private int fanCount = 5;
        [SerializeField, Tooltip("부채꼴 전체 각도 (도)")] private float fanSpread = 48f;
        [SerializeField] private float fanSpeed = 14f;
        [SerializeField] private float fanRange = 9f;
        [Header("부적 F: 봉인진 — 앞쪽 바닥에 진을 깔아 안의 적을 묶고 약하게 태운다")]
        [SerializeField] private int sealTickDamage = 8;
        [SerializeField] private float sealTickInterval = 0.7f;
        [SerializeField] private float sealDuration = 3.5f;
        [SerializeField, Tooltip("플레이어 앞 진 가운데까지 거리")] private float sealDistance = 3.2f;
        [SerializeField] private Vector2 sealBox = new(3.2f, 1.6f);

        /// <summary>지금 들고 있는 무기 (Q로 전환).</summary>
        public WeaponMode Weapon { get; private set; } = WeaponMode.Melee;
        public bool IsRanged => Weapon == WeaponMode.Ranged;
        public float WeaponCooldownLeft => _weaponCd;
        public float WeaponCooldown => GameTuning.Current.weaponSwitchCooldown;
        /// <summary>무기를 바꿨을 때 (새 무기).</summary>
        public event Action<WeaponMode> WeaponChanged;
        public int AttackSkillCost => attackSkillCost;
        /// <summary>실제로 소모하는 도력 (공짜 설정이면 0).</summary>
        public int AttackSkillGaugeCost => attackSkillFree ? 0 : attackSkillCost;
        public int AttackSkillBaseDamage => IsRanged ? arrowDamage : attackSkillDamage;
        public int CounterBaseDamage => counterDamage;
        public float MeleeCounterRange => meleeCounterRange;
        public int MoveSkillCost => moveSkillCost;
        /// <summary>실제로 소모하는 도력 (공짜 설정이면 0).</summary>
        public int MoveSkillGaugeCost => moveSkillFree ? 0 : moveSkillCost;
        /// <summary>지금 무기의 일반공격 쿨타임 / 기본 데미지.</summary>
        public float AttackSkillCooldown => IsRanged ? arrowCooldown : attackSkillCooldown;
        public float AttackSkillCooldownLeft => _attackCd;
        /// <summary>봉 일반공격: 다음에 나갈 단 (1~3). 원거리 무기엔 단계가 없다.</summary>
        public int NextComboStage => Mathf.Clamp(_comboDone + 1, 1, 3);
        public float MoveSkillCooldown => moveSkillCooldown;
        public int Skill1Cost => skill1Cost;
        public int Skill2Cost => skill2Cost;
        public float Skill1Cooldown => skill1Cooldown;
        public float Skill2Cooldown => skill2Cooldown;
        /// <summary>지금 든 무기의 D / F 남은 쿨타임 (봉과 부적은 쿨타임을 따로 센다).</summary>
        public float Skill1CooldownLeft => _skillCd[WeaponIndex, 0];
        public float Skill2CooldownLeft => _skillCd[WeaponIndex, 1];
        /// <summary>무기를 지정해 D / F 남은 쿨타임 (테스트·UI용).</summary>
        public float SkillCooldownLeft(WeaponMode weapon, int slot) => _skillCd[weapon == WeaponMode.Ranged ? 1 : 0, slot == 1 ? 0 : 1];
        public string Skill1Name => IsRanged ? "부채꼴" : "휩쓸기";
        public string Skill2Name => IsRanged ? "봉인진" : "내려찍기";
        /// <summary>대시를 쓸 수 없을 때(충전 0) 다음 1회분이 찰 때까지 남은 시간. 충전이 남아 있으면 0.</summary>
        public float MoveSkillCooldownLeft => _moveCharges <= 0 ? _moveCd : 0f;
        public int MoveSkillCharges => _moveCharges;
        public int MoveSkillMaxCharges => Mathf.Max(1, moveSkillCharges);

        /// <summary>스킬 발동 / 발동 실패(도력 부족·쿨타임 등). UI 피드백용.</summary>
        public event Action<PlayerAction> ActionUsed;
        public event Action<PlayerAction> ActionDenied;
        /// <summary>방어 성공 — 도력 충전 직후, 스왑·반격 전에 호출된다.</summary>
        public event Action<DefenseSuccess> DefenseSucceeded;
        /// <summary>반격 발사 직후.</summary>
        public event Action<CounterShot> CounterFired;
        /// <summary>이동 스킬 발동 (쓴 캐릭터, 방향). 대시가 시작된 뒤 호출된다.</summary>
        public event Action<CharacterKind, int> MoveSkillUsed;

        private PlayerMotor _motor;
        private PlayerParty _party;
        private SkillGauge _gauge;
        private PlayerDefense _defense;
        private SuccessEffects _effects;
        private float _attackCd;
        private bool _attackBuffered;  // 쿨타임 막바지에 눌러 둔 A (끝나면 자동으로 나간다)
        private int _comboDone;        // 이어서 친 단 수 (0 = 처음부터, 다음은 +1단)
        private float _comboExpire;    // 이 시각이 지나면 다시 1단부터
        private float _moveCd;        // 다음 충전까지 남은 시간 (충전이 가득이면 0)
        private int _moveCharges = -1; // -1 = 아직 초기화 안 됨 (Awake에서 최대치로)
        private float _weaponCd;
        // D / F 쿨타임을 무기별로 따로 센다 [무기(봉 0 · 부적 1), 칸(D 0 · F 1)] — 봉 스킬을 쓰고 부적으로 바꾸면 부적 스킬은 바로 쓸 수 있다
        private readonly float[,] _skillCd = new float[2, 2];
        private int WeaponIndex => IsRanged ? 1 : 0;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _party = GetComponent<PlayerParty>();
            _gauge = GetComponent<SkillGauge>();
            _defense = GetComponent<PlayerDefense>();
            _effects = GetComponent<SuccessEffects>();
            if (_effects == null) _effects = gameObject.AddComponent<SuccessEffects>();
        }

        private void Update()
        {
            _attackCd = Mathf.Max(0f, _attackCd - Time.deltaTime);
            if (_attackBuffered && _attackCd <= 0f)
            {
                _attackBuffered = false; // 눌러 둔 공격: 쿨타임이 끝나는 순간 바로 (패링 중이거나 도력이 모자라면 TryAttackSkill이 막는다)
                if (!GameManager.InputBlocked && !_party.IsDead) TryAttackSkill();
            }
            if (_comboDone > 0 && Time.time > _comboExpire) _comboDone = 0; // 이어치기 시간이 끝나면 1단부터
            if (_moveCharges < 0 || _moveCharges > MoveSkillMaxCharges) _moveCharges = MoveSkillMaxCharges; // 최대 횟수를 바꾸면 맞춘다
            if (_moveCharges < MoveSkillMaxCharges)
            {
                _moveCd -= Time.deltaTime;
                if (_moveCd <= 0f)
                {
                    _moveCharges++;
                    _moveCd = _moveCharges < MoveSkillMaxCharges ? moveSkillCooldown : 0f; // 아직 덜 찼으면 다음 1회분 충전 시작
                }
            }
            _weaponCd = Mathf.Max(0f, _weaponCd - Time.deltaTime);
            for (int w = 0; w < 2; w++)
                for (int k = 0; k < 2; k++) _skillCd[w, k] = Mathf.Max(0f, _skillCd[w, k] - Time.deltaTime); // 들고 있지 않은 무기의 쿨타임도 흐른다

            if (GameManager.InputBlocked || _party.IsDead) return;
            if (Input.GetKeyDown(Controls.AttackSkill)) PressAttack();
            if (Input.GetKeyDown(Controls.Skill1)) TrySkill(1);
            if (Input.GetKeyDown(Controls.Skill2)) TrySkill(2);
            if (Input.GetKeyDown(Controls.MoveSkill)) TryMoveSkill();
            if (Input.GetKeyDown(Controls.Weapon)) TrySwitchWeapon();
        }

        // ───────────── 방어 성공 ─────────────

        public void OnDefenseSuccess(EnemyAttack attack, bool perfect = false)
        {
            bool heavy = attack.Type == AttackType.Heavy;
            Vector2 hitPoint = attack.transform.position;
            EnemyController source = attack.Source;
            // 공격이 날아온 방향 (소스가 이미 죽었으면 히트박스 진행 방향의 반대)
            float attackerX = source != null ? source.transform.position.x : transform.position.x - attack.Direction.x;
            string defenseName = _party.Current.defenseName;

            // 공격해 온 쪽을 먼저 바라본다 — 반격 방향 기준
            _motor.FaceTowards(attackerX);
            _gauge.Add((heavy ? heavySuccessGain : normalSuccessGain) + SkillTreeState.GaugeGainBonus);
            DefenseSucceeded?.Invoke(new DefenseSuccess(_party.Current.kind, heavy, attack, perfect));

            // 타격감: 강공격은 한 층 더 크게.
            PlayParryFeel(hitPoint, heavy, perfect);
            if (perfect) _effects.PlayPerfect(hitPoint);

            if (heavy)
            {
                Hud.Popup($"강공격 {defenseName}!", new Color(1f, 0.85f, 0.3f), true);
                Fx.Flash(transform.position, 1.5f, 4f, _party.Current.color, 0.2f);
            }
            else
            {
                Hud.Popup($"{defenseName}!", Color.white, false);
            }
            Counter(source, attackerX, heavy);
        }

        /// <summary>패링 = "막는다": 공격이 부서지고, 딱 멈추고(히트스탑), 금속성 "팅 + 쿵", 파편과 충격파.</summary>
        private void PlayParryFeel(Vector2 hitPoint, bool heavy, bool perfect)
        {
            Sfx.Play(perfect ? SfxId.PerfectParry : heavy ? SfxId.HeavyParry : SfxId.Parry);
            if (HitFeel.Instance != null) HitFeel.Instance.ParrySuccess(heavy);
            Fx.Sparks(hitPoint, heavy ? new Color(1f, 0.85f, 0.3f) : Color.white, heavy ? 22 : 12, heavy ? 12f : 8f);
            var art = GameAssets.AttackFx;
            if (art != null) if (IsRanged) FxClip.PlayAt(art.parryRanged, hitPoint, heavy ? 1.5f : 1.05f, false, 46); // 둥근 섬광·고리 대신 패링 그림이 성공 이펙트
                else FxClip.PlayFit(art.parryMelee, hitPoint, heavy ? new Vector2(4.4f, 1.9f) : new Vector2(3.2f, 1.35f), false, 46); // 봉: 패링 충격파
            // 성공 연출: 플레이어 몸 주변에 요기 흡수 (플레이어 뒤쪽 레이어)
            if (art != null) FxClip.PlayFit(art.yogiAbsorb, transform.position + Vector3.up * 0.55f, heavy ? new Vector2(1.7f, 3.2f) : new Vector2(1.4f, 2.7f), false, 8);
            if (heavy) Hud.ScreenFlash(new Color(1f, 1f, 1f, 0.35f));
        }

        private void Counter(EnemyController target, float attackerX, bool heavy)
        {
            _motor.FaceTowards(target != null ? target.transform.position.x : attackerX);
            int dir = _motor.Facing;
            Vector2 pos = transform.position;
            Color color = WeaponColor;
            var hit = NewHit(DamageSource.Counter, heavy);

            if (IsRanged)
            {
                // 원거리 반격: 목표를 따라가는 화살. 목표에 닿는 순간 데미지 (사거리 제한 없음 — 대신 근접보다 약하다)
                Vector2 toTarget = target != null ? (Vector2)target.transform.position - pos : new Vector2(dir, 0f);
                PlayerProjectile.Spawn(pos + new Vector2(dir * 0.4f, 0f), toTarget, target, true,
                    heavy ? counterArrowSpeedHeavy : counterArrowSpeedNormal, counterArrowMaxDistance,
                    counterDamage, hit, Color.Lerp(color, Color.white, 0.4f), heavy ? new Vector2(0.7f, 0.3f) : new Vector2(0.5f, 0.2f));
            }
            else
            {
                // 근접 반격: 사거리 1.8 박스. 보이는 박스 = 판정 박스. 밖에 있는 적은 못 때린다.
                var center = new Vector2(pos.x + dir * meleeCounterRange * 0.5f, pos.y);
                var boxColor = new Color(color.r, color.g, color.b, 0.6f);
                MeleeStrike.Spawn(center, new Vector2(meleeCounterRange, meleeCounterHeight), boxColor, counterDamage, hit,
                    art: GameAssets.AttackFx != null ? GameAssets.AttackFx.meleeCounter : null, facing: dir, follow: transform);
                // (패링 반격 때 앞으로 내딛던 Lunge는 없앴다 — 사용자 요청: 막은 자리에서 그대로 반격)
            }
            CounterFired?.Invoke(new CounterShot(hit, pos, target, dir));
        }

        // ───────────── 스킬 ─────────────

        /// <summary>A 키 입력. 쿨타임이 거의 끝났으면(attackBuffer 안쪽) 기억해 뒀다가 끝나는 순간 나가고, 아직 한참 남았으면 예전처럼 무시한다.</summary>
        public void PressAttack()
        {
            bool parrying = _defense.State == DefenseState.Active && !_defense.SucceededThisActive;
            if (_attackCd > 0f && _attackCd <= attackBuffer && !parrying)
            {
                _attackBuffered = true;
                return;
            }
            TryAttackSkill();
        }

        public bool TryAttackSkill()
        {
            // 패링을 막 켠 동안(아직 못 막음)은 공격 불가 — 하지만 한 번 막은 뒤에는 창이 남아 있어도 바로 공격(반격 이어치기)할 수 있다
            bool parrying = _defense.State == DefenseState.Active && !_defense.SucceededThisActive;
            if (parrying || _attackCd > 0f)
            {
                ActionDenied?.Invoke(PlayerAction.Attack);
                return false;
            }
            if (!_gauge.TrySpend(AttackSkillGaugeCost))
            {
                Denied(PlayerAction.Attack, $"도력 부족 ({attackSkillCost})");
                return false;
            }

            int stage = IsRanged ? 1 : NextComboStage;
            _attackCd = !IsRanged && stage == 3 ? comboFinisherCooldown : AttackSkillCooldown;
            if (!IsRanged)
            {
                // 1·2단을 치면 다음 단으로, 3단(마무리)을 치면 처음으로. 쿨타임 + 이어치기 시간 안에 못 누르면 1단부터
                _comboDone = stage >= 3 ? 0 : stage;
                _comboExpire = Time.time + _attackCd + comboWindow;
            }
            ActionUsed?.Invoke(PlayerAction.Attack);
            Sfx.Play(SfxId.Skill);
            int dir = _motor.Facing;
            Vector2 pos = transform.position;
            Color color = WeaponColor;
            var hit = NewHit(DamageSource.AttackSkill, false);
            // 평타는 몬스터 공격을 끊지 않는다(패링이 할 일): 부적 = 살짝 밀림만, 봉 1·2단 = 경직 없음, 봉 3단(마무리)만 경직 + 기절
            hit.Stagger = IsRanged ? HitStagger.Light : stage == 3 ? HitStagger.Full : HitStagger.None;

            if (IsRanged)
            {
                // 화살: 앞쪽으로 직진 (유도 없음)
                PlayerProjectile.Spawn(pos + new Vector2(dir * 0.4f, 0f), new Vector2(dir, 0f), null, false,
                    arrowSpeed, arrowRange, arrowDamage, hit,
                    Color.Lerp(color, Color.white, 0.2f), hit.Pierce ? new Vector2(1.1f, 0.22f) : new Vector2(0.8f, 0.18f));
            }
            else
            {
                var fx = GameAssets.AttackFx;
                Vector2 box = stage == 1 ? meleeSkillBox : stage == 2 ? comboBox2 : comboBox3;
                int damage = stage == 1 ? attackSkillDamage : stage == 2 ? comboDamage2 : comboDamage3;
                var art = fx == null ? null : stage == 1 ? fx.meleeAttack : stage == 2 ? fx.meleeAttack2 : fx.meleeAttack3;
                // 1·2단은 앞쪽 박스, 3단 마무리는 플레이어를 가운데로 양옆을 휩쓴다 (그림도 몸을 감싸는 회오리)
                var center = stage == 3 ? new Vector2(pos.x, pos.y) : new Vector2(pos.x + dir * box.x * 0.5f, pos.y);
                // 3단 기절은 일반 몬스터만 — 보스(슈퍼 아머)에겐 하던 공격이 끊겨 경직처럼 되므로 걸지 않는다 (너무 쉬워진다는 사용자 피드백)
                System.Action<EnemyController> onHit = stage == 3 ? e => { if (!e.SuperArmor) e.Stun(comboStun3); } : null;
                int hits = MeleeStrike.Spawn(center, box, new Color(color.r, color.g, color.b, 0.45f), damage, hit, onHit, art, dir, follow: transform);
                if (hits > 0 && HitFeel.Instance != null)
                {
                    if (stage == 3) HitFeel.Instance.Play(new HitFeelProfile { hitStop = 0.05f, slowMotion = 0f, slowScale = 1f, shake = 0.2f, shakeDuration = 0.15f });
                    else HitFeel.Instance.SkillHit();
                }
                // (평타 때 앞으로 내딛던 Lunge는 없앴다 — 사용자 요청: 제자리에서 벤다)
            }
            return true;
        }

        // ───────────── 스킬 D / F ─────────────

        /// <summary>D(1) / F(2) 스킬. 무기에 따라 봉(휩쓸기·내려찍기) / 부적(부채꼴·봉인진). 도력과 쿨타임이 있다.</summary>
        public bool TrySkill(int slot)
        {
            var action = slot == 1 ? PlayerAction.Skill1 : PlayerAction.Skill2;
            int cost = slot == 1 ? skill1Cost : skill2Cost;
            // 패링을 막 켠 동안(아직 못 막음)은 못 쓴다 — 막은 뒤에는 바로 쓸 수 있다 (일반공격과 같은 규칙)
            bool parrying = _defense.State == DefenseState.Active && !_defense.SucceededThisActive;
            if (_party.IsDead || parrying || _skillCd[WeaponIndex, slot == 1 ? 0 : 1] > 0f)
            {
                ActionDenied?.Invoke(action);
                return false;
            }
            if (!_gauge.TrySpend(cost))
            {
                Denied(action, $"도력 부족 ({cost})");
                return false;
            }

            _skillCd[WeaponIndex, slot == 1 ? 0 : 1] = slot == 1 ? skill1Cooldown : skill2Cooldown;
            ActionUsed?.Invoke(action);
            Sfx.Play(SfxId.Skill);
            int dir = _motor.Facing;
            Vector2 pos = transform.position;
            if (slot == 1)
            {
                if (IsRanged) FanSkill(pos, dir); else SweepSkill(pos, dir);
            }
            else
            {
                if (IsRanged) SealSkill(pos, dir); else StartCoroutine(SlamSkill(pos, dir));
            }
            return true;
        }

        private float GroundY => transform.position.y - transform.localScale.y * 0.5f;

        private void SweepSkill(Vector2 pos, int dir)
        {
            var center = new Vector2(pos.x + dir * sweepBox.x * 0.5f, pos.y);
            var hit = NewHit(DamageSource.AttackSkill, false);
            int hits = MeleeStrike.Spawn(center, sweepBox, new Color(WeaponColor.r, WeaponColor.g, WeaponColor.b, 0.45f), sweepDamage, hit,
                e => e.Stun(sweepStun), GameAssets.AttackFx != null ? GameAssets.AttackFx.meleeAttack : null, dir, stretchArt: true, follow: transform);
            if (hits > 0 && HitFeel.Instance != null) HitFeel.Instance.SkillHit();
            _motor.Lunge(dir);
        }

        private IEnumerator SlamSkill(Vector2 pos, int dir)
        {
            // 공중 정점에서 내리꽂는 순간까지 기다린 뒤 충격파 (모션 3번째 장면)
            if (slamDelay > 0f) yield return new WaitForSeconds(slamDelay);
            if (_party.IsDead) yield break;
            pos = transform.position;
            var center = new Vector2(pos.x + dir * (transform.localScale.x * 0.5f + slamBox.x * 0.5f), pos.y);
            var hit = NewHit(DamageSource.AttackSkill, true);
            int hits = MeleeStrike.Spawn(center, slamBox, new Color(0.6f, 1f, 0.95f, 0.45f), slamDamage, hit, e => e.Stun(slamStun));
            var art = GameAssets.AttackFx != null ? GameAssets.AttackFx.staffSlam : null;
            if (art != null && !art.IsEmpty)
            {
                // 땅 충격파: 판정 박스 폭에 맞춰 바닥에 붙여 재생 (그림 아래 끝 = 바닥)
                float scale = FxClip.FitScale(art, new Vector2(slamBox.x * 1.15f, 2.4f));
                float hgt = art.frames[0].bounds.size.y * scale;
                FxClip.PlayAt(art, new Vector2(center.x, GroundY + hgt * 0.5f - 0.05f), scale, dir < 0, 11);
            }
            Fx.Sparks(new Vector2(center.x, GroundY + 0.1f), new Color(0.6f, 1f, 0.95f), 14, 8f);
            if (HitFeel.Instance != null)
                HitFeel.Instance.Play(new HitFeelProfile { hitStop = hits > 0 ? 0.05f : 0f, slowMotion = 0f, slowScale = 1f, shake = 0.22f, shakeDuration = 0.18f });
        }

        private void FanSkill(Vector2 pos, int dir)
        {
            var hit = NewHit(DamageSource.AttackSkill, false);
            int n = Mathf.Max(1, fanCount);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1) - 0.5f);
                float angle = t * fanSpread * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(angle) * dir, Mathf.Sin(angle));
                PlayerProjectile.Spawn(pos + new Vector2(dir * 0.5f, 0.1f), d, null, false, fanSpeed, fanRange, fanDamage, hit,
                    Color.Lerp(WeaponColor, Color.white, 0.2f), new Vector2(0.8f, 0.18f));
            }
            _motor.Lunge(dir);
        }

        private void SealSkill(Vector2 pos, int dir)
        {
            var hit = NewHit(DamageSource.AttackSkill, false);
            var ground = new Vector2(pos.x + dir * sealDistance, GroundY);
            var fx = GameAssets.AttackFx;
            SealZone.Spawn(ground, sealBox, sealDuration, sealTickInterval, sealTickDamage, hit,
                fx != null ? fx.sealStart : null, fx != null ? fx.sealLoop : null, fx != null ? fx.sealEnd : null);
        }

        /// <summary>씬을 넘어 이어받은 무기를 조용히 맞춘다 (전환 쿨타임·교체 모션·효과 없음).</summary>
        public void SetWeapon(WeaponMode weapon) => Weapon = weapon;

        /// <summary>Q: 근접 ↔ 원거리. 방어 활성 중이거나 쿨타임이면 안 된다. 도력은 쓰지 않는다.</summary>
        public bool TrySwitchWeapon()
        {
            _comboDone = 0; // 무기를 바꾸면 단계는 처음부터
            _attackBuffered = false;
            if (_party.IsDead) return false;
            if (_weaponCd > 0f || _defense.State == DefenseState.Active)
            {
                ActionDenied?.Invoke(PlayerAction.Weapon);
                return false;
            }
            Weapon = IsRanged ? WeaponMode.Melee : WeaponMode.Ranged;
            _weaponCd = WeaponCooldown;
            Sfx.Play(SfxId.UiConfirm, 0.8f);
            Color c = WeaponColor;
            Fx.Ring(transform.position, c, 0.6f, 2.2f, 0.1f, 0.25f);
            Fx.Sparks(transform.position, c, 8, 6f, 0.14f, 0.3f);
            Hud.WorldText(transform.position + Vector3.up * 1.2f, IsRanged ? "원거리" : "근접", c, 0.95f);
            ActionUsed?.Invoke(PlayerAction.Weapon);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        /// <summary>무기를 나타내는 색 (스킬창·상태창): 근접 붉은색 / 원거리 초록색.</summary>
        public Color WeaponColor => IsRanged ? new Color(0.32f, 0.85f, 0.48f) : _party.Current.color;

        public bool TryMoveSkill()
        {
            if (_moveCharges < 0 || _moveCharges > MoveSkillMaxCharges) _moveCharges = MoveSkillMaxCharges;
            if (_moveCharges <= 0 || _motor.IsDashing)
            {
                ActionDenied?.Invoke(PlayerAction.Move);
                return false;
            }
            if (!_gauge.TrySpend(MoveSkillGaugeCost))
            {
                Denied(PlayerAction.Move, $"도력 부족 ({moveSkillCost})");
                return false;
            }

            // 충전 소모 — 가득 찬 상태에서 쓰기 시작하면 그때부터 충전 시간이 흐른다
            if (_moveCharges >= MoveSkillMaxCharges) _moveCd = moveSkillCooldown;
            _moveCharges--;
            var kind = _party.Current.kind;
            float h = _motor.HorizontalInput;
            int dir = h != 0f ? (h > 0f ? 1 : -1) : _motor.Facing;

            _motor.Dash(dir);
            Sfx.Play(SfxId.Dash);
            ActionUsed?.Invoke(PlayerAction.Move);
            MoveSkillUsed?.Invoke(kind, dir);
            return true;
        }

        /// <summary>지금 나와 있는 캐릭터의 공격 정보 .</summary>
        private HitInfo NewHit(DamageSource source, bool heavy)
        {
            var hit = HitInfo.Create(source, _party.Current.kind, transform.position, heavy);
            hit.Ranged = IsRanged;
            return hit;
        }

        private void Denied(PlayerAction action, string message)
        {
            ActionDenied?.Invoke(action);
            Sfx.Play(SfxId.Denied, 0.6f);
            Hud.WorldText(transform.position + Vector3.up, message, new Color(0.7f, 0.7f, 0.7f));
        }
    }
}
