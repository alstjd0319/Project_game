using UnityEngine;

namespace ParryRL
{
    /// <summary>SpriteRenderer에 <see cref="SpriteClip"/>을 반복 재생한다 (포탈·NPC처럼 판정 없는 그림용). 게임 시간 기준.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ClipPlayer : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private SpriteClip _clip;
        private float _t;

        public SpriteClip Clip => _clip;
        public int Frame { get; private set; }

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        /// <summary>재생할 그림을 바꾼다 (같은 그림이면 이어서 재생).</summary>
        public void Play(SpriteClip clip)
        {
            if (clip == null || clip.IsEmpty || clip == _clip) return;
            _clip = clip;
            _t = 0f;
            Apply();
        }

        public void Flip(bool flipX)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _sr.flipX = flipX;
        }

        private void Update()
        {
            if (_clip == null || _clip.IsEmpty) return;
            _t += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            Frame = (int)(_t * Mathf.Max(1f, _clip.fps)) % _clip.frames.Length;
            _sr.sprite = _clip.frames[Frame];
        }
    }
}
