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
        [SerializeField] private float archerCounterSpeedNormal = 6f;
        [SerializeField] private float archerCounterSpeedHeavy = 8f;
        [SerializeField] private float archerCounterMaxDistance = 25f;

        [Header("일반공격 (A) — 게이지 없이 쿨타임만, 연타 방지용으로 약하게")]
        [SerializeField] private int attackSkillCost = 3;
        [SerializeField, Tooltip("켜면 A 공격 스킬이 게이지를 쓰지 않는다 (쿨타임만 적용). 효율 계산용 기본 비용은 그대로 둔다")] private bool attackSkillFree = true;
        [SerializeField] private float attackSkillCooldown = 0.35f;
        [SerializeField, Tooltip("기본 일반공격 데미지 — 캐릭터 공격력 배율(GameTuning)이 곱해짐")] private int attackSkillDamage = 15;
        [SerializeField] private Vector2 warriorSkillBox = new(2.4f, 1.6f);
        [SerializeField] private float archerArrowSpeed = 16f;
        [SerializeField] private float archerArrowRange = 12f;

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

            if (GameManager.InputBlocked || _party.IsDead || _motor.IsBackstepping) return;
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
            CharacterKind defender = _party.Current.kind;
            // 회피 후퇴 방향: 누르고 있는 방향키 쪽, 없으면 뒤쪽. 공격 쪽으로 몸을 돌리기 전에 읽는다.
            float held = _motor.HorizontalInput;
            int backDir = held != 0f ? (held > 0f ? 1 : -1) : -_motor.Facing;

            // 공격해 온 쪽을 먼저 바라본다 — 스왑 등장기(화살비 등)도 이 방향 기준
            _motor.FaceTowards(attackerX);
            _gauge.Add(heavy ? heavySuccessGain : normalSuccessGain);
            DefenseSucceeded?.Invoke(new DefenseSuccess(defender, heavy, attack, perfect));

            // 타격감: 막은 캐릭터 기준 (곧 스왑되므로 스왑 전에 판단). 강공격은 한 층 더 크게.
            if (defender == CharacterKind.Archer) PlayDodgeFeel(hitPoint, heavy, attack.Direction, perfect);
            else PlayParryFeel(hitPoint, heavy, perfect);

            // 막는 자세를 연출이 끝날 때까지 유지 (방어 활성은 성공 즉시 끝나므로 따로 붙잡는다)
            bool dodged = defender == CharacterKind.Archer;
            float poseSeconds = dodged ? _effects.DodgeDuration(perfect) : (perfect ? 0.55f : 0.25f);
            _party.HoldDefensePose(defender, poseSeconds);
            if (dodged)
            {
                _motor.Backstep(backDir, _effects.DodgeDistance(perfect), poseSeconds);
                _effects.PlayBackstepTrail(poseSeconds, backDir);
            }
            if (perfect) _effects.PlayPerfect(defender, hitPoint, attack.Direction);

            // 어떤 공격이든 방어에 성공하면 스왑 (Project_Game 방식): 스왑(→ 등장기 → 교대 공명 시작) →
            // 새로 등장한 캐릭터가 등장과 동시에 반격 "펑!" (기획서 5.2 처리 순서). 강공격은 연출과 반격이 한 층 더 크다.
            // (회피 후퇴가 끝나길 기다렸다 베는 안은 후퇴로 적과 멀어져 전사 근접 반격이 닿지 않아 쓰지 않는다)
            _party.Swap(heavy ? SwapCause.HeavyParry : SwapCause.Defense);
            if (heavy)
            {
                Hud.Popup($"강공격 {defenseName}!  →  {_party.Current.displayName} 등장", new Color(1f, 0.85f, 0.3f), true);
                Fx.Flash(transform.position, 1.5f, 4f, _party.Current.color, 0.2f);
            }
            else
            {
                Hud.Popup($"{defenseName}!  →  {_party.Current.displayName} 등장", Color.white, false);
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

        /// <summary>
        /// 회피 = "스친다": 공격이 몸을 통과해 지나가고(판정은 꺼짐), 시간이 느려지고(슬로우모션 위주),
        /// 바람 가르는 "휙", 몸이 잠깐 반투명 + 잔상 + 공격 방향으로 스치는 줄기. 파편·충격파 없음.
        /// (화이트박스 단계 연출 — 에셋을 입힐 때 다시 손본다)
        /// </summary>
        private void PlayDodgeFeel(Vector2 hitPoint, bool heavy, Vector2 attackDirection, bool perfect)
        {
            Sfx.Play(perfect ? SfxId.PerfectDodge : heavy ? SfxId.HeavyDodge : SfxId.Dodge);
            // 일반 회피는 시간을 멈추거나 늦추지 않는다 — 가볍게 물러나기만 한다 (퍼펙트만 시간 정지, SuccessEffects)
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(heavy ? 0.14f : 0.05f, heavy ? 0.2f : 0.1f);
            if (heavy) Hud.ScreenFlash(new Color(0.75f, 1f, 0.9f, 0.18f));
        }

        private void Counter(EnemyController target, float attackerX, bool heavy)
        {
            _motor.FaceTowards(target != null ? target.transform.position.x : attackerX);
            int dir = _motor.Facing;
            Vector2 pos = transform.position;
            Color color = _party.Current.color;
            var hit = NewHit(DamageSource.Counter, heavy);

            if (_party.Current.kind == CharacterKind.Warrior)
            {
                // 근접 반격: 사거리 1.8 박스. 보이는 박스 = 판정 박스. 밖에 있는 적은 못 때린다.
                var center = new Vector2(pos.x + dir * warriorCounterRange * 0.5f, pos.y);
                var boxColor = new Color(color.r, color.g, color.b, 0.6f);
                MeleeStrike.Spawn(center, new Vector2(warriorCounterRange, warriorCounterHeight), boxColor, counterDamage, hit,
                    art: GameAssets.AttackFx != null ? GameAssets.AttackFx.warriorCounter : null, facing: dir);
                _motor.Lunge(dir);
            }
            else
            {
                // 원거리 반격: 투사체가 목표에 도달하는 순간 데미지
                Vector2 toTarget = target != null ? (Vector2)target.transform.position - pos : new Vector2(dir, 0f);
                PlayerProjectile.Spawn(pos + new Vector2(dir * 0.4f, 0f), toTarget, target, true,
                    heavy ? archerCounterSpeedHeavy : archerCounterSpeedNormal, archerCounterMaxDistance,
                    counterDamage, hit, Color.Lerp(color, Color.white, 0.4f), heavy ? new Vector2(0.7f, 0.3f) : new Vector2(0.5f, 0.2f));
            }
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

            if (_party.Current.kind == CharacterKind.Warrior)
            {
                var center = new Vector2(pos.x + dir * warriorSkillBox.x * 0.5f, pos.y);
                int hits = MeleeStrike.Spawn(center, warriorSkillBox, new Color(color.r, color.g, color.b, 0.45f), attackSkillDamage, hit,
                    art: GameAssets.AttackFx != null ? GameAssets.AttackFx.warriorAttack : null, facing: dir);
                if (hits > 0 && HitFeel.Instance != null) HitFeel.Instance.SkillHit();
                _motor.Lunge(dir);
            }
            else
            {
                PlayerProjectile.Spawn(pos + new Vector2(dir * 0.4f, 0f), new Vector2(dir, 0f), null, false,
                    archerArrowSpeed, archerArrowRange, attackSkillDamage, hit,
                    Color.Lerp(color, Color.white, 0.2f), hit.Pierce ? new Vector2(1.1f, 0.22f) : new Vector2(0.8f, 0.18f));
            }
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
            int dir;
            if (h != 0f) dir = h > 0f ? 1 : -1;
            else dir = kind == CharacterKind.Warrior ? _motor.Facing : -_motor.Facing; // 궁수: 백스텝

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
