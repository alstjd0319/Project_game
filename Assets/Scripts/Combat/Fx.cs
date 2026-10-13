using UnityEngine;

namespace ParryRL
{
    /// <summary>이펙트용 둥근 그림 (코드로 한 번만 생성): 부드러운 빛덩이 / 얇은 고리.</summary>
    public static class FxSprites
    {
        private static Sprite _glow, _ring;

        public static Sprite Glow => _glow != null ? _glow : _glow = Make(false);
        public static Sprite Ring => _ring != null ? _ring : _ring = Make(true);

        private static Sprite Make(bool ring)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a;
                    if (ring) a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.12f);          // 가장자리 근처 얇은 고리
                    else a = d >= 1f ? 0f : Mathf.Pow(1f - d, 1.6f) * (d < 0.35f ? 1f : 0.85f); // 중심이 밝고 가장자리로 부드럽게
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }

    /// <summary>부드러운 조각으로 만드는 이펙트. 판정 없음(콜라이더 없음), 실시간 기준으로 재생.</summary>
    public static class Fx
    {
        private const int FxOrder = 50;

        /// <summary>사방으로 튀는 불꽃 조각.</summary>
        public static void Sparks(Vector2 pos, Color color, int count, float speed, float size = 0.12f, float life = 0.35f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float s = size * Random.Range(0.6f, 1.3f);
                var go = Box.Create("Fx_Spark", pos, new Vector2(s * 2.4f, s * 0.8f), color, FxOrder);
                go.GetComponent<SpriteRenderer>().sprite = FxSprites.Glow;
                go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                go.AddComponent<FxPiece>().Init(dir * speed * Random.Range(0.5f, 1.2f), life * Random.Range(0.7f, 1.2f),
                    1f, 0.2f, 8f, 6f);
            }
        }

        /// <summary>커졌다가 사라지는 둥근 섬광.</summary>
        public static void Flash(Vector2 pos, float startSize, float endSize, Color color, float life = 0.12f)
        {
            var go = Box.Create("Fx_Flash", pos, Vector2.one * startSize, color, FxOrder - 1);
            go.GetComponent<SpriteRenderer>().sprite = FxSprites.Glow;
            go.AddComponent<FxPiece>().Init(Vector2.zero, life, 1f, endSize / startSize, 0f, 0f);
        }

        /// <summary>퍼져나가는 둥근 고리(충격파).</summary>
        public static void Ring(Vector2 pos, Color color, float startSize, float endSize, float thickness, float life)
        {
            var go = new GameObject("Fx_Ring");
            go.transform.position = pos;
            go.AddComponent<FxRing>().Init(color, startSize, endSize, thickness, life, FxOrder);
        }

        /// <summary>한 방향으로 스쳐 지나가는 가는 줄기 (회피의 "휙").</summary>
        public static void Streaks(Vector2 pos, Vector2 direction, Color color, int count, float speed = 14f, float life = 0.22f)
        {
            Vector2 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = new Vector2(-dir.y, dir.x) * Random.Range(-0.7f, 0.7f) - dir * Random.Range(0f, 0.6f);
                var go = Box.Create("Fx_Streak", pos + offset, new Vector2(Random.Range(0.4f, 0.9f), 0.05f), color, FxOrder);
                go.transform.rotation = Quaternion.Euler(0, 0, angle);
                go.AddComponent<FxPiece>().Init(dir * speed * Random.Range(0.7f, 1.2f), life * Random.Range(0.8f, 1.2f), 1f, 1.4f, 3f, 0f);
            }
        }

        /// <summary>제자리에 남는 잔상(대시 등).</summary>
        public static void Afterimage(Vector2 pos, Vector2 size, Color color, float life = 0.18f, Sprite sprite = null, bool flipX = false)
        {
            color.a *= 0.5f;
            var go = Box.Create("Fx_Afterimage", pos, size, color, 5);
            if (sprite != null)
            {
                // 도트 아트 캐릭터의 잔상: 몸 크기(1×1 × 스케일)에 맞춰 늘려 그린다
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = Vector2.one;
                sr.flipX = flipX;
            }
            go.AddComponent<FxPiece>().Init(Vector2.zero, life, 1f, 1f, 0f, 0f);
        }

        /// <summary>도트 그림 모양 잔상 (그림이 있는 캐릭터의 대시).</summary>
        public static void SpriteAfterimage(Sprite sprite, Vector2 pos, Vector3 scale, bool flipX, Color color, float life = 0.18f)
        {
            var go = new GameObject("Fx_Afterimage");
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.color = color;
            sr.sortingOrder = 5;
            go.AddComponent<FxPiece>().Init(Vector2.zero, life, 1f, 1f, 0f, 0f);
        }
    }

    public class FxPiece : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Vector2 _velocity;
        private float _life;
        private float _t;
        private Vector3 _startScale;
        private float _endScaleMul;
        private float _drag;
        private float _gravity;
        private Color _color;

        public void Init(Vector2 velocity, float life, float startScaleMul, float endScaleMul, float drag, float gravity)
        {
            _sr = GetComponent<SpriteRenderer>();
            _color = _sr.color;
            _velocity = velocity;
            _life = Mathf.Max(0.01f, life);
            _startScale = transform.localScale * startScaleMul;
            _endScaleMul = endScaleMul;
            _drag = drag;
            _gravity = gravity;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            float k = _t / _life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            _velocity *= Mathf.Exp(-_drag * dt);
            _velocity.y -= _gravity * dt;
            transform.position += (Vector3)(_velocity * dt);
            transform.localScale = _startScale * Mathf.Lerp(1f, _endScaleMul, k);
            var c = _color;
            c.a *= 1f - k * k;
            _sr.color = c;
        }
    }

    public class FxRing : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Color _color;
        private float _start, _end, _life, _t;

        public void Init(Color color, float startSize, float endSize, float thickness, float life, int order)
        {
            _color = color;
            _start = startSize;
            _end = endSize;
            _life = Mathf.Max(0.01f, life);
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = FxSprites.Ring;
            _sr.sortingOrder = order;
            Layout(0f);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = _t / _life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Layout(k);
        }

        private void Layout(float k)
        {
            float eased = 1f - (1f - k) * (1f - k);
            float size = Mathf.Lerp(_start, _end, eased);
            transform.localScale = new Vector3(size, size, 1f);
            var c = _color;
            c.a *= 1f - k;
            _sr.color = c;
        }
    }
}
