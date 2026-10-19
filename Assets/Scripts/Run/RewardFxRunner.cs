using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>재화 하나의 움직임: 튀어 오름 → 떨어지며 멈칫 → 반짝이며 사라짐 (unscaled time).</summary>
    public class RewardFxRunner : MonoBehaviour
    {
        private const float Gravity = 9f, Life = 0.75f;
        private Vector2 _velocity;
        private SpriteClip _pickup;
        private float _delay;

        public void Begin(Vector2 velocity, SpriteClip pickup, float delay)
        {
            _velocity = velocity;
            _pickup = pickup;
            _delay = delay;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (_delay > 0f)
            {
                sr.enabled = false;
                yield return new WaitForSecondsRealtime(_delay);
                sr.enabled = true;
            }
            for (float t = 0f; t < Life; t += Time.unscaledDeltaTime)
            {
                _velocity.y -= Gravity * Time.unscaledDeltaTime;
                _velocity *= 1f - 2.5f * Time.unscaledDeltaTime;
                transform.position += (Vector3)(_velocity * Time.unscaledDeltaTime);
                yield return null;
            }
            if (_pickup != null && !_pickup.IsEmpty) FxClip.PlayAt(_pickup, transform.position, transform.localScale.x * 1.4f, false, 48);
            Destroy(gameObject);
        }
    }
}
