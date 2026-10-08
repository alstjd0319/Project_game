using System;
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
    /// 방어 성공 처리(반격·게이지·스왑)와 액티브 스킬 2개 (기획서 4.1~4.4).
    /// 스킬 내용은 프로토타입용 임시 구현. 데미지 계산은 전부 <see cref="DamageCalc"/> (기획서 5.3).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerParty), typeof(SkillGauge))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("게이지 충전")]
        [SerializeField] private int normalSuccessGain = 1;
        [SerializeField] private int heavySuccessGain = 3;

        [Header("반격")]
        [SerializeField, Tooltip("기본 반격 데미지 — 캐릭터 공격력 배율(GameTuning)이 곱해짐")] private int counterDamage = 20;
        [SerializeField, Tooltip("전사 근접 반격 사거리 (플레이어 중심 기준)")] private float warriorCounterRange = 1.8f;
        [SerializeField] private float warriorCounterHeight = 1.6f;

        [Header("일반공격 (A) — 게이지 없이 쿨타임만, 연타 방지용으로 약하게")]
        [SerializeField] private int attackSkillCost = 3;
        [SerializeField, Tooltip("켜면 A 공격 스킬이 게이지를 쓰지 않는다 (쿨타임만 적용). 효율 계산용 기본 비용은 그대로 둔다")] private bool attackSkillFree = true;
        [SerializeField] private float attackSkillCooldown = 0.35f;
        [SerializeField, Tooltip("기본 일반공격 데미지 — 캐릭터 공격력 배율(GameTuning)이 곱해짐")] private int attackSkillDamage = 15;
        [SerializeField] private Vector2 warriorSkillBox = new(2.4f, 1.6f);

        [Header("이동 스킬 (Shift) — 임시")]
        [SerializeField] private int moveSkillCost = 1;
        [SerializeField, Tooltip("기획서 4.4 — 0.1초에서 1초로 변경 (게이지 1칸당 딜 효율 관리)")] private float moveSkillCooldown = 1f;

        public int AttackSkillCost => attackSkillCost;
        /// <summary>실제로 소모하는 게이지 (공짜 설정이면 0).</summary>
        public int AttackSkillGaugeCost => attackSkillFree ? 0 : attackSkillCost;
        public int AttackSkillBaseDamage => attackSkillDamage;
        public int CounterBaseDamage => counterDamage;
        public float WarriorCounterRange => warriorCounterRange;
        public int MoveSkillCost => moveSkillCost;
        public float AttackSkillCooldown => attackSkillCooldown;
        public float AttackSkillCooldownLeft => _attackCd;
        public float MoveSkillCooldown => moveSkillCooldown;
        public float MoveSkillCooldownLeft => _moveCd;

        /// <summary>스킬 발동 / 발동 실패(게이지 부족·쿨타임 등). UI 피드백용.</summary>
        public event Action<PlayerAction> ActionUsed;
        public event Action<PlayerAction> ActionDenied;
        /// <summary>방어 성공 — 게이지 충전 직후, 스왑·반격 전에 호출된다.</summary>
        public event Action<DefenseSuccess> DefenseSucceeded;
        /// <summary>반격 발사 직후.</summary>
        public event Action<CounterShot> CounterFired;
        /// <summary>이동 스킬 발동 (쓴 캐릭터, 방향). 대시가 시작된 뒤 호출된다.</summary>
        public event Action<CharacterKind, int> MoveSkillUsed;

        private PlayerMotor _motor;
        private PlayerParty _party;
        private SkillGauge _gauge;
        private PlayerDefense _defense;
        private RunModifiers _mods;
        private SuccessEffects _effects;
        private float _attackCd;
        private float _moveCd;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _party = GetComponent<PlayerParty>();
            _gauge = GetComponent<SkillGauge>();
            _defense = GetComponent<PlayerDefense>();
            _mods = GetComponent<RunModifiers>();
            _effects = GetComponent<SuccessEffects>();
            if (_effects == null) _effects = gameObject.AddComponent<SuccessEffects>();
        }

        private void Update()
        {
            _attackCd = Mathf.Max(0f, _attackCd - Time.deltaTime);
            _moveCd = Mathf.Max(0f, _moveCd - Time.deltaTime);

            if (GameManager.InputBlocked || _party.IsDead) return;
            if (Input.GetKeyDown(Controls.AttackSkill)) TryAttackSkill();
            if (Input.GetKeyDown(Controls.MoveSkill)) TryMoveSkill();
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
            _gauge.Add(heavy ? heavySuccessGain : normalSuccessGain);
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
            Fx.Flash(hitPoint, heavy ? 1.2f : 0.7f, heavy ? 3.2f : 1.8f, Color.white, heavy ? 0.16f : 0.1f);
            Fx.Ring(hitPoint, Color.white, 0.4f, heavy ? 3.5f : 2f, heavy ? 0.16f : 0.1f, heavy ? 0.3f : 0.2f);
            Fx.Sparks(hitPoint, heavy ? new Color(1f, 0.85f, 0.3f) : Color.white, heavy ? 22 : 12, heavy ? 12f : 8f);
            if (heavy) Hud.ScreenFlash(new Color(1f, 1f, 1f, 0.35f));
        }

        private void Counter(EnemyController target, float attackerX, bool heavy)
        {
            _motor.FaceTowards(target != null ? target.transform.position.x : attackerX);
            int dir = _motor.Facing;
            Vector2 pos = transform.position;
            Color color = _party.Current.color;
            var hit = NewHit(DamageSource.Counter, heavy);

            // 근접 반격: 사거리 1.8 박스. 보이는 박스 = 판정 박스. 밖에 있는 적은 못 때린다.
            var center = new Vector2(pos.x + dir * warriorCounterRange * 0.5f, pos.y);
            var boxColor = new Color(color.r, color.g, color.b, 0.6f);
            MeleeStrike.Spawn(center, new Vector2(warriorCounterRange, warriorCounterHeight), boxColor, counterDamage, hit,
                art: GameAssets.AttackFx != null ? GameAssets.AttackFx.warriorCounter : null, facing: dir);
            _motor.Lunge(dir);
            CounterFired?.Invoke(new CounterShot(hit, pos, target, dir));
        }

        // ───────────── 스킬 ─────────────

        public bool TryAttackSkill()
        {
            if (_defense.State == DefenseState.Active || _attackCd > 0f)
            {
                ActionDenied?.Invoke(PlayerAction.Attack);
                return false;
            }
            if (!_gauge.TrySpend(AttackSkillGaugeCost))
            {
                Denied(PlayerAction.Attack, $"게이지 부족 ({attackSkillCost})");
                return false;
            }

            _attackCd = attackSkillCooldown;
            ActionUsed?.Invoke(PlayerAction.Attack);
            Sfx.Play(SfxId.Skill);
            int dir = _motor.Facing;
            Vector2 pos = transform.position;
            Color color = _party.Current.color;
            var hit = NewHit(DamageSource.AttackSkill, false);

            var center = new Vector2(pos.x + dir * warriorSkillBox.x * 0.5f, pos.y);
            int hits = MeleeStrike.Spawn(center, warriorSkillBox, new Color(color.r, color.g, color.b, 0.45f), attackSkillDamage, hit,
                art: GameAssets.AttackFx != null ? GameAssets.AttackFx.warriorAttack : null, facing: dir);
            if (hits > 0 && HitFeel.Instance != null) HitFeel.Instance.SkillHit();
            _motor.Lunge(dir);
            return true;
        }

        public bool TryMoveSkill()
        {
            if (_moveCd > 0f || _motor.IsDashing)
            {
                ActionDenied?.Invoke(PlayerAction.Move);
                return false;
            }
            if (!_gauge.TrySpend(moveSkillCost))
            {
                Denied(PlayerAction.Move, $"게이지 부족 ({moveSkillCost})");
                return false;
            }

            _moveCd = moveSkillCooldown;
            var kind = _party.Current.kind;
            float h = _motor.HorizontalInput;
            int dir = h != 0f ? (h > 0f ? 1 : -1) : _motor.Facing;

            _motor.Dash(dir, _mods != null ? _mods.DashDistanceBonus(kind) : 0f);
            Sfx.Play(SfxId.Dash);
            ActionUsed?.Invoke(PlayerAction.Move);
            MoveSkillUsed?.Invoke(kind, dir);
            return true;
        }

        /// <summary>지금 나와 있는 캐릭터의 공격 정보 (증강이 발동 순간 성질을 바꿀 수 있음).</summary>
        private HitInfo NewHit(DamageSource source, bool heavy)
        {
            var hit = HitInfo.Create(source, _party.Current.kind, transform.position, heavy);
            if (_mods != null) _mods.PrepareHit(ref hit);
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
