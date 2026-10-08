using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 지금 나와 있는 캐릭터의 도트 그림을 판정 네모 위에 그린다.
    /// 판정은 여전히 플레이어 네모(BoxCollider2D)이고, 그림은 자식 오브젝트라 판정에 영향이 없다.
    /// 그림이 있는 캐릭터는 네모를 숨기고(연습 스위치로 겹쳐 보기 가능), 없는 캐릭터는 네모로 표시한다.
    /// </summary>
    // PlayerParty.LateUpdate가 색(피격·무적·쿨타임)을 계산한 뒤에 읽어야 한다.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerParty), typeof(PlayerMotor), typeof(PlayerDefense))]
    public class PlayerSprite : MonoBehaviour
    {
        private const int BoxOrder = 10;
        private const int HitboxOverlayOrder = 13;
        private const float ParryImpactHold = 0.18f;
        private const float RunThreshold = 0.1f;
        private const float JumpApexSpeed = 1.5f;   // 세로 속도가 이보다 작으면 정점 그림
        private const float LandHold = 0.1f;
        private const float MinAirTimeForLanding = 0.15f;

        [SerializeField] private SpriteRenderer artRenderer;
        [SerializeField] private CharacterArt[] arts;
        [SerializeField, Tooltip("네모일 때만 보이는 방향 표시 눈")] private Transform eye;

        /// <summary>지금 재생 중인 동작 이름 (그림이 없으면 빈 문자열). 테스트·디버그용.</summary>
        public string Clip { get; private set; } = "";
        public int Frame { get; private set; }
        public bool IsShowingArt => artRenderer != null && artRenderer.enabled;
        public SpriteRenderer ArtRenderer => artRenderer;

        private PlayerParty _party;
        private PlayerMotor _motor;
        private PlayerDefense _defense;
        private PlayerCombat _combat;
        private Rigidbody2D _rb;
        private SpriteRenderer _box;

        private string _lastClip = "";
        private float _clipTime;
        private float _parryTime;
        private float _parryImpactTimer;
        private bool _wasDashing;
        private float _dashTime;
        private float _airTime;
        private float _landTimer;
        private float _deathTime;

        // 한 번 재생하는 동작 (반격·공격 스킬·피격·교대 등장). 새로 들어오면 덮어쓴다.
        private string _oneShot;
        private float _oneShotTime;

        private void Awake()
        {
            _party = GetComponent<PlayerParty>();
            _motor = GetComponent<PlayerMotor>();
            _defense = GetComponent<PlayerDefense>();
            _combat = GetComponent<PlayerCombat>();
            _rb = GetComponent<Rigidbody2D>();
            _box = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _defense.Activated += OnDefenseActivated;
            _party.Damaged += OnDamaged;
            if (_combat != null)
            {
                _combat.DefenseSucceeded += OnDefenseSucceeded;
                _combat.CounterFired += OnCounterFired;
                _combat.ActionUsed += OnActionUsed;
            }
        }

        private void OnDisable()
        {
            _defense.Activated -= OnDefenseActivated;
            _party.Damaged -= OnDamaged;
            if (_combat != null)
            {
                _combat.DefenseSucceeded -= OnDefenseSucceeded;
                _combat.CounterFired -= OnCounterFired;
                _combat.ActionUsed -= OnActionUsed;
            }
        }

        private void OnDefenseActivated()
        {
            _parryTime = 0f;
            _oneShot = null; // 방어 입력이 진행 중인 동작보다 우선
        }

        private void OnDefenseSucceeded(DefenseSuccess _) => _parryImpactTimer = ParryImpactHold;

        // 반격이 바로 이어지면 막는 장면 대신 반격 그림 (반격 첫 장 = 칼을 든 자세라 막는 순간으로도 읽힌다)
        private void OnCounterFired(CounterShot _)
        {
            _parryImpactTimer = 0f;
            PlayOnce("counter");
        }

        private void OnActionUsed(PlayerAction action)
        {
            if (action == PlayerAction.Attack) PlayOnce("attack");
            else if (action == PlayerAction.Move) _oneShot = null;
        }

        private void OnDamaged(int _) => PlayOnce("hit");

        private void PlayOnce(string clip)
        {
            _oneShot = clip;
            _oneShotTime = 0f;
        }

        public CharacterArt ArtFor(CharacterKind kind)
        {
            if (arts == null) return null;
            foreach (var a in arts)
                if (a != null && a.kind == kind) return a;
            return null;
        }

        private void LateUpdate()
        {
            // 히트스탑(timeScale 0) 동안 그림도 멈춰야 "멈칫"이 산다 → 게임 시간 기준.
            float dt = Time.deltaTime;
            _clipTime += dt;
            _parryTime += dt;
            _dashTime += dt;
            _oneShotTime += dt;
            if (_parryImpactTimer > 0f) _parryImpactTimer -= dt;
            if (_landTimer > 0f) _landTimer -= dt;
            // 게임오버가 timeScale을 0으로 만들어도 쓰러지는 장면은 끝까지 재생
            if (_party.IsDead) _deathTime += Time.unscaledDeltaTime;
            else _deathTime = 0f;

            bool dashing = _motor.IsDashing;
            if (dashing && !_wasDashing) _dashTime = 0f;
            _wasDashing = dashing;

            if (!_motor.IsGrounded) _airTime += dt;
            else
            {
                if (_airTime >= MinAirTimeForLanding) _landTimer = LandHold;
                _airTime = 0f;
            }

            var art = ArtFor(_party.Current.kind);
            if (art == null || artRenderer == null)
            {
                if (artRenderer != null) artRenderer.enabled = false;
                _box.enabled = true;
                _box.sortingOrder = BoxOrder;
                if (eye != null) eye.gameObject.SetActive(true);
                Clip = "";
                return;
            }

            artRenderer.enabled = true;
            if (eye != null) eye.gameObject.SetActive(false);

            // 판정 네모: 평소 숨김, 연습 스위치로 그림 위에 반투명하게 겹쳐 본다.
            bool overlay = GameTuning.Current.showHitboxes;
            _box.enabled = overlay;
            if (overlay)
            {
                var c = _box.color;
                c.a *= 0.45f;
                _box.color = c;
                _box.sortingOrder = HitboxOverlayOrder;
            }

            Pick(art, out string clip, out Sprite sprite, out int frame);
            if (clip != _lastClip)
            {
                // 동작이 바뀌면 반복 동작은 첫 프레임부터
                _lastClip = clip;
                _clipTime = 0f;
                Pick(art, out clip, out sprite, out frame);
            }
            Clip = clip;
            Frame = frame;
            artRenderer.sprite = sprite;
            artRenderer.flipX = _motor.Facing < 0;
            artRenderer.color = _party.ArtTint;
        }

        private void Pick(CharacterArt art, out string clip, out Sprite sprite, out int frame)
        {
            SpriteClip source;

            if (_party.IsDead && !art.death.IsEmpty)
            {
                clip = "death";
                source = art.death;
                frame = Mathf.Min(source.frames.Length - 1, (int)(_deathTime * source.fps));
            }
            else if (_motor.IsDashing && !art.dash.IsEmpty)
            {
                clip = "dash";
                source = art.dash;
                frame = Mathf.Min(source.frames.Length - 1, (int)(_dashTime * source.fps));
            }
            else if (_parryImpactTimer > 0f && !art.parry.IsEmpty)
            {
                clip = "parry";
                source = art.parry;
                frame = Mathf.Clamp(art.parryImpactFrame, 0, source.frames.Length - 1);
            }
            else if (OneShotFrame(art, out source, out frame))
            {
                clip = _oneShot; // 막은 직후엔 활성이 남아 있어도 반격 그림이 우선 (새로 방어를 누르면 _oneShot이 지워진다)
            }
            else if (_defense.State == DefenseState.Active && !_defense.SucceededThisActive && !art.parry.IsEmpty)
            {
                // 활성 시간(+버퍼)에 맞춰 한 번 재생 — 그림 길이와 판정 길이가 어긋나지 않게
                clip = "parry";
                source = art.parry;
                int n = source.frames.Length;
                float duration = Mathf.Max(0.01f, _defense.ActiveDuration);
                frame = Mathf.Min(n - 1, (int)(_parryTime / duration * n));
            }
            else if (!_motor.IsGrounded)
            {
                clip = "air";
                if (!art.jump.IsEmpty)
                {
                    // 도약(0)은 건너뛴다 — 점프 입력이 바로 떠야 조작감이 산다. 1 상승 · 2 정점 · 3 낙하
                    source = art.jump;
                    float vy = _rb.linearVelocity.y;
                    frame = vy > JumpApexSpeed ? 1 : vy < -JumpApexSpeed ? 3 : 2;
                    frame = Mathf.Min(frame, source.frames.Length - 1);
                }
                else
                {
                    source = art.run;
                    frame = Mathf.Clamp(art.airRunFrame, 0, Mathf.Max(0, source.frames.Length - 1));
                }
            }
            else if (_landTimer > 0f && !art.jump.IsEmpty)
            {
                clip = "land";
                source = art.jump;
                frame = source.frames.Length - 1;
            }
            else if (Mathf.Abs(_rb.linearVelocity.x) > RunThreshold && !art.run.IsEmpty)
            {
                clip = "run";
                source = art.run;
                frame = Loop(source);
            }
            else
            {
                clip = "idle";
                source = art.idle;
                frame = source.IsEmpty ? 0 : Loop(source);
            }

            sprite = source.IsEmpty ? null : source.frames[frame];
        }

        /// <summary>한 번 재생하는 동작의 현재 프레임. 끝났거나 그림이 없으면 false.</summary>
        private bool OneShotFrame(CharacterArt art, out SpriteClip source, out int frame)
        {
            source = _oneShot switch
            {
                "counter" => art.counter,
                "attack" => art.attack,
                "hit" => art.hit,
                "enter" => art.enter,
                _ => null,
            };
            frame = 0;
            if (source == null || source.IsEmpty)
            {
                _oneShot = null;
                return false;
            }
            int start = _oneShot switch
            {
                "counter" => art.counterHitFrame,
                "attack" => art.attackHitFrame,
                _ => 0,
            };
            frame = Mathf.Clamp(start, 0, source.frames.Length - 1) + (int)(_oneShotTime * source.fps);
            if (frame >= source.frames.Length)
            {
                _oneShot = null;
                return false;
            }
            return true;
        }

        private int Loop(SpriteClip c) => (int)(_clipTime * c.fps) % c.frames.Length;

        /// <summary>대시 잔상: 그림이 있으면 지금 프레임 모양으로 남긴다.</summary>
        public bool TrySpawnAfterimage()
        {
            if (!IsShowingArt || artRenderer.sprite == null) return false;
            Fx.SpriteAfterimage(artRenderer.sprite, artRenderer.transform.position, artRenderer.transform.lossyScale,
                artRenderer.flipX, new Color(0.75f, 0.78f, 0.85f, 0.55f));
            return true;
        }
    }
}
