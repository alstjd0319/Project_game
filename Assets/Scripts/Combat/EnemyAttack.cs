using UnityEngine;

namespace ParryRL
{
    /// <summary>플레이어와 판정이 끝난 공격을 어떻게 처리할지.</summary>
    public enum AttackResolution
    {
        /// <summary>패링 — 그 자리에서 부서짐.</summary>
        Shatter,
        /// <summary>회피·스왑 무적 — 판정이 꺼진 채 몸을 통과해 지나감 (연출만).</summary>
        PassThrough,
        /// <summary>피격 — 맞고 사라짐.</summary>
        Consumed,
    }

    /// <summary>
    /// 적 공격 히트박스 = 텔레그래프 인디케이터 (기획서 3.1). 두 가지 모양:
    /// 투사체(원거리) — 박스가 고정 속도로 날아간다 / 근접 베기 — 예비 모션 뒤 몸 앞에서 한 번에 뻗는다.
    /// 보이는 네모와 BoxCollider2D(Is Trigger)가 같은 트랜스폼을 쓰므로 판정 = 시각 1:1.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class EnemyAttack : MonoBehaviour
    {
        public AttackType Type { get; private set; }
        public EnemyController Source { get; private set; }
        public int Damage { get; private set; }
        public Vector2 Direction => _direction;

        private Rigidbody2D _rb;
        private BoxCollider2D _collider;
        private Vector2 _direction;
        private float _speed;
        private float _maxDistance;
        private float _traveled;
        private bool _resolved;
        private bool _ghost;
        private float _ghostTime;
        private SpriteRenderer _sr;
        private Color _color;       // 네모(판정 박스)의 지금 색 — 그림이 있으면 그림 색도 여기서 나온다
        private Color _ghostColor;

        // 그림 (근접 베기): 박스의 자식이라 박스 크기 그대로 그려진다 → 보이는 그림 = 판정 범위
        private SpriteRenderer _art;
        private SpriteClip _artClip;
        private const int ArtOrder = 20;
        private const int HitboxOverlayOrder = 21;

        private const float GhostLife = 0.45f;

        /// <summary>회피로 흘려보내져 판정 없이 지나가는 중.</summary>
        public bool IsGhost => _ghost;

        // ── 근접 베기: 몸 앞에 붙은 박스가 사거리 끝까지 한 번에 뻗는다 (날아가는 투사체가 아님) ──
        private bool _slash;
        private Vector2 _anchor;   // 뻗기 시작하는 쪽 끝 (몬스터 몸 앞면)
        private float _length;
        private float _reach;      // 다 뻗었을 때 길이
        private float _hold;       // 다 뻗은 뒤 머무는 시간
        private Color _slashColor;
        private bool _slashSpent;  // 플레이어를 맞힘 — 판정은 꺼졌고 베는 모습만 마저 보여준다
        // 다 뻗은 뒤 판정을 유지한 채 흐려지며 남는 시간 — 너무 짧으면 "베었다"가 눈에 안 남는다
        private const float SlashHoldTime = 0.14f;
        private const float SlashStartLength = 0.05f;

        /// <summary>근접 베기인가 (투사체가 아니라 몸에서 뻗는 박스).</summary>
        public bool IsSlash => _slash;
        /// <summary>근접 베기에서 몬스터 쪽 끝 (고정).</summary>
        public Vector2 SlashAnchor => _anchor;

        /// <summary>
        /// 근접 베기. anchor(몬스터 몸 앞면)에 붙은 채 direction 쪽으로 speed로 뻗어 reach 길이까지 간다.
        /// 텔레그래프(반응시간)는 앞의 예비 모션이 맡고, 베기는 아주 빠르게 뻗는다 (기획서 3.1).
        /// 크기는 localScale로만 바뀌므로 보이는 박스 = 판정 박스.
        /// </summary>
        public static EnemyAttack SpawnSlash(EnemyController source, AttackType type, Vector2 anchor, float direction,
            float height, float speed, float reach, Color color, int damage)
        {
            var dir = new Vector2(Mathf.Sign(direction), 0f);
            var attack = Spawn(source, type, anchor + dir * (SlashStartLength * 0.5f), dir,
                new Vector2(SlashStartLength, height), speed, reach, color, damage);
            attack.gameObject.name = $"EnemySlash_{type}";
            attack._slash = true;
            attack._anchor = anchor;
            attack._length = SlashStartLength;
            attack._reach = reach;
            attack._slashColor = color;
            attack._rb.interpolation = RigidbodyInterpolation2D.None; // 위치·크기를 직접 정하므로
            var fx = GameAssets.AttackFx;
            if (fx != null) attack.AttachArt(fx.EnemySlash(type));
            return attack;
        }

        public static EnemyAttack Spawn(EnemyController source, AttackType type, Vector2 origin, Vector2 direction,
            Vector2 size, float speed, float maxDistance, Color color, int damage)
        {
            var go = Box.Create($"EnemyAttack_{type}", origin, size, color, 20, withCollider: true);
            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var attack = go.AddComponent<EnemyAttack>();
            attack._rb = rb;
            attack._collider = col;
            attack.Source = source;
            attack.Type = type;
            attack.Damage = damage;
            attack._direction = direction.normalized;
            attack._speed = speed;
            attack._maxDistance = maxDistance;
            attack._sr = go.GetComponent<SpriteRenderer>();
            attack._color = color;
            return attack;
        }

        /// <summary>판정 네모 대신 그림을 보여준다. 판정이 살아 있는 동안은 첫 장(꽉 찬 모양)만.</summary>
        private void AttachArt(SpriteClip clip)
        {
            if (clip == null || clip.IsEmpty) return;
            var go = new GameObject("Art");
            go.transform.SetParent(transform, false);
            _art = go.AddComponent<SpriteRenderer>();
            _art.sprite = clip.frames[0];
            _art.flipX = _direction.x < 0f;
            _art.sortingOrder = ArtOrder;
            // 부모 스케일 = 박스 크기 → 그림을 1×1 유닛으로 맞춰 두면 박스와 똑같은 크기
            FxClip.FitToBox(go.transform, clip.frames[0], Vector2.one);
            _artClip = clip;
            ApplyVisual();
        }

        /// <summary>그림 색: 공격색 그대로(박스는 반투명이지만 그림은 불투명하게), 흘려보낸 공격은 회색 반투명.</summary>
        private Color ArtColor => new(_color.r, _color.g, _color.b, _ghost ? _color.a : 1f);

        private void SetColor(Color c)
        {
            _color = c;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (_art == null)
            {
                _sr.color = _color;
                return;
            }
            // 판정 네모: 평소 숨김, 연습 스위치 "판정 상자 보기"로 그림 위에 반투명하게 겹쳐 본다
            bool overlay = GameTuning.Current.showHitboxes;
            _sr.enabled = overlay;
            var box = _color;
            if (overlay) box.a *= 0.45f;
            _sr.color = box;
            _sr.sortingOrder = _ghost ? 4 : HitboxOverlayOrder;
            _art.sortingOrder = _ghost ? 4 : ArtOrder;
            _art.color = ArtColor;
        }

        private void LateUpdate()
        {
            if (_art != null) ApplyVisual();
        }

        /// <summary>판정이 끝난 베기 그림의 나머지(부서지는 장면)를 판정 없이 마저 재생한다.</summary>
        private void PlayArtBreakup()
        {
            if (_art == null || _artClip.frames.Length < 2) return;
            FxClip.Play(_artClip, 1, transform.position, transform.localScale, _art.flipX, ArtColor, ArtOrder);
        }

        private void FixedUpdate()
        {
            if (_resolved && !_slashSpent) return;
            if (_slash)
            {
                ExtendSlash();
                return;
            }
            float step = _speed * Time.fixedDeltaTime;
            _rb.MovePosition(_rb.position + _direction * step);
            _traveled += step;
            if (_traveled >= _maxDistance) Expire();
        }

        private void ExtendSlash()
        {
            if (_length < _reach)
            {
                _length = Mathf.Min(_reach, _length + _speed * Time.fixedDeltaTime);
                var scale = transform.localScale;
                scale.x = _length;
                transform.localScale = scale;
                Vector2 center = _anchor + _direction * (_length * 0.5f);
                transform.position = center;
                _rb.position = center;
                return;
            }

            // 다 뻗음 → 판정을 유지한 채 흐려지다가 사라짐
            _hold += Time.fixedDeltaTime;
            var c = _slashColor;
            c.a *= Mathf.Lerp(1f, 0.35f, _hold / SlashHoldTime);
            SetColor(c);
            if (_hold >= SlashHoldTime) Expire();
        }

        /// <summary>근접 베기의 뻗어 나가는 끝 / 투사체면 중심.</summary>
        private Vector2 Tip => _slash ? _anchor + _direction * _length : (Vector2)transform.position;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_resolved || other.attachedRigidbody == null) return;
            var defense = other.attachedRigidbody.GetComponent<PlayerDefense>();
            if (defense != null) defense.ReceiveAttack(this);
        }

        /// <summary>플레이어와 판정이 끝남. 공격 1개는 한 번만 판정된다 — 이후엔 절대 다시 맞지 않는다.</summary>
        public void Resolve(AttackResolution resolution)
        {
            if (_resolved) return;
            _resolved = true;
            _collider.enabled = false;

            switch (resolution)
            {
                case AttackResolution.Shatter:
                    Fx.Sparks(Tip, _color, 8, 6f, 0.14f);
                    PlayArtBreakup();
                    Destroy(gameObject);
                    break;

                case AttackResolution.PassThrough:
                    // 판정-시각 일치 원칙: 판정이 꺼졌으니 "살아 있는 공격"처럼 보이면 안 된다
                    // → 회색·반투명으로 바꾸고 빠르게 사라지게 해서 위협이 아님을 분명히 한다.
                    _ghost = true;
                    _rb.simulated = false;
                    var c = _color;
                    float grey = (c.r + c.g + c.b) / 3f;
                    _ghostColor = new Color(Mathf.Lerp(grey, 1f, 0.3f), Mathf.Lerp(grey, 1f, 0.3f), Mathf.Lerp(grey, 1f, 0.3f), 0.35f);
                    _sr.sortingOrder = 4; // 플레이어 뒤로
                    SetColor(_ghostColor);
                    break;

                default:
                    // 베기는 맞힌 순간 사라지면 휘두른 게 안 보인다 → 판정만 끄고 끝까지 베는 모습은 보여준다
                    if (_slash) _slashSpent = true;
                    else Destroy(gameObject);
                    break;
            }
        }

        private void Update()
        {
            if (!_ghost) return;
            // 흘려보낸 공격은 원래 방향·속도로 계속 지나가며 사라진다 (게임 시간 기준 → 회피 슬로우 중엔 천천히).
            // 근접 베기는 몬스터 몸에 붙은 것이라 제자리에서 흐려진다.
            if (!_slash) transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
            _ghostTime += Time.deltaTime;
            var c = _ghostColor;
            c.a *= 1f - Mathf.Clamp01(_ghostTime / GhostLife);
            SetColor(c);
            if (_ghostTime >= GhostLife) Destroy(gameObject);
        }

        private void Expire()
        {
            _resolved = true;
            _collider.enabled = false;
            if (_art != null) PlayArtBreakup();
            else if (_slash) Fx.Flash(Tip, 0.5f, 0.2f, _color, 0.08f);
            else Fx.Flash(transform.position, transform.localScale.x, transform.localScale.x * 0.5f, _color, 0.1f);
            Destroy(gameObject);
        }
    }
}
