using UnityEngine;

namespace ParryRL
{
    /// <summary>좌우 이동 / 2단 고정 높이 점프 / 대시(이동 스킬용).</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerMotor : MonoBehaviour
    {
        // 걷기 속도는 캐릭터별 — GameTuning (F1 캐릭터 탭). 궁수가 전사보다 빠르다.

        [Header("점프 (2단, 누르는 시간과 무관한 고정 높이 — 공중에서 한 번 더 누르면 그 자리에서 다시 솟는다)")]
        [SerializeField] private float jumpVelocity = 7f;
        [SerializeField] private int maxJumps = 2;

        [Header("대시")]
        [SerializeField] private float dashDistance = 3f;
        [SerializeField] private float dashDuration = 0.15f;

        [Header("회피 성공 후퇴 (Project_Game 방식 — 실시간으로 직접 이동하므로 시간이 멈춰도 움직인다)")]
        [SerializeField, Tooltip("뒤로 젖혀지는 최대 각도")] private float backstepLeanAngle = 12f;

        [Header("공격 내딛기 (전사 베기 때 몸이 칼을 따라 살짝 나아간다 — 무적 아님)")]
        [SerializeField] private float lungeDistance = 0.45f;
        [SerializeField] private float lungeDuration = 0.12f;

        [SerializeField, Tooltip("바라보는 방향 표시용 (판정 없음)")] private Transform eye;

        public int Facing { get; private set; } = 1;
        public bool IsGrounded { get; private set; }
        public bool IsDashing => _dashTimer > 0f;
        /// <summary>회피 성공 후퇴 중 — 무적이고 입력을 받지 않는다.</summary>
        public bool IsBackstepping { get; private set; }

        /// <summary>이동 스킬 무적: 대시 중 + 끝난 뒤 여유 시간 (GameTuning).</summary>
        public bool IsDashInvulnerable =>
            GameTuning.Current.dashInvulnerable && (IsDashing || Time.time - _dashEndTime < GameTuning.Current.dashInvulnerableExtra);

        private float _dashEndTime = float.NegativeInfinity;

        /// <summary>지금 나와 있는 캐릭터의 걷기 속도.</summary>
        public float MoveSpeed => _party != null ? GameTuning.Current.MoveSpeed(_party.Current.kind) : GameTuning.Current.warriorMoveSpeed;

        /// <summary>방어 모션(제자리) 중에는 걷기 입력을 막는다.</summary>
        public bool MovementLocked { get; set; }

        public float HorizontalInput => _h;

        private Rigidbody2D _rb;
        private PlayerParty _party;
        private BoxCollider2D _collider;
        private float _gravityScale;
        private float _h;
        private int _jumpsLeft;
        private float _dashTimer;
        private float _dashDir;
        private float _dashSpeed;
        private float _afterimageTimer;
        private SpriteRenderer _sr;
        private PlayerSprite _sprite;
        private Collider2D _dropCollider;
        private float _lungeTimer;
        private int _lungeDir;
        private float _dropTimer;
        private float _backstepStartTime;
        private float _backstepStartX;
        private float _backstepDistance;
        private float _backstepDuration;
        private int _backstepDir;
        private readonly RaycastHit2D[] _wallHits = new RaycastHit2D[8];
        private readonly Collider2D[] _groundHits = new Collider2D[4];

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _party = GetComponent<PlayerParty>();
            _collider = GetComponent<BoxCollider2D>();
            _sr = GetComponent<SpriteRenderer>();
            _sprite = GetComponent<PlayerSprite>();
            _gravityScale = _rb.gravityScale;
            _jumpsLeft = maxJumps;
        }

        private void Update()
        {
            if (IsBackstepping)
            {
                TickBackstep();
                _h = 0f;
                return;
            }

            if (GameManager.InputBlocked)
            {
                _h = 0f;
                return;
            }

            _h = Controls.Horizontal;
            if (_h != 0f && !MovementLocked && !IsDashing) SetFacing(_h);

            if (_dropTimer > 0f)
            {
                _dropTimer -= Time.deltaTime;
                if (_dropTimer <= 0f) EndDrop();
            }

            // ↓ + 점프: 발판 위에 서 있으면 점프 대신 발판을 통과해 내려간다
            if (Input.GetKeyDown(Controls.Jump) && Input.GetKey(Controls.Down) && !IsDashing && TryDropThrough()) { }
            else if (Input.GetKeyDown(Controls.Jump) && _jumpsLeft > 0 && !IsDashing)
            {
                _jumpsLeft--;
                var v = _rb.linearVelocity;
                v.y = jumpVelocity;
                _rb.linearVelocity = v;
                Sfx.Play(SfxId.Jump);
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGround();
            if (IsGrounded && _rb.linearVelocity.y <= 0.01f) _jumpsLeft = maxJumps;

            if (IsBackstepping)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            if (_lungeTimer > 0f && !IsDashing)
            {
                // 빠르게 나가 감속하는 짧은 내딛기 (거리 = lungeDistance)
                _lungeTimer -= Time.fixedDeltaTime;
                float k = Mathf.Clamp01(_lungeTimer / lungeDuration);
                _rb.linearVelocity = new Vector2(_lungeDir * lungeDistance / lungeDuration * 2f * k, _rb.linearVelocity.y);
                return;
            }

            if (IsDashing)
            {
                _dashTimer -= Time.fixedDeltaTime;
                _rb.linearVelocity = new Vector2(_dashDir * _dashSpeed, 0f);

                _afterimageTimer -= Time.fixedDeltaTime;
                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = 0.03f;
                    if (_sprite == null || !_sprite.TrySpawnAfterimage())
                        Fx.Afterimage(transform.position, transform.localScale, _sr.color, 0.18f, _sr.sprite, _sr.flipX);
                }

                if (_dashTimer <= 0f)
                {
                    _dashEndTime = Time.time;
                    _rb.gravityScale = _gravityScale;
                    _rb.linearVelocity = new Vector2(_dashDir * MoveSpeed, 0f);
                }
                return;
            }

            float vx = MovementLocked ? 0f : _h * MoveSpeed;
            _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
        }

