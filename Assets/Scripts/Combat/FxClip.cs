using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 판정이 끝난 공격의 그림을 마저 재생하는 이펙트 (판정 없음). 박스와 같은 자리·크기로 그리고,
    /// 마지막 장면에서 흐려지며 사라진다. 연출이라 실시간 기준.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FxClip : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private SpriteClip _clip;
        private int _start;
        private float _t;
        private Color _color;

        public int Frame { get; private set; }
        public SpriteRenderer Renderer => _sr;

        /// <summary>
        /// clip의 startFrame부터 끝까지 재생. center·size는 판정 박스 그대로 (그림을 박스 크기로 늘린다).
        /// </summary>
        public static FxClip Play(SpriteClip clip, int startFrame, Vector2 center, Vector2 size, bool flipX, Color color, int order, bool uniform = false)
        {
            var go = new GameObject("Fx_Clip");
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.flipX = flipX;
            sr.sortingOrder = order;
            FitToBox(go.transform, clip.frames[0], size, uniform);
            var fx = go.AddComponent<FxClip>();
            fx._sr = sr;
            fx._clip = clip;
            fx._start = Mathf.Clamp(startFrame, 0, clip.frames.Length - 1);
            fx._color = color;
            fx.Frame = fx._start;
            sr.sprite = clip.frames[fx._start];
            sr.color = color;
            return fx;
        }

        /// <summary>그림(가운데 피벗)이 정확히 size(유닛) 크기로 보이도록 스케일을 맞춘다.</summary>
        public static void FitToBox(Transform t, Sprite sprite, Vector2 size, bool uniform = false)
        {
            Vector2 s = sprite.bounds.size;
            if (uniform)
            {
                // 비율을 유지한 채 박스 안에 들어가게 (정사각 칸에 그린 이펙트를 늘리면 일그러지므로)
                float k = Mathf.Min(size.x / s.x, size.y / s.y);
                t.localScale = new Vector3(k, k, 1f);
                return;
            }
            t.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f);
        }

        /// <summary>
        /// 연출용 그림 재생 (판정 없음): 원본 크기 × scale로 pos에 그리고 끝까지 재생한 뒤 사라진다. 그림이 없으면 아무것도 안 한다.
        /// </summary>
        public static FxClip PlayAt(SpriteClip clip, Vector2 pos, float scale = 1f, bool flipX = false, int order = 45, Color? tint = null)
        {
            if (clip == null || clip.IsEmpty) return null;
            var fx = Play(clip, 0, pos, Vector2.one, flipX, tint ?? Color.white, order);
            fx.transform.localScale = new Vector3(scale, scale, 1f);
            return fx;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float frameTime = 1f / Mathf.Max(1f, _clip.fps);
            int n = _clip.frames.Length;
            int f = _start + (int)(_t / frameTime);
            if (f >= n)
            {
                Destroy(gameObject);
                return;
            }
            Frame = f;
            _sr.sprite = _clip.frames[f];
            // 마지막 장면 동안 흐려짐
            float last = (n - _start - 1) * frameTime;
            var c = _color;
            if (_t > last) c.a *= 1f - Mathf.Clamp01((_t - last) / frameTime);
            _sr.color = c;
        }
    }
}
