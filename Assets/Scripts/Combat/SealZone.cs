using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 부적 스킬 "봉인진": 바닥에 깔리는 판정 영역. 지속 시간 동안 안에 있는 적을 계속 기절시키고(묶는다),
    /// 일정 간격마다 약한 데미지를 준다. 판정 상자(박스) = 그림의 진 범위. 그림은 시작 → 반복 → 끝 (1016 봉인진).
    /// 에셋 그림은 비스듬히 내려다본 둥근 진이라 옆에서 보는 이 게임에선 "위에서 보는" 느낌이 난다 →
    /// 진(타원)과 빛기둥을 둘로 나눠, 진은 세로로 납작하게 눌러 <b>바닥에 깔린 얇은 원</b>으로, 빛기둥은 그 위로 곧게 솟게 그린다.
    /// </summary>
    public class SealZone : MonoBehaviour
    {
        private const float BindInterval = 0.2f;
        private const float BindSeconds = 0.35f;
        private const float ArtWidthFactor = 1.15f; // 그림 폭 = 판정 폭 × 이 값 (빛기둥·가장자리가 판정 밖으로 조금 나오게)

        private Vector2 _size;
        private float _duration;
        private float _tickInterval;
        private int _tickDamage;
        private HitInfo _hit;
        private SpriteClip _start, _loop, _end;
        private SpriteRenderer _ring, _pillars;   // 바닥에 깔린 진 / 그 위로 솟는 빛기둥
        private SpriteRenderer _glow;
        private GameObject _holder;
        private static readonly Dictionary<Sprite, (Sprite ring, Sprite pillars)> Split = new();

        // 에셋 그림(세로 비율): 진 위쪽 가장자리 · 진 중심선 · 기둥이 서는 선
        private const float RingTop = 0.506f, RingCenter = 0.706f;
        private const float RingFlatten = 0.4f;     // 진을 세로로 납작하게 (1 = 원래 비스듬한 모양)
        private const float PillarHeight = 1.5f;    // 빛기둥 층의 세로 배율 (아래 끝은 흐려져 바닥에서 솟아난다)
        private float _t;
        private float _nextBind;
        private float _nextTick;

        private static readonly List<EnemyController> Inside = new();

        public float TimeLeft => Mathf.Max(0f, _duration - _t);

        /// <param name="groundCenter">진 가운데 바닥 좌표</param>
        /// <param name="start">진이 펼쳐지는 장면(한 번) · <paramref name="loop"/> 지속 동안 반복 · <paramref name="end"/> 사라지는 장면(한 번)</param>
        public static SealZone Spawn(Vector2 groundCenter, Vector2 size, float duration, float tickInterval, int tickDamage,
            HitInfo hit, SpriteClip start, SpriteClip loop, SpriteClip end)
        {
            var go = Box.Create("SealZone", new Vector2(groundCenter.x, groundCenter.y + size.y * 0.5f), size,
                new Color(1f, 0.8f, 0.3f, 0.35f), 7);
            var z = go.AddComponent<SealZone>();
            z._size = size;
            z._duration = duration;
            z._tickInterval = tickInterval;
            z._tickDamage = tickDamage;
            z._hit = hit;
            z._start = start; z._loop = loop; z._end = end;
            bool hasArt = start != null && !start.IsEmpty && loop != null && !loop.IsEmpty && end != null && !end.IsEmpty;
            var box = go.GetComponent<SpriteRenderer>();
            box.enabled = GameTuning.Current.showHitboxes || !hasArt;
            if (hasArt)
            {
                // 판정 폭에 맞춰 키우고, 진의 중심선이 바닥(판정 아래 끝)에 오게 한다
                float scale = size.x * ArtWidthFactor / start.frames[0].bounds.size.x;
                z._holder = new GameObject("SealArt");
                z._holder.transform.position = new Vector3(groundCenter.x, groundCenter.y + 0.02f, 0f);
                z._ring = NewLayer(z._holder.transform, "Ring", 6, new Vector3(scale, scale * RingFlatten, 1f));
                z._pillars = NewLayer(z._holder.transform, "Pillars", 7, new Vector3(scale, scale * PillarHeight, 1f)); // 가로는 진 폭 그대로, 세로로 길게 (빛기둥이 솟는 느낌)
                // 바닥에 번지는 은은한 금빛 (진이 땅을 비춘다)
                z._glow = NewLayer(z._holder.transform, "GroundGlow", 5, new Vector3(size.x * 1.3f, 0.55f, 1f));
                z._glow.sprite = FxSprites.Glow;
                z._glow.color = new Color(1f, 0.78f, 0.3f, 0.4f);
                z.ApplySprite(start.frames[0]);
            }
            return z;
        }

        private static SpriteRenderer NewLayer(Transform parent, string name, int order, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        private void OnDestroy()
        {
            if (_holder != null) Destroy(_holder);
        }

        /// <summary>
        /// 한 장면을 진(아래쪽)과 빛기둥(위쪽)으로 나눠 각 층에 넣는다. 나눈 그림은 한 번 만들어 재사용한다.
        /// 진 층은 위쪽 가장자리~아래 끝(뒤쪽 호도 함께 눌려 하나의 납작한 진이 된다).
        /// 빛기둥 층은 맨 위~진 위쪽 가장자리까지만 쓰고 아래 끝을 부드럽게 흐려, 눌리지 않은 뒤쪽 호가 기둥 사이에 비치지 않고 바닥에서 솟아나게 한다.
        /// </summary>
        private void ApplySprite(Sprite frame)
        {
            if (!Split.TryGetValue(frame, out var parts) || parts.ring == null)
            {
                Rect r = frame.rect;
                float h = r.height;
                var ringRect = new Rect(r.x, r.y, r.width, h * (1f - RingTop));
                float ringPivotY = 1f - (RingCenter - RingTop) / (1f - RingTop);
                Sprite ring = Sprite.Create(frame.texture, ringRect, new Vector2(0.5f, ringPivotY), frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                Sprite pillars;
                try { pillars = MakePillars(frame, h * RingTop); }
                catch (UnityException) // 읽을 수 없는 텍스처(빌더가 읽기 가능으로 임포트한다) — 흐리지 않고 잘라 쓴다
                {
                    var rect = new Rect(r.x, r.y + h * (1f - RingTop), r.width, h * RingTop);
                    pillars = Sprite.Create(frame.texture, rect, new Vector2(0.5f, 0f), frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
                parts = (ring, pillars);
                Split[frame] = parts;
            }
            _ring.sprite = parts.ring;
            _pillars.sprite = parts.pillars;
        }

        private static Sprite MakePillars(Sprite frame, float heightPx)
        {
            Rect r = frame.rect;
            int w = (int)r.width, ph = Mathf.RoundToInt(heightPx);
            int srcY = (int)r.y + (int)r.height - ph;   // 그림 맨 위 ph줄 (텍스처 y는 아래가 0)
            var px = frame.texture.GetPixels32();
            int texW = frame.texture.width;
            var dst = new Color32[w * ph];
            int fade = Mathf.Max(1, Mathf.RoundToInt(ph * 0.22f));   // 아래 끝 22%를 서서히 투명하게
            for (int y = 0; y < ph; y++)
            {
                float k = y >= fade ? 1f : (float)y / fade;
                k = k * k * (3f - 2f * k);
                for (int x = 0; x < w; x++)
                {
                    var c = px[(srcY + y) * texW + (int)r.x + x];
                    c.a = (byte)(c.a * k);
                    dst[y * w + x] = c;
                }
            }
            var tex = new Texture2D(w, ph, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(dst);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, ph), new Vector2(0.5f, 0f), frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _duration)
            {
                Destroy(gameObject);
                return;
            }
            UpdateArt();

            if (_t >= _nextBind || _t >= _nextTick)
            {
                Physics2D.SyncTransforms();
                Inside.Clear();
                foreach (var col in Physics2D.OverlapBoxAll(transform.position, _size, 0f))
                {
                    var e = col.attachedRigidbody != null
                        ? col.attachedRigidbody.GetComponent<EnemyController>()
                        : col.GetComponent<EnemyController>();
                    if (e != null && !e.IsDead && !Inside.Contains(e)) Inside.Add(e);
                }
                if (_t >= _nextBind)
                {
                    _nextBind = _t + BindInterval;
                    foreach (var e in Inside) e.Stun(BindSeconds);
                }
                if (_t >= _nextTick)
                {
                    _nextTick = _t + _tickInterval;
                    foreach (var e in Inside)
                        if (!e.IsDead) DamageCalc.Apply(_tickDamage, _hit, e, 0f); // 방향 0 = 밀치지 않는다 (진 안에 묶어 둔다)
                }
            }
        }

        private void UpdateArt()
        {
            if (_ring == null) return;
            if (_glow != null)
            {
                float fadeIn = Mathf.Clamp01(_t / 0.3f), fadeOut = Mathf.Clamp01((_duration - _t) / 0.4f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(_t * 5f);
                var gc = _glow.color; gc.a = 0.4f * pulse * Mathf.Min(fadeIn, fadeOut); _glow.color = gc;
            }
            // 시작(한 번) → 반복(지속 동안) → 끝(한 번): 끝은 지속 시간이 다하기 직전에 맞춰 시작한다
            float startTime = _start.frames.Length / Mathf.Max(1f, _start.fps);
            float endTime = _end.frames.Length / Mathf.Max(1f, _end.fps);
            float left = _duration - _t;
            SpriteClip clip;
            int frame;
            if (_t < startTime)
            {
                clip = _start;
                frame = (int)(_t * _start.fps);
            }
            else if (left <= endTime)
            {
                clip = _end;
                frame = (int)((endTime - left) * _end.fps);
            }
            else
            {
                clip = _loop;
                frame = (int)((_t - startTime) * _loop.fps) % _loop.frames.Length;
            }
            ApplySprite(clip.frames[Mathf.Clamp(frame, 0, clip.frames.Length - 1)]);
        }
    }
}
