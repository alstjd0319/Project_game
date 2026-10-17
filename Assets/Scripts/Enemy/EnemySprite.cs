using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 몬스터의 도트 그림을 판정 네모 위에 그린다. 판정은 여전히 네모(BoxCollider2D)이고 그림은 자식 오브젝트라 판정에 영향이 없다.
    /// 그림이 있으면 네모를 숨기고(연습 스위치 "판정 상자 보기"로 겹쳐 보기), 베기 판정 박스는 그대로 보인다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(EnemyController))]
    public class EnemySprite : MonoBehaviour
    {
        private const int HitboxOverlayOrder = 9;

        [SerializeField] private SpriteRenderer artRenderer;
        [SerializeField] private EnemyArt art;

        /// <summary>지금 재생 중인 동작 이름 (그림이 없으면 빈 문자열). 테스트·디버그용.</summary>
        public string Clip { get; private set; } = "";
        public int Frame { get; private set; }
        public bool IsShowingArt => artRenderer != null && artRenderer.enabled;
        public SpriteRenderer ArtRenderer => artRenderer;
        public EnemyArt Art => art;

        private EnemyController _enemy;
        private SpriteRenderer _box;
        private int _boxOrder;
        private string _lastClip = "";
        private float _clipTime;

        private void Awake()
        {
            _enemy = GetComponent<EnemyController>();
            _box = GetComponent<SpriteRenderer>();
            _boxOrder = _box.sortingOrder;
        }

        private void OnEnable()
        {
            _enemy.Died += OnDied;
            _enemy.Staggered += OnStaggered;
        }

        private void OnDisable()
        {
            _enemy.Died -= OnDied;
            _enemy.Staggered -= OnStaggered;
        }

        // 연속으로 맞으면 피격 그림을 처음부터
        private void OnStaggered() => _clipTime = 0f;

        private SpriteClip _override;
        private float _overrideTime;

        /// <summary>그림 묶음을 바꾼다 (보스 2페이즈 모습).</summary>
        public void SetArt(EnemyArt newArt)
        {
            art = newArt;
            _lastClip = "";
        }

        /// <summary>이 동작을 처음부터 끝까지 한 번 재생하는 동안 다른 동작보다 앞선다 (보스 변신). 길이(초)를 돌려준다.</summary>
        public float PlayOnce(SpriteClip clip) => Play(clip, false);

        /// <summary>이 동작을 <see cref="StopOverride"/>할 때까지 반복한다 (보스 돌진).</summary>
        public void PlayLoop(SpriteClip clip) => Play(clip, true);

        public void StopOverride()
        {
            _override = null;
            _lastClip = "";
        }

        private bool _overrideLoop;

        private float Play(SpriteClip clip, bool loop)
        {
            if (clip == null || clip.IsEmpty) return 0f;
            _override = clip;
            _overrideLoop = loop;
            _overrideTime = 0f;
            return clip.frames.Length / Mathf.Max(0.1f, clip.fps);
        }

        public bool IsPlayingOnce => _override != null;

        private void LateUpdate()
        {
            _clipTime += Time.deltaTime; // 히트스탑 동안 그림도 멈춘다
            if (_override != null && artRenderer != null)
            {
                _overrideTime += Time.deltaTime;
                int f = (int)(_overrideTime * _override.fps);
                if (_overrideLoop) f %= _override.frames.Length;
                if (f < _override.frames.Length)
                {
                    artRenderer.enabled = true;
                    _box.enabled = GameTuning.Current.showHitboxes;
                    artRenderer.sprite = _override.frames[f];
                    artRenderer.flipX = _enemy.Facing < 0f;
                    artRenderer.color = Color.white;
                    Clip = "once";
                    Frame = f;
                    return;
                }
                _override = null;
                _lastClip = "";
            }

            if (art == null || artRenderer == null)
            {
                if (artRenderer != null) artRenderer.enabled = false;
                _box.enabled = true;
                _box.sortingOrder = _boxOrder;
                Clip = "";
                return;
            }

            artRenderer.enabled = true;
            bool overlay = GameTuning.Current.showHitboxes;
            _box.enabled = overlay;
            if (overlay)
            {
                var c = _box.color;
                c.a *= 0.45f;
                _box.color = c;
                _box.sortingOrder = HitboxOverlayOrder;
            }

            string clip = PickClip(out SpriteClip source);
            if (clip != _lastClip)
            {
                _lastClip = clip;
                _clipTime = 0f;
            }
            int frame = FrameOf(clip, source);

            Clip = clip;
            Frame = frame;
            artRenderer.sprite = source.IsEmpty ? null : source.frames[frame];
            artRenderer.flipX = _enemy.Facing < 0f;
            artRenderer.color = _enemy.ArtTint;
        }

        private string PickClip(out SpriteClip source)
        {
            if (_enemy.IsStunned && !art.stun.IsEmpty) { source = art.stun; return "stun"; }
            if (_enemy.IsStaggered && !art.hit.IsEmpty) { source = art.hit; return "hit"; }
            if (_enemy.IsComboAttack && !art.combo.IsEmpty && (_enemy.IsWindingUp || _enemy.IsRecovering))
            {
                source = art.combo;
                return "combo" + _enemy.ComboStep; // 타격마다 이름이 바뀌어 장면 시간이 처음부터
            }
            if (_enemy.IsWindingUp)
            {
                source = _enemy.IsHeavyAttack ? art.heavyWindup : art.windup;
                if (!source.IsEmpty) return _enemy.IsHeavyAttack ? "heavyWindup" : "windup";
            }
            if (_enemy.IsRecovering)
            {
                source = _enemy.IsHeavyAttack ? art.heavySlash : art.slash;
                if (!source.IsEmpty) return _enemy.IsHeavyAttack ? "heavySlash" : "slash";
            }
            if (_enemy.IsMoving && !art.walk.IsEmpty) { source = art.walk; return "walk"; }
            source = art.idle;
            return "idle";
        }

        private int FrameOf(string clip, SpriteClip source)
        {
            if (source.IsEmpty) return 0;
            int n = source.frames.Length;
            if (clip.StartsWith("combo"))
            {
                // 연속 공격: 첫 예비 모션은 첫 타격 장면 앞까지 진행도에 맞춰, 그 뒤엔 방금 친 타격 장면, 끝나면 마지막 장면까지
                var hits = art.comboHitFrames;
                int step = _enemy.ComboStep;
                if (hits == null || hits.Length == 0) return 0;
                if (step == 0) return Mathf.Clamp((int)(_enemy.WindupProgress * hits[0]), 0, Mathf.Max(0, hits[0] - 1));
                int last = Mathf.Clamp(hits[Mathf.Min(step, hits.Length) - 1], 0, n - 1);
                if (_enemy.IsRecovering) return Mathf.Min(n - 1, last + (int)(_clipTime * source.fps));
                return last;
            }
            switch (clip)
            {
                case "windup":
                case "heavyWindup":
                    // 예비 모션 길이(= 반응시간)에 맞춰 한 번 — 마지막 장면에서 베기가 나간다
                    return Mathf.Min(n - 1, (int)(_enemy.WindupProgress * n));
                case "slash":
                case "heavySlash":
                    // 베기 박스는 예비 모션 끝에 한 번에 뻗으므로 칼을 뻗은 장면부터, 끝나면 회복 끝까지 멈춤
                    int start = Mathf.Clamp(clip == "slash" ? art.slashHitFrame : art.heavySlashHitFrame, 0, n - 1);
                    return Mathf.Min(n - 1, start + (int)(_clipTime * source.fps));
                case "hit":
                    return Mathf.Min(n - 1, (int)(_clipTime * source.fps));
                default:
                    return (int)(_clipTime * source.fps) % n;
            }
        }

        /// <summary>몬스터 오브젝트는 죽는 즉시 사라지므로, 쓰러지는 그림은 따로 남겨 재생한다 (판정 없음).</summary>
        private void OnDied(EnemyController _)
        {
            if (art == null || artRenderer == null || art.death.IsEmpty) return;
            var go = new GameObject("EnemyCorpse");
            go.transform.position = artRenderer.transform.position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = artRenderer.sortingOrder;
            sr.flipX = artRenderer.flipX;
            go.AddComponent<EnemyCorpse>().Play(art.death);
        }
    }
}