        /// <summary>
        /// 회피 성공 후퇴: 시작 위치에서 ease-out으로 distance만큼 물러난다. timeScale이 0이어도 물리가 안 도니
        /// 실시간(unscaledTime)으로 트랜스폼을 직접 옮긴다. 벽 앞에서는 멈춘다 (적은 트리거라 통과).
        /// </summary>
        public void Backstep(int dir, float distance, float duration)
        {
            _dashTimer = 0f;
            _rb.gravityScale = _gravityScale;
            _backstepDir = dir >= 0 ? 1 : -1;
            _backstepDistance = ClampToWall(_backstepDir, distance);
            _backstepDuration = Mathf.Max(0.01f, duration);
            _backstepStartTime = Time.unscaledTime;
            _backstepStartX = transform.position.x;
            IsBackstepping = true;
        }

        private void TickBackstep()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _backstepStartTime) / _backstepDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            var pos = new Vector3(_backstepStartX + _backstepDir * _backstepDistance * eased, transform.position.y, transform.position.z);
            transform.position = pos;
            _rb.position = pos;

            // 이동 반대쪽으로 몸이 젖혀지는 회피 모션
            float falloff = (1f - t) * (1f - t);
            float lean = _backstepDir * backstepLeanAngle * Mathf.Sin(Mathf.Clamp01(t * 2f) * Mathf.PI * 0.5f) * falloff;
            transform.rotation = Quaternion.Euler(0f, 0f, t >= 1f ? 0f : lean);

            if (t >= 1f)
            {
                IsBackstepping = false;
                Physics2D.SyncTransforms();
            }
        }

        private float ClampToWall(int dir, float distance)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = _collider.Cast(new Vector2(dir, 0f), filter, _wallHits, distance);
            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(_wallHits[i].normal.x) < 0.5f) continue; // 바닥·천장
                allowed = Mathf.Min(allowed, Mathf.Max(0f, _wallHits[i].distance));
            }
            return allowed;
        }

        private bool CheckGround()
        {
            Vector2 size = transform.localScale;
            var center = new Vector2(transform.position.x, transform.position.y - size.y * 0.5f - 0.03f);
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapBox(center, new Vector2(size.x * 0.9f, 0.06f), 0f, filter, _groundHits);
            bool rising = _rb.linearVelocity.y > 0.01f;
            for (int i = 0; i < count; i++)
            {
                if (_groundHits[i] == _collider || _groundHits[i] == _dropCollider) continue;
                // 올라가는 중에 발판을 통과하는 동안은 바닥으로 치지 않는다 (점프 횟수가 공짜로 돌아오지 않게)
                if (rising && _groundHits[i].GetComponent<PlatformEffector2D>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>서 있는 곳이 얇은 발판(PlatformEffector2D)이면 잠깐 충돌을 꺼서 아래로 내려간다.</summary>
        public bool TryDropThrough()
        {
            if (!IsGrounded || IsBackstepping || _dropTimer > 0f) return false;
            Vector2 size = transform.localScale;
            var center = new Vector2(transform.position.x, transform.position.y - size.y * 0.5f - 0.03f);
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapBox(center, new Vector2(size.x * 0.9f, 0.06f), 0f, filter, _groundHits);
            for (int i = 0; i < count; i++)
            {
                if (_groundHits[i] == _collider || _groundHits[i].GetComponent<PlatformEffector2D>() == null) continue;
                _dropCollider = _groundHits[i];
                _dropTimer = 0.35f;
                Physics2D.IgnoreCollision(_collider, _dropCollider, true);
                IsGrounded = false;
                _jumpsLeft = Mathf.Min(_jumpsLeft, maxJumps - 1); // 내려간 뒤엔 공중 점프 한 번 소모
                return true;
            }
            return false;
        }

        private void EndDrop()
        {
            if (_dropCollider != null) Physics2D.IgnoreCollision(_collider, _dropCollider, false);
            _dropCollider = null;
        }

        /// <summary>전사가 벨 때 칼을 따라 몸이 살짝 나아간다. 대시와 달리 무적이 아니고 중력도 그대로.</summary>
        public void Lunge(int dir)
        {
            if (IsBackstepping || IsDashing) return;
            _lungeDir = dir >= 0 ? 1 : -1;
            _lungeTimer = lungeDuration;
        }

        public void SetFacing(float dir)
        {
            if (dir == 0f) return;
            Facing = dir > 0f ? 1 : -1;
            if (eye != null)
            {
                var p = eye.localPosition;
                p.x = Mathf.Abs(p.x) * Facing;
                eye.localPosition = p;
            }
        }

        public void FaceTowards(float worldX)
        {
            float dx = worldX - transform.position.x;
            if (Mathf.Abs(dx) > 0.01f) SetFacing(dx);
        }

        /// <param name="extraDistance">증강 보정 (거리 유지: 백스텝 +1.5)</param>
        public void Dash(int dir, float extraDistance = 0f)
        {
            _dashDir = dir;
            _dashSpeed = (dashDistance + extraDistance) / dashDuration;
            _dashTimer = dashDuration;
            _afterimageTimer = 0f;
            _rb.gravityScale = 0f;
        }
    }
}
