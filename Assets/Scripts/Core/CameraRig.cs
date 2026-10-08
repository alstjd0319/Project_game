using UnityEngine;

namespace ParryRL
{
    /// <summary>플레이어 좌우 추적 + 화면 흔들림 (unscaled time — 히트스탑 중에도 재생).</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] private Transform target;
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private float fixedY = 3.5f;
        [SerializeField] private float arenaMinX = -22f;
        [SerializeField] private float arenaMaxX = 22f;

        private Camera _cam;
        private Vector3 _basePos;
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeTimer;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _basePos = transform.position;
        }

        public void Shake(float amplitude, float duration)
        {
            // 진행 중인 흔들림보다 약하면 무시
            float remaining = _shakeDuration > 0f ? _shakeAmplitude * (_shakeTimer / _shakeDuration) : 0f;
            if (amplitude < remaining) return;
            _shakeAmplitude = amplitude;
            _shakeDuration = duration;
            _shakeTimer = duration;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (target != null)
            {
                float halfWidth = _cam.orthographicSize * _cam.aspect;
                float x = Mathf.Clamp(target.position.x, arenaMinX + halfWidth, arenaMaxX - halfWidth);
                if (arenaMaxX - arenaMinX < halfWidth * 2f) x = (arenaMinX + arenaMaxX) * 0.5f;
                float t = 1f - Mathf.Exp(-followSharpness * dt);
                _basePos = new Vector3(Mathf.Lerp(_basePos.x, x, t), fixedY, _basePos.z);
            }

            Vector3 offset = Vector3.zero;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= dt;
                float strength = _shakeAmplitude * Mathf.Clamp01(_shakeTimer / _shakeDuration);
                offset = (Vector3)(Random.insideUnitCircle * strength);
            }

            transform.position = _basePos + offset;
        }
    }
}
