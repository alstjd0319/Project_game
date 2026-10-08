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

        [Header("공격 공통 — 강공격 확률·공격 간격·예비 모션 길이·반응시간·원거리 속도는 GameTuning (F1 패널)")]
        [SerializeField] private float recoverTime = 0.35f;
        [SerializeField] private int normalDamage = 10;
        [SerializeField] private int heavyDamage = 20;

        [Header("도트 아트 (비워두면 흰 네모) — 원본은 왼쪽을 본다. 판정-시각 일치를 위해 몸 크기(1×1 × 스케일)에 맞춰 늘려 그린다")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite windupSprite;
        [SerializeField] private Sprite attackSprite;

        [Header("예비 모션 (텔레그래프 전)")]
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
        [SerializeField] private float fireRange = 11f;
        [SerializeField] private float rangedMaxTravel = 16f;

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

        public void SetArena(float minX, float maxX)
        {
            _arenaMinX = minX;
            _arenaMaxX = maxX;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            if (idleSprite != null)
            {
                _sr.sprite = idleSprite;
                _sr.drawMode = SpriteDrawMode.Sliced;
                _sr.size = Vector2.one;
            }
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
            UpdateSprite();
            ArtTint = IsStunned ? Color.Lerp(Color.white, StunColor, 0.65f) : _artTint;

            IsMoving = false;
            if (_player != null && (_state == State.Idle || _state == State.Windup) && Mathf.Abs(DxToPlayer) > 0.05f)
                Facing = Mathf.Sign(DxToPlayer);

            if (_state != State.Idle || IsStunned || _player == null || GameManager.InputBlocked) return;

            Move();
            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f && InAttackRange() && !T.enemyPassive) StartCoroutine(AttackRoutine());
        }

        private void UpdateSprite()
        {
            if (idleSprite == null) return;
            Sprite sprite = _state switch
            {
                State.Windup => windupSprite != null ? windupSprite : idleSprite,
                State.Recover => attackSprite != null ? attackSprite : idleSprite,
                _ => idleSprite,
            };
            _sr.sprite = sprite;
            if (_player != null && !IsDead) _sr.flipX = DxToPlayer > 0f;
        }

        private float DxToPlayer => _player.position.x - transform.position.x;

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
            IsMoving = true;
            float half = _baseScale.x * 0.5f;
            float x = Mathf.Clamp(transform.position.x + dir * moveSpeed * Time.deltaTime, _arenaMinX + half, _arenaMaxX - half);
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
            float heavyChance = T.heavyOnly ? 1f : T.heavyChance;
            var type = UnityEngine.Random.value < heavyChance ? AttackType.Heavy : AttackType.Normal;
            bool heavy = type == AttackType.Heavy;
            // 근접: 베기가 한 번에 나가므로 예비 모션이 곧 반응시간 / 원거리: 짧은 예비 모션 + 날아오는 동안
            float windup = kind == EnemyKind.Melee
                ? (heavy ? T.meleeHeavyReaction : T.meleeNormalReaction)
                : (heavy ? T.heavyWindup : T.normalWindup);
            float pulse = heavy ? heavyWindupScale : normalWindupScale;
            Color attackColor = heavy ? heavyAttackColor : normalAttackColor;
            Color windupColor = new(attackColor.r, attackColor.g, attackColor.b, 1f);
            IsHeavyAttack = heavy;
            WindupProgress = 0f;

            Sfx.Play(heavy ? SfxId.HeavyCue : SfxId.NormalCue);
            if (heavy) Hud.WorldText(transform.position + Vector3.up * (_baseScale.y * 0.5f + 0.6f), "!!", heavyAttackColor, 1.3f);

            // 예비 모션: 색이 공격색으로 차오르고, 몸이 커진다 (발밑 고정). 콜라이더도 같은 트랜스폼이라 함께 커짐.
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
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

            _state = State.Recover;
            yield return new WaitForSeconds(recoverTime);
            _state = State.Idle;
            _cooldown = RollInterval();
        }

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
                // 베기: 몸 앞면에서 한 번에 "삭" 뻗는다 (전사 공격 스킬처럼). 반응시간은 앞의 예비 모션이 맡는다.
                // 순간이동 대신 아주 빠르게 뻗게 해서, 뻗는 경로 위의 플레이어를 물리 판정이 놓치지 않게 한다.
                float dir = Mathf.Sign(DxToPlayer);
                Facing = dir;
                var anchor = new Vector2(transform.position.x + dir * HalfWidth, _feetY + meleeAttackHeight);
                EnemyAttack.SpawnSlash(this, type, anchor, dir, heavy ? meleeHeavySlashHeight : meleeNormalSlashHeight,
                    meleeSlashSpeed, Mathf.Max(0.1f, meleeReach - HalfWidth), color, damage);
            }
            else
            {
                Vector2 origin = transform.position;
                Vector2 aim = (Vector2)_player.position - origin;
                EnemyAttack.Spawn(this, type, origin, aim, size, heavy ? T.rangedHeavySpeed : T.rangedNormalSpeed,
                    rangedMaxTravel, color, damage);
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
        public void TakeDamage(int damage, float fromDirection, bool heavy, string note = null)
        {
            if (IsDead) return;

            if (!T.enemyInvincible) _hp -= damage;
            _flash = 1f;
            Sfx.Play(SfxId.EnemyHit);
            Fx.Sparks(transform.position, Color.white, heavy ? 12 : 7, heavy ? 9f : 6f);
            string text = string.IsNullOrEmpty(note) ? damage.ToString() : $"{damage} <size=20><color=#8CF2FF>{note}</color></size>";
            // 머리 위 체력바(+0.3) 위에서 시작해 글자가 체력바에 가리지 않게
            Hud.WorldText(transform.position + Vector3.up * (_baseScale.y * 0.5f + 0.75f), text,
                heavy ? new Color(1f, 0.85f, 0.3f) : Color.white, heavy ? 1.3f : 1f);

            if (_hp <= 0)
            {
                Die();
                return;
            }

            StopAllCoroutines();
            RestoreBody();
            float half = _baseScale.x * 0.5f;
            float x = Mathf.Clamp(transform.position.x + fromDirection * knockback, _arenaMinX + half, _arenaMaxX - half);
            SetPosition(new Vector2(x, transform.position.y));
            StartCoroutine(StaggerRoutine());
            Staggered?.Invoke();
        }

        /// <summary>
        /// 기절: 예비 모션 중이면 공격을 취소하고, 시간 동안 움직이지도 공격하지도 않는다.
        /// 이미 날아간 공격은 그대로 둔다 — 이미 텔레그래프된 공격이라 플레이어가 막을 수 있고, 지우면 "막을 기회"도 사라지기 때문.
        /// 기절 중 맞아도 경직(0.35초)이 기절을 줄이지 않는다.
        /// </summary>
        public void Stun(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
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
            Fx.Ring(transform.position, _baseColor, 0.5f, 3f, 0.15f, 0.35f);
            Died?.Invoke(this);
            AnyDied?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
