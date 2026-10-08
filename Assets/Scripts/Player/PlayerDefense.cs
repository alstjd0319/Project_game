using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>판정 타이밍 기록 (튜닝용 표시).</summary>
    public enum TimingKind
    {
        InWindow,  // 활성 중에 닿음 → 성공
        Buffered,  // 활성이 끝난 직후 버퍼 안에 닿음 → 성공
        TooEarly,  // 헛스윙 쿨타임 중에 맞음 → 너무 일찍 누름
        TooLate,   // 맞은 직후에 누름 → 늦음
    }

    public readonly struct DefenseTiming
    {
        public readonly TimingKind Kind;
        /// <summary>InWindow: 누른 뒤 닿기까지 / Buffered: 활성 종료 뒤 닿기까지 / TooEarly: 누른 뒤 맞기까지 / TooLate: 맞은 뒤 누르기까지.</summary>
        public readonly float Seconds;

        public DefenseTiming(TimingKind kind, float seconds)
        {
            Kind = kind;
            Seconds = seconds;
        }

        /// <summary>입력 직후 짧은 시간 안에 닿은 성공 (연출 강화용).</summary>
        public bool IsPerfect(float perfectWindow) => Kind == TimingKind.InWindow && Seconds <= perfectWindow;

        public override string ToString() => Kind switch
        {
            TimingKind.InWindow => $"누르고 {Seconds:0.00}초 뒤 성공",
            TimingKind.Buffered => $"버퍼 성공 (활성 끝나고 {Seconds:0.00}초)",
            TimingKind.TooEarly => $"너무 일찍 ({Seconds:0.00}초 전에 누름)",
            _ => $"늦음 (맞고 {Seconds:0.00}초 뒤 누름)",
        };
    }

    /// <summary>
    /// 패링 판정 (기획서 8장 확정 알고리즘). 궁수(회피)는 폐기됐다.
    /// 1. S 입력 → 활성(가안 0.3초). 2. 활성 중 적 히트박스가 허트박스에 닿으면 즉시 성공 + 활성 종료.
    /// 3. 아무것도 안 닿고 활성이 끝나면 헛스윙 → 쿨타임. 활성이 아닐 때 닿으면 그냥 피격(쿨타임 없음).
    /// 입력 버퍼: 활성이 끝난 직후 짧은 시간 안에 닿아도 성공으로 친다 (살짝 이른 입력 보정).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerParty), typeof(PlayerCombat))]
    public class PlayerDefense : MonoBehaviour
    {
        private const float LateWindow = 0.3f; // 맞은 뒤 이 시간 안에 누르면 "늦음"으로 표시

        public DefenseState State { get; private set; } = DefenseState.Ready;
        public float Timer { get; private set; }
        /// <summary>방어 활성 시간 = 튜닝 기본값 + 증강 보정 (넓은 판정).</summary>
        public float ActiveTime => GameTuning.Current.defenseActiveTime + (_mods != null ? _mods.DefenseWindowBonus : 0f);
        public float WhiffCooldown => GameTuning.Current.whiffCooldown;
        /// <summary>이번 활성의 전체 길이 (활성 시간 + 입력 버퍼).</summary>
        public float ActiveDuration { get; private set; }

        /// <summary>방어 발동 / 발동 실패(쿨타임·이미 활성). UI 피드백용.</summary>
        public event Action Activated;
        public event Action ActivateDenied;
        public event Action<DefenseTiming> TimingReported;
        /// <summary>헛스윙 (아무것도 안 닿고 활성이 끝남).</summary>
        public event Action Whiffed;

        private PlayerMotor _motor;
        private PlayerParty _party;
        private PlayerCombat _combat;
        private RunModifiers _mods;
        private float _pressTime = float.NegativeInfinity;
        private float _lastHitTime = float.NegativeInfinity;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _party = GetComponent<PlayerParty>();
            _combat = GetComponent<PlayerCombat>();
            _mods = GetComponent<RunModifiers>();
        }

        private void Update()
        {
            switch (State)
            {
                case DefenseState.Active:
                    Timer -= Time.deltaTime;
                    if (Timer <= 0f) Whiff();
                    break;
                case DefenseState.Cooldown:
                    Timer -= Time.deltaTime;
                    if (Timer <= 0f) SetState(DefenseState.Ready, 0f);
                    break;
            }

            if (GameManager.InputBlocked) return;
            if (Input.GetKeyDown(Controls.Defend)) TryActivate();
        }

        /// <summary>방어 버튼 입력. 준비 상태일 때만 활성으로 전환된다.</summary>
        public bool TryActivate()
        {
            if (_party.IsDead) return false;

            float sinceHit = Time.time - _lastHitTime;
            if (sinceHit <= LateWindow) Report(new DefenseTiming(TimingKind.TooLate, sinceHit));

            if (State != DefenseState.Ready)
            {
                ActivateDenied?.Invoke();
                return false;
            }

            ActiveDuration = ActiveTime + GameTuning.Current.InputBufferOrZero;
            SetState(DefenseState.Active, ActiveDuration);
            _pressTime = Time.time;
            Sfx.Play(SfxId.DefendStart, 0.8f);
            Activated?.Invoke();
            return true;
        }

        private void SetState(DefenseState state, float timer)
        {
            State = state;
            Timer = timer;
            _motor.MovementLocked = state == DefenseState.Active;
        }

        private void Whiff()
        {
            SetState(DefenseState.Cooldown, WhiffCooldown);
            Sfx.Play(SfxId.Whiff);
            Hud.WorldText(transform.position + Vector3.up, "헛스윙", new Color(0.65f, 0.65f, 0.7f));
            Whiffed?.Invoke();
        }

        /// <summary>적 히트박스가 허트박스에 닿은 순간 호출된다 (EnemyAttack.OnTriggerEnter2D).</summary>
        public void ReceiveAttack(EnemyAttack attack)
        {
            if (_party.IsDead) return;

            if (State == DefenseState.Active)
            {
                float sincePress = Time.time - _pressTime;
                bool buffered = sincePress > ActiveTime;
                var timing = buffered
                    ? new DefenseTiming(TimingKind.Buffered, sincePress - ActiveTime)
                    : new DefenseTiming(TimingKind.InWindow, sincePress);
                Report(timing);

                // 방어 성공: 데미지 취소, 활성 즉시 종료 (헛스윙 쿨타임 없음)
                SetState(DefenseState.Ready, 0f);
                // 패링은 막아서 부순다
                attack.Resolve(AttackResolution.Shatter);
                _combat.OnDefenseSuccess(attack, timing.IsPerfect(GameTuning.Current.perfectWindow));
                return;
            }

            if (_motor.IsDashInvulnerable)
            {
                // 이동 스킬 무적: 조용히 통과 (대시 잔상이 이미 "빠져나갔다"를 보여줌)
                attack.Resolve(AttackResolution.PassThrough);
                return;
            }

            if (State == DefenseState.Cooldown) Report(new DefenseTiming(TimingKind.TooEarly, Time.time - _pressTime));
            _lastHitTime = Time.time;

            attack.Resolve(AttackResolution.Consumed);
            _party.TakeDamage(attack.Damage);
        }

        private void Report(DefenseTiming timing)
        {
            TimingReported?.Invoke(timing);
            if (!GameTuning.Current.showTiming) return;

            Color color = timing.Kind switch
            {
                TimingKind.InWindow => new Color(0.55f, 0.95f, 1f),
                TimingKind.Buffered => new Color(1f, 0.85f, 0.4f),
                _ => new Color(1f, 0.55f, 0.55f),
            };
            Hud.WorldText(transform.position + Vector3.up * 1.9f, timing.ToString(), color, 0.8f);
        }
    }
}
