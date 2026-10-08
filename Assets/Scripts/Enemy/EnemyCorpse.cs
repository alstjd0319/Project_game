using UnityEngine;

namespace ParryRL
{
    /// <summary>죽은 몬스터의 쓰러지는 그림 (판정 없음). 한 번 재생하고 마지막 장면에서 잠깐 머문 뒤 흐려지며 사라진다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyCorpse : MonoBehaviour
    {
        private const float Hold = 0.4f;
        private const float FadeTime = 0.4f;

        private SpriteRenderer _sr;
        private SpriteClip _clip;
        private float _time;

        public int Frame { get; private set; }

        public void Play(SpriteClip clip)
        {
            _sr = GetComponent<SpriteRenderer>();
            _clip = clip;
            _time = 0f;
            _sr.sprite = clip.frames[0];
        }

        private void Update()
        {
            if (_clip == null || _clip.IsEmpty) { Destroy(gameObject); return; }
            _time += Time.deltaTime;
            int n = _clip.frames.Length;
            Frame = Mathf.Min(n - 1, (int)(_time * _clip.fps));
            _sr.sprite = _clip.frames[Frame];

            float fade = _time - n / _clip.fps - Hold;
            if (fade > 0f) _sr.color = new Color(1f, 1f, 1f, 1f - fade / FadeTime);
            if (fade >= FadeTime) Destroy(gameObject);
        }
    }
}
