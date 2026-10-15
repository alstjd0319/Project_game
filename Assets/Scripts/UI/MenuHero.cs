using UnityEngine;

namespace ParryRL
{
    /// <summary>메뉴 화면 주인공: 대기 그림을 반복 재생한다.</summary>
    public class MenuHero : MonoBehaviour
    {
        private SpriteClip _clip;
        private SpriteRenderer _sr;
        private float _t;

        public void Init(SpriteClip clip)
        {
            _clip = clip;
            _sr = GetComponent<SpriteRenderer>();
            if (clip != null && !clip.IsEmpty) _sr.sprite = clip.frames[0];
        }

        private void Update()
        {
            if (_clip == null || _clip.IsEmpty) return;
            _t += Time.unscaledDeltaTime;
            _sr.sprite = _clip.frames[(int)(_t * _clip.fps) % _clip.frames.Length];
        }
    }
}
