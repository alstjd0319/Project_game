using System;
using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 테스트용 몬스터. 근접: 바짝 다가와 몸 앞에서 베기 박스를 뻗는다 / 원거리: 거리를 유지하며 투사체를 쏜다.
    /// 공격 전 예비 모션(색 + 크기)으로 일반/강공격을 구분 (기획서 7장).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class EnemyController : MonoBehaviour
    {
        private enum State { Idle, Windup, Recover, Stagger, Dead }

        [Header("기본")]
        [SerializeField] private EnemyKind kind = EnemyKind.Melee;
        [SerializeField] private int maxHp = 100;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float staggerTime = 0.15f;
        [SerializeField] private float knockback = 0.25f;
        [SerializeField, Tooltip("처치 시 경험치")] private int xpReward = 10;
        [SerializeField, Tooltip("약한 경직(부적 평타)에 맞았을 때 밀리는 거리 비율 (보통 밀림 대비)")] private float lightKnockbackRatio = 0.3f;

        [Header("보스")]
        [SerializeField, Tooltip("슈퍼 아머: 어떤 공격에도 경직(공격 끊김)이 걸리지 않는다 (보스). 기절은 stunResist만큼 짧게 걸린다")] private bool superArmor;
        [SerializeField, Range(0f, 1f), Tooltip("기절 시간을 이 비율만큼 줄인다 (보스)")] private float stunResist;
        [SerializeField, Tooltip("강공격 확률 (0 이상이면 GameTuning 값 대신 이 값)")] private float heavyChanceOverride = -1f;
        [SerializeField, Tooltip("일반 공격 연속 타격 수 (1 = 한 번). 보스 1페이즈 3연타 — 타격마다 느낌표가 다시 노랑 → 빨강")] private int comboHits = 1;
        [SerializeField, Tooltip("연속 공격에서 다음 타격까지의 간격(초, Tempo 적용) — 이 간격이 다음 타격의 반응시간")] private float comboGap = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("일반 공격이 연속 공격으로 나올 확률 (comboHits > 1일 때, 나머지는 한 번만 친다)")] private float comboChance = 1f;
        [SerializeField, Tooltip("연속 공격 전 \"준비합니다\" 알림 뒤 기다리는 시간(초, Tempo 적용) — 이 동안 웅크린 채 서 있다")] private float comboAnnounceTime;

        [Header("공격 공통 — 강공격 확률·공격 간격·예비 모션 길이·반응시간·원거리 속도는 GameTuning (F1 패널)")]
        [SerializeField] private float recoverTime = 0.35f;
        [SerializeField] private int normalDamage = 10;
        [SerializeField] private int heavyDamage = 20;

        [Header("예비 모션 (텔레그래프 전)")]
        [SerializeField, Tooltip("근접 공격의 \"지금 눌러\" 느낌표가 뜨는 시점: 베기 직전 패링 판정 창(defenseActiveTime)의 이 비율만큼 앞")] private float pressCueFraction = 0.8f;
        [SerializeField] private float normalWindupScale = 1.06f;
        [SerializeField] private float heavyWindupScale = 1.25f;
        [SerializeField] private Color normalAttackColor = new(1f, 0.7f, 0.45f, 0.55f);
        [SerializeField] private Color heavyAttackColor = new(1f, 0.1f, 0.3f, 1f);
        [SerializeField, Tooltip("원거리 투사체 크기")] private Vector2 normalHitboxSize = new(0.6f, 0.6f);
        [SerializeField] private Vector2 heavyHitboxSize = new(0.9f, 0.9f);

        [Header("근접 베기 — 예비 모션(= 반응시간, GameTuning) 뒤 몸 앞에서 한 번에 뻗는 박스 (기획서 3.1)")]
        [SerializeField, Tooltip("베기가 뻗는 속도 (유닛/초) — 2.4 길이를 약 0.05초에")] private float meleeSlashSpeed = 45f;
        [SerializeField, Tooltip("이 거리(중심 간) 안에 들어오면 공격")] private float meleeRange = 2.6f;
        [SerializeField, Tooltip("다가가다 멈추는 거리 = 사거리 × 이 값")] private float meleeApproachRatio = 0.8f;
        [SerializeField, Tooltip("다 뻗었을 때 끝이 몸 중심에서 떨어진 거리 — 사거리 끝에 선 플레이어를 덮도록")] private float meleeReach = 2.9f;
        [SerializeField, Tooltip("발밑 기준 베기 박스 중심 높이")] private float meleeAttackHeight = 0.7f;
        [SerializeField] private float meleeNormalSlashHeight = 1.0f;
        [SerializeField] private float meleeHeavySlashHeight = 1.4f;

        [Header("원거리")]
        [SerializeField, Tooltip("원거리 몬스터 사정거리(유닛). 화면 반폭이 약 10.7이라 11이면 화면 끝에서도 쐈다 → 7로 줄임")] private float fireRange = 7f;
        [SerializeField, Tooltip("불덩이가 날아가는 최대 거리 (사정거리보다 조금 길게)")] private float rangedMaxTravel = 10f;

        public EnemyKind Kind => kind;
        public bool IsDead => _state == State.Dead;

        // ── 도트 그림(EnemySprite)이 읽는 상태 ──
        /// <summary>바라보는 방향 (+1 오른쪽 / -1 왼쪽). 공격이 나간 뒤에는 그 방향으로 고정.</summary>
        public float Facing { get; private set; } = -1f;
        public bool IsMoving { get; private set; }
        public bool IsWindingUp => _state == State.Windup;
        /// <summary>예비 모션 진행도 0~1 (그림을 반응시간 길이에 맞춰 재생).</summary>
        public float WindupProgress { get; private set; }
        /// <summary>지금(또는 방금) 공격이 강공격인지.</summary>
        public bool IsHeavyAttack { get; private set; }
        public bool IsRecovering => _state == State.Recover;
        public bool IsStaggered => _state == State.Stagger;
        /// <summary>그림에 곱할 색 — 네모와 같은 신호(예비 모션 공격색·기절 파랑)를 흰색 기준으로.</summary>
        public Color ArtTint { get; private set; } = Color.white;
        /// <summary>맞고 경직에 들어갈 때 (죽는 타격 제외). 피격 그림을 처음부터 다시 재생하는 데 쓴다.</summary>
        public event Action Staggered;
        public float HpRatio => (float)_hp / maxHp;
        public int XpReward => xpReward;
        /// <summary>엘리트 몬스터 (체력이 많고 경험치도 많다). 크기는 판정이라 그대로 두고 보라 빛무리로 구분한다.</summary>
        public bool IsElite { get; private set; }

        /// <summary>엘리트로 만든다 — 체력 배율·경험치 배율. 판정 크기는 바꾸지 않는다.</summary>
        public void MakeElite(float hpMultiplier, int xpMultiplier)
        {
            IsElite = true;
            maxHp = Mathf.RoundToInt(maxHp * hpMultiplier);
            _hp = maxHp;
            xpReward *= Mathf.Max(1, xpMultiplier);
            var aura = new GameObject("EliteAura").AddComponent<SpriteRenderer>();
            aura.transform.SetParent(transform, false);
            aura.transform.localScale = new Vector3(2.4f / Mathf.Max(0.1f, transform.localScale.x), 2.8f / Mathf.Max(0.1f, transform.localScale.y), 1f);
            aura.sprite = FxSprites.Glow;
            aura.color = new Color(0.75f, 0.4f, 1f, 0.55f);
            aura.sortingOrder = _sr != null ? _sr.sortingOrder - 1 : 0;
        }
        public bool IsStunned => !IsDead && Time.time < _stunUntil;
        /// <summary>몸 가로 절반 (예비 모션 크기 변화 제외).</summary>
        public float HalfWidth => _baseScale.x * 0.5f;

        private static readonly Color StunColor = new(0.55f, 0.75f, 1f);
        private float _stunUntil = float.NegativeInfinity;
        public event Action<EnemyController> Died;

        /// <summary>어떤 적이든 죽으면 호출 (경험치 등). 스포너를 거치지 않고 생긴 적도 잡기 위해 정적 이벤트.</summary>
        public static event Action<EnemyController> AnyDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AnyDied = null; // 도메인 리로드 없이 플레이해도 구독이 남지 않게

        private SpriteRenderer _sr;
        private Rigidbody2D _rb;
        private Transform _player;
        private Color _artTint = Color.white;
        private Color _baseColor;
        private Color _tint;
        private Vector3 _baseScale;
        private float _feetY;
        private float _flash;
        private int _hp;
        private float _cooldown;
        private State _state;
        private float _arenaMinX = -20f;
        private float _arenaMaxX = 20f;

        /// <summary>공격 박자 배율 — 예비 모션·회복·공격 간격에 곱한다 (1보다 작으면 빨라진다, 보스 2페이즈).</summary>
        public float Tempo { get; set; } = 1f;
        /// <summary>피해를 받지 않는다 (보스 변신 중).</summary>
        public bool Invulnerable { get; set; }
        /// <summary>움직이지도 공격하지도 않는다 (보스 변신 연출 중).</summary>
        public bool Frozen { get; set; }
        /// <summary>체력이 이 아래로 내려가지 않는다 (보스 1페이즈: 한 방에 2페이즈를 건너뛰고 죽지 않게).</summary>
        public int HpFloor { get; set; }
        public bool SuperArmor => superArmor;
        public int ComboHits { get => comboHits; set => comboHits = Mathf.Max(1, value); }
        /// <summary>일반 공격이 연속 공격으로 나올 확률 (0~1).</summary>
        public float ComboChance { get => comboChance; set => comboChance = Mathf.Clamp01(value); }
        /// <summary>연속 공격을 고른 순간 (예비 모션 전, 알림 시간 전) — 보스가 "연속 공격을 준비합니다"를 띄운다.</summary>
        public event Action ComboPreparing;
        public int NormalDamage { get => normalDamage; set => normalDamage = value; }
        /// <summary>지금 공격이 연속 공격(일반 공격 · 타격 2번 이상)인가 — 그림이 연속 공격 시트를 쓴다.</summary>
        public bool IsComboAttack { get; private set; }
        /// <summary>연속 공격에서 지금까지 나간 타격 수 (0 = 첫 예비 모션 중).</summary>
        public int ComboStep { get; private set; }
        /// <summary>연속 공격 타격 횟수 (테스트용 — 공격마다 늘어난다).</summary>
        public int ComboStrikeCount { get; private set; }
        public int MaxHp => maxHp;

        public float HeavyChanceOverride
        {
            get => heavyChanceOverride;
            set => heavyChanceOverride = value;
        }

        /// <summary>몸(판정) 크기를 바꾼다 — 발끝은 그대로 (보스 2페이즈에서 모습이 커질 때).</summary>
        public void SetBodySize(Vector2 size)
        {
            _baseScale = new Vector3(size.x, size.y, 1f);
            SetScaleFeetAnchored(_baseScale);
        }

        /// <summary>근접 베기 모양을 바꾼다 (보스 2페이즈: 더 길고 높게).</summary>
        public void SetMeleeGeometry(float range, float reach, float attackHeight, float normalHeight, float heavyHeight)
        {
            meleeRange = range;
            meleeReach = reach;
            meleeAttackHeight = attackHeight;
            meleeNormalSlashHeight = normalHeight;
            meleeHeavySlashHeight = heavyHeight;
        }

        /// <summary>하던 일이 있는가 (예비 모션·회복·경직·기절) — 보스 특수 패턴은 한가할 때만 시작한다.</summary>
        public bool IsBusy => _state != State.Idle || IsStunned;

        /// <summary>그 쪽을 바라본다 (보스 특수 패턴).</summary>
        public void FaceTowardsX(float x)
        {
            float dx = x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) Facing = Mathf.Sign(dx);
        }

        /// <summary>땅 위에서 x로 옮긴다 (보스 돌진). 바닥 범위 안으로 자른다.</summary>
        public float MoveToX(float x)
        {
            float half = _baseScale.x * 0.5f;
            x = Mathf.Clamp(x, _arenaMinX + half, _arenaMaxX - half);
            SetPosition(new Vector2(x, transform.position.y));
            return x;
        }

        public float ArenaMinX => _arenaMinX;
        public float ArenaMaxX => _arenaMaxX;
        public float FeetY => _feetY;

        /// <summary>"지금 패링" 느낌표를 노랑 → 빨강으로 (보스 특수 패턴이 직접 부른다).</summary>
        public void TurnCueRed(PressCueIcon icon) => ShowPressCue(icon);

        /// <summary>하던 공격(예비 모션·회복)을 그만둔다. 이미 나간 공격은 남는다.</summary>
        public void CancelAttack()
        {
            if (IsDead) return;
            StopAllCoroutines();
            RestoreBody();
            _state = State.Idle;
            _tint = _baseColor;
            _artTint = Color.white;
            WindupProgress = 0f;
        }

        public void SetArena(float minX, float maxX)
        {
            _arenaMinX = minX;
            _arenaMaxX = maxX;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _baseColor = _sr.color;
            _tint = _baseColor;
            _baseScale = transform.localScale;
            _hp = maxHp;
        }

        private void Start()
        {
            var defense = FindAnyObjectByType<PlayerDefense>();
            if (defense != null) _player = defense.transform;
            _feetY = transform.position.y - _baseScale.y * 0.5f;
            _cooldown = RollInterval() * 0.6f;
        }

        // 꺼진 동안엔 Update가 돌지 않아 걷는 중 표시가 남지 않게
        private void OnDisable() => IsMoving = false;

        private void Update()
        {
            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime * 8f);
            Color body = IsStunned ? Color.Lerp(_baseColor, StunColor, 0.65f) : _tint;
            _sr.color = Color.Lerp(body, Color.white, _flash);
            ArtTint = IsStunned ? Color.Lerp(Color.white, StunColor, 0.65f) : _artTint;

            IsMoving = false;
            // 공격을 시작하면(예비 모션·회복) 방향을 잠근다 — 플레이어가 옆으로 지나가도 그 방향 그 자리에서 휘두른다
            if (_player != null && _state == State.Idle && Mathf.Abs(DxToPlayer) > 0.05f)
                Facing = Mathf.Sign(DxToPlayer);

            if (Frozen) { IsMoving = false; return; }
            if (_state != State.Idle || IsStunned || _player == null || GameManager.InputBlocked) return;

            Move();
            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f && InAttackRange() && !T.enemyPassive) StartCoroutine(AttackRoutine());
        }

        private float _attackDir = -1f; // 지금 공격의 방향 (시작할 때 고정)

        private float DxToPlayer => _player.position.x - transform.position.x;

        /// <summary>땅을 걷는 몬스터인가 (창귀). 도깨비불(원거리)은 떠다녀서 구멍을 건넌다.</summary>
        public bool Walks => kind == EnemyKind.Melee;

        /// <summary>밀려날 때: 걷는 몬스터는 구멍 쪽으로 밀려 땅 밖으로 나가지 않게 한다.</summary>
        private float KnockbackX(float targetX, float half)
        {
            if (!Walks) return targetX;
            float dir = Mathf.Sign(targetX - transform.position.x);
            if (dir == 0f || GroundProbe.HasGround(targetX + dir * half, _feetY)) return targetX;
            return transform.position.x;
        }

        private bool InAttackRange()
        {
            if (kind == EnemyKind.Melee)
                return Mathf.Abs(DxToPlayer) <= meleeRange && Mathf.Abs(_player.position.y - transform.position.y) < 2.5f;
            return Vector2.Distance(_player.position, transform.position) <= fireRange;
        }

        private void Move()
        {
            float dx = DxToPlayer;
            float dist = Mathf.Abs(dx);
            float dir = 0f;

            if (kind == EnemyKind.Melee)
            {
                if (dist > meleeRange * meleeApproachRatio) dir = Mathf.Sign(dx);
            }
            else
            {
                // 사거리 안이면 그 자리에서 쏘고, 벗어나면 사거리 안으로 다시 접근한다 (거리 유지·후퇴 없음)
                if (!InAttackRange()) dir = Mathf.Sign(dx); // 사격 판정과 같은 거리 기준 (발판 위 플레이어에도 멈춰 서지 않게)
            }

            if (dir == 0f) return;
            float half = _baseScale.x * 0.5f;
            float x = Mathf.Clamp(transform.position.x + dir * moveSpeed * Time.deltaTime, _arenaMinX + half, _arenaMaxX - half);
            // 걷는 몬스터는 바닥 구멍 앞에서 멈춘다 (앞발 아래에 땅이 없으면 더 가지 않는다). 떠다니는 도깨비불은 그냥 건넌다
            if (Walks && !GroundProbe.HasGround(x + dir * half, _feetY)) return;
            IsMoving = true;
            SetPosition(new Vector2(x, transform.position.y));
        }

        private void SetPosition(Vector2 pos)
        {
            transform.position = pos;
            _rb.position = pos;
        }

        private IEnumerator AttackRoutine()
        {
            _state = State.Windup;
            // 공격 방향은 시작하는 순간 정해 둔다: 이후 플레이어가 이동·대시로 반대편에 가도 따라 돌지 않고 그 자리에서 그대로 벤다
            _attackDir = Mathf.Abs(DxToPlayer) > 0.05f ? Mathf.Sign(DxToPlayer) : (Facing != 0f ? Facing : -1f);
            Facing = _attackDir;
            float heavyChance = T.heavyOnly ? 1f : heavyChanceOverride >= 0f ? heavyChanceOverride : T.heavyChance;
            var type = UnityEngine.Random.value < heavyChance ? AttackType.Heavy : AttackType.Normal;
            bool heavy = type == AttackType.Heavy;
            // 근접: 베기가 한 번에 나가므로 예비 모션이 곧 반응시간 / 원거리: 짧은 예비 모션 + 날아오는 동안
            float windup = kind == EnemyKind.Melee
                ? (heavy ? T.meleeHeavyReaction : T.meleeNormalReaction)
                : (heavy ? T.heavyWindup : T.normalWindup);
            windup *= Mathf.Max(0.1f, Tempo);
            float pulse = heavy ? heavyWindupScale : normalWindupScale;
            Color attackColor = heavy ? heavyAttackColor : normalAttackColor;
            Color windupColor = new(attackColor.r, attackColor.g, attackColor.b, 1f);
            IsHeavyAttack = heavy;
            WindupProgress = 0f;
            IsComboAttack = !heavy && comboHits > 1 && kind == EnemyKind.Melee && UnityEngine.Random.value < comboChance;
            ComboStep = 0;
            if (IsComboAttack)
            {
                // 연속 공격은 드물게 나오고, 나오기 전에 알림 + 웅크린 채 잠깐 멈춘다 (이 동안 예비 모션 진행도 0 = 첫 장면)
                ComboPreparing?.Invoke();
                float wait = comboAnnounceTime * Mathf.Max(0.1f, Tempo);
                for (float t = 0f; t < wait; t += Time.deltaTime) yield return null;
            }

            Sfx.Play(heavy ? SfxId.HeavyCue : SfxId.NormalCue);
            // 예비 모션 시작 때의 경고는 원거리 강공격에만 있다. 근접(창귀)은 일반·강공격 모두 베기 직전의 "지금 눌러" 느낌표 하나뿐.
            if (heavy && kind == EnemyKind.Ranged)
            {
                var warn = GameAssets.AttackFx != null ? GameAssets.AttackFx.heavyWarning : null;
                Vector2 above = transform.position + Vector3.up * (_baseScale.y * 0.5f + 0.7f);
                if (FxClip.PlayAt(warn, above, 0.55f) == null)
                    Hud.WorldText(transform.position + Vector3.up * (_baseScale.y * 0.5f + 0.6f), "!!", heavyAttackColor, 1.3f);
            }

            // 근접은 일반·강공격 모두 느낌표 하나로 타이밍을 알려 준다: 예비 모션이 시작되면 노란 느낌표(공격이 온다)가 뜨고,
            // 베기가 나가기 직전 패링 판정 창(defenseActiveTime)이 베기 순간을 덮을 만큼의 시점(창의 80%)에 빨갛게 바뀐다
            // → 빨간 느낌표가 보이자마자 패링(S)을 누르면 맞는다.
            bool cueShown = false;
            PressCueIcon cueIcon = kind == EnemyKind.Melee ? PressCueIcon.Spawn(this) : null;
            float cueAt = windup - T.defenseActiveTime * pressCueFraction;
            // 예비 모션: 색이 공격색으로 차오르고, 몸이 커진다 (발밑 고정). 콜라이더도 같은 트랜스폼이라 함께 커짐.
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                if (kind == EnemyKind.Melee && !cueShown && t >= cueAt)
                {
                    cueShown = true;
                    ShowPressCue(cueIcon);
                }
                float k = t / windup;
                float s = Mathf.Lerp(1f, pulse, Mathf.Sin(k * Mathf.PI * 0.5f));
                SetScaleFeetAnchored(new Vector3(_baseScale.x * s, _baseScale.y * s, 1f));
                WindupProgress = k;
                _tint = Color.Lerp(_baseColor, windupColor, heavy ? k : k * 0.6f);
                _artTint = Color.Lerp(Color.white, windupColor, heavy ? k : k * 0.6f);
                yield return null;
            }

            WindupProgress = 1f;
            RestoreBody();
            if (_player != null) Launch(type);

            // 연속 공격(보스 3연타): 같은 방향으로 짧은 간격을 두고 다시 벤다. 타격마다 새 느낌표가 노랑 → 빨강으로 타이밍을 알려 준다
            if (IsComboAttack)
            {
                ComboStrikeCount++;
                for (int k = 1; k < comboHits && !IsDead; k++)
                {
                    ComboStep = k;
                    if (cueIcon != null) cueIcon.FadeOut();
                    cueIcon = PressCueIcon.Spawn(this);
                    float gap = comboGap * Mathf.Max(0.1f, Tempo);
                    float nextCue = gap - T.defenseActiveTime * pressCueFraction;
                    bool nextShown = false;
                    for (float t = 0f; t < gap; t += Time.deltaTime)
                    {
                        if (!nextShown && t >= nextCue)
                        {
                            nextShown = true;
                            ShowPressCue(cueIcon);
                        }
                        yield return null;
                    }
                    if (_player != null) Launch(type);
                    ComboStrikeCount++;
                }
                ComboStep = comboHits;
            }

            _state = State.Recover;
            yield return new WaitForSeconds(recoverTime * Mathf.Max(0.1f, Tempo));
            _state = State.Idle;
            _cooldown = RollInterval() * Mathf.Max(0.1f, Tempo);
        }

        /// <summary>"지금 패링을 눌러": 노란 느낌표가 빨갛게 바뀐다 (빨강은 적 공격 경고 전용).</summary>
        private void ShowPressCue(PressCueIcon icon)
        {
            Sfx.Play(SfxId.HeavyCue, 0.7f);
            PressCueCount++;
            if (icon != null) icon.TurnRed(); // 노랑 → 빨강 + 한 번 커졌다 가라앉는다
            // 빨갛게 바뀌는 순간 뒤에서 붉은 불꽃이 확 퍼진다 (에셋의 경고 이펙트)
            var warn = GameAssets.AttackFx != null ? GameAssets.AttackFx.heavyWarning : null;
            Vector2 at = transform.position + Vector3.up * (_baseScale.y * 0.5f + 1.35f);
            FxClip.PlayAt(warn, at, 0.45f, false, 48, new Color(1f, 1f, 1f, 0.8f));
        }

        /// <summary>느낌표를 띄운 횟수 (테스트용).</summary>
        public int PressCueCount { get; private set; }

        private static GameTuning T => GameTuning.Current;

        private static float RollInterval()
        {
            float min = Mathf.Max(0.1f, T.attackIntervalMin);
            return UnityEngine.Random.Range(min, Mathf.Max(min, T.attackIntervalMax));
        }

        private void Launch(AttackType type)
        {
            bool heavy = type == AttackType.Heavy;
            Vector2 size = heavy ? heavyHitboxSize : normalHitboxSize;
            Color color = heavy ? heavyAttackColor : normalAttackColor;
            int damage = heavy ? heavyDamage : normalDamage;

            if (kind == EnemyKind.Melee)
            {
                // 베기: 몸 앞면에서 한 번에 "삭" 뻗는다 (플레이어 공격 스킬처럼). 반응시간은 앞의 예비 모션이 맡는다.
                // 순간이동 대신 아주 빠르게 뻗게 해서, 뻗는 경로 위의 플레이어를 물리 판정이 놓치지 않게 한다.
                float dir = _attackDir; // 시작할 때 정한 방향 (플레이어 위치를 다시 보지 않는다)
                Facing = dir;
                var anchor = new Vector2(transform.position.x + dir * HalfWidth, _feetY + meleeAttackHeight);
                EnemyAttack.SpawnSlash(this, type, anchor, dir, heavy ? meleeHeavySlashHeight : meleeNormalSlashHeight,
                    meleeSlashSpeed, Mathf.Max(0.1f, meleeReach - HalfWidth), color, damage);
            }
            else
            {
                Vector2 origin = transform.position;
                Vector2 aim = (Vector2)_player.position - origin;
                var shot = EnemyAttack.Spawn(this, type, origin, aim, size, heavy ? T.rangedHeavySpeed : T.rangedNormalSpeed,
                    rangedMaxTravel, color, damage);
                // 도깨비불 불덩이 그림: 앞 4장 청록(일반) · 뒤 4장 빨강(강공격 — 빨강은 적 공격 경고 전용 색)
                var fx = GameAssets.AttackFx;
                if (fx != null) shot.AttachProjectileArt(fx.dokkaebiProjectile, heavy ? 4 : 0, 4, 2.2f);
            }
        }

        private void SetScaleFeetAnchored(Vector3 scale)
        {
            transform.localScale = scale;
            SetPosition(new Vector2(transform.position.x, _feetY + scale.y * 0.5f));
        }

        private void RestoreBody()
        {
            SetScaleFeetAnchored(_baseScale);
            _tint = _baseColor;
            _artTint = Color.white;
        }

        /// <param name="note">데미지 숫자 옆에 작게 붙일 설명 (예: "원거리 ×1.4")</param>
        /// <param name="stagger">Full = 밀림 + 경직 + 하던 공격 끊김 / Light = 살짝 밀림만 / None = 피해만 (HitStagger)</param>
        public void TakeDamage(int damage, float fromDirection, bool heavy, string note = null, HitStagger stagger = HitStagger.Full)
        {
            if (IsDead) return;

            if (Invulnerable) return; // 보스 변신 중: 피해 없음
            // 피격음은 맞은 공격의 무게로 고른다 (보스가 경직을 무시해도 소리는 원래 공격 무게대로) + 쇠로 된 보스는 쇳소리를 겹친다
            Sfx.Play(heavy ? SfxId.HitHeavy : stagger == HitStagger.Full ? SfxId.EnemyHit : SfxId.HitLight);
            if (superArmor) Sfx.Play(SfxId.HitMetal, heavy ? 1f : 0.6f);
            if (superArmor && stagger == HitStagger.Full) stagger = HitStagger.Light; // 보스는 경직에 걸리지 않는다 (강공격 반격도 — 사용자 요청). 넉백 0이라 밀리지도 않는다
            if (!T.enemyInvincible) _hp = Mathf.Max(HpFloor, _hp - damage);
            _flash = 1f;
            Fx.Sparks(transform.position, Color.white, heavy ? 12 : 7, heavy ? 9f : 6f);
            FxClip.PlayAt(GameAssets.AttackFx != null ? GameAssets.AttackFx.hitSpark : null, transform.position, heavy ? 0.7f : 0.5f);
            string text = string.IsNullOrEmpty(note) ? damage.ToString() : $"{damage} <size=20><color=#8CF2FF>{note}</color></size>";
            // 머리 위 체력바(+0.3) 위에서 시작해 글자가 체력바에 가리지 않게
            Hud.WorldText(transform.position + Vector3.up * (_baseScale.y * 0.5f + 0.75f), text,
                heavy ? new Color(1f, 0.85f, 0.3f) : Color.white, heavy ? 1.3f : 1f);

            if (_hp <= 0)
            {
                Die();
                return;
            }

            float half = _baseScale.x * 0.5f;
            if (stagger == HitStagger.None) return; // 피해·번쩍임만 — 하던 일(이동·예비 모션·공격)은 그대로
            if (stagger == HitStagger.Light)
            {
                // 살짝 밀리기만 하고 경직·공격 취소는 없다
                float lx = Mathf.Clamp(transform.position.x + fromDirection * knockback * lightKnockbackRatio, _arenaMinX + half, _arenaMaxX - half);
                SetPosition(new Vector2(KnockbackX(lx, half), transform.position.y));
                return;
            }

            StopAllCoroutines();
            RestoreBody();
            float x = Mathf.Clamp(transform.position.x + fromDirection * knockback, _arenaMinX + half, _arenaMaxX - half);
            SetPosition(new Vector2(KnockbackX(x, half), transform.position.y));
            StartCoroutine(StaggerRoutine());
            Staggered?.Invoke();
        }

        /// <summary>
        /// 기절: 예비 모션 중이면 공격을 취소하고, 시간 동안 움직이지도 공격하지도 않는다.
        /// 이미 날아간 공격은 그대로 둔다 — 이미 텔레그래프된 공격이라 플레이어가 막을 수 있고, 지우면 "막을 기회"도 사라지기 때문.
        /// 기절 중 맞아도 경직(0.35초)이 기절을 줄이지 않는다.
        /// </summary>
        /// <param name="ignoreResist">기절 저항(stunResist)을 무시하고 그대로 건다 (보스 연속 패링 보상)</param>
        public void Stun(float seconds, bool ignoreResist = false)
        {
            if (IsDead || seconds <= 0f) return;
            if (!ignoreResist) seconds *= 1f - stunResist;
            bool fresh = !IsStunned;
            _stunUntil = Mathf.Max(_stunUntil, Time.time + seconds);
            if (_state == State.Windup || _state == State.Recover)
            {
                StopAllCoroutines();
                RestoreBody();
                _state = State.Idle;
            }
            _cooldown = Mathf.Max(_cooldown, 0.5f); // 풀리자마자 바로 치지 않게
            if (fresh)
                Hud.WorldText(transform.position + Vector3.up * (_baseScale.y * 0.5f + 1.25f), "기절", StunColor, 0.9f);
        }

        private IEnumerator StaggerRoutine()
        {
            _state = State.Stagger;
            yield return new WaitForSeconds(staggerTime);
            _state = State.Idle;
            _cooldown = Mathf.Max(_cooldown, 0.6f);
        }

        /// <summary>죽이지 않고 조용히 치운다 (연습 스위치) — 경험치·처치 이벤트 없음. 이미 나간 공격은 남는다.</summary>
        public void Despawn()
        {
            if (IsDead) return;
            _state = State.Dead;
            StopAllCoroutines();
            Fx.Ring(transform.position, Color.white, 0.5f, 2.5f, 0.1f, 0.3f);
            Destroy(gameObject);
        }

        private void Die()
        {
            _state = State.Dead;
            StopAllCoroutines();
            Sfx.Play(SfxId.EnemyDie);
            Fx.Sparks(transform.position, _baseColor, 20, 9f, 0.2f, 0.6f);
            FxClip.PlayAt(GameAssets.AttackFx != null ? GameAssets.AttackFx.yokaiDissolve : null, transform.position + Vector3.up * 0.2f, 0.9f);
            Fx.Ring(transform.position, _baseColor, 0.5f, 3f, 0.15f, 0.35f);
            Died?.Invoke(this);
            AnyDied?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
