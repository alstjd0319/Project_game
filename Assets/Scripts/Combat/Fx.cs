using UnityEngine;

namespace ParryRL
{
    /// <summary>네모 조각으로 만드는 이펙트. 판정 없음(콜라이더 없음), 실시간 기준으로 재생.</summary>
    public static class Fx
    {
        private const int FxOrder = 50;

        /// <summary>사방으로 튀는 네모 파편.</summary>
        public static void Sparks(Vector2 pos, Color color, int count, float speed, float size = 0.12f, float life = 0.35f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float s = size * Random.Range(0.6f, 1.3f);
                var go = Box.Create("Fx_Spark", pos, new Vector2(s, s), color, FxOrder);
                go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 90f));
                go.AddComponent<FxPiece>().Init(dir * speed * Random.Range(0.5f, 1.2f), life * Random.Range(0.7f, 1.2f),
                    1f, 0.2f, 8f, 6f);
            }
        }

        /// <summary>커졌다가 사라지는 네모 섬광.</summary>
        public static void Flash(Vector2 pos, float startSize, float endSize, Color color, float life = 0.12f)
        {
            var go = Box.Create("Fx_Flash", pos, Vector2.one * startSize, color, FxOrder - 1);
            go.AddComponent<FxPiece>().Init(Vector2.zero, life, 1f, endSize / startSize, 0f, 0f);
        }

        /// <summary>퍼져나가는 네모 테두리(충격파).</summary>
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
        private readonly SpriteRenderer[] _edges = new SpriteRenderer[4];
        private Color _color;
        private float _start, _end, _thickness, _life, _t;

        public void Init(Color color, float startSize, float endSize, float thickness, float life, int order)
        {
            _color = color;
            _start = startSize;
            _end = endSize;
            _thickness = thickness;
            _life = Mathf.Max(0.01f, life);
            for (int i = 0; i < 4; i++)
            {
                var edge = Box.Create("Edge", transform.position, Vector2.one, color, order);
                edge.transform.SetParent(transform, true);
                _edges[i] = edge.GetComponent<SpriteRenderer>();
            }
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
            float th = _thickness * (1f - k * 0.7f);
            float half = size * 0.5f;
            var c = _color;
            c.a *= 1f - k;

            SetEdge(0, new Vector2(0, half), new Vector2(size, th), c);
            SetEdge(1, new Vector2(0, -half), new Vector2(size, th), c);
            SetEdge(2, new Vector2(-half, 0), new Vector2(th, size), c);
            SetEdge(3, new Vector2(half, 0), new Vector2(th, size), c);
        }

        private void SetEdge(int i, Vector2 localPos, Vector2 size, Color c)
        {
            var t = _edges[i].transform;
            t.localPosition = localPos;
            t.localScale = new Vector3(size.x, size.y, 1f);
            _edges[i].color = c;
        }
    }
}
