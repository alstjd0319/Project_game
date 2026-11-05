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
        private bool _followY;
        private float _maxY;
        private float _standY;      // 플레이어가 마지막으로 서 있던 발 높이 (점프만으로는 카메라가 출렁이지 않게)
        private PlayerMotor _motor;
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeTimer;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _basePos = transform.position;
        }

        public float ArenaMinX => arenaMinX;
        public float ArenaMaxX => arenaMaxX;

        /// <summary>카메라가 멈추는 좌우 끝 (= 벽 안쪽 면). 방 길이가 바뀌면 바꾼다.</summary>
        public void SetArena(float minX, float maxX)
        {
            arenaMinX = minX;
            arenaMaxX = maxX;
        }

        /// <summary>위아래로 넓은 맵: 플레이어가 선 높이를 따라 카메라가 오르내린다 (바닥에 서면 fixedY, maxY까지).</summary>
        public void SetVerticalFollow(float maxY)
        {
            _followY = true;
            _maxY = maxY;
            _motor = target != null ? target.GetComponent<PlayerMotor>() : null;
        }

        private float WantedY()
        {
            if (!_followY || target == null) return fixedY;
            float feet = target.position.y - target.localScale.y * 0.5f;
            // 발판에 서 있을 때만 높이를 바꾼다. 떨어질 땐 화면 아래로 빠지지 않게 바로 따라 내려간다
            if ((_motor != null && _motor.IsGrounded) || feet < _standY) _standY = Mathf.Max(0f, feet);
            // 선 높이의 70%만 올라간다: 높은 곳에서 아래쪽(몰려오는 몬스터)이 더 보이게
            return Mathf.Clamp(fixedY + _standY * 0.7f, fixedY, Mathf.Max(fixedY, _maxY));
        }

        /// <summary>바닥에 섰을 때보다 카메라가 올라간 높이 (배경이 높이감을 내는 데 쓴다).</summary>
        public float VerticalRise => _followY ? Mathf.Max(0f, _basePos.y - fixedY) : 0f;

        /// <summary>카메라 크기와 높이를 바꾼다 (보스방: 큰 보스가 화면에 들어오게 넓게). 바닥이 화면에서 같은 높이에 오도록 높이를 함께 준다.</summary>
        public void SetView(float orthographicSize, float y)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.orthographicSize = orthographicSize;
            fixedY = y;
            _basePos = new Vector3(_basePos.x, fixedY, _basePos.z);
            transform.position = _basePos;
        }

        /// <summary>목표 위치로 카메라를 바로 옮긴다 (방에 들어왔을 때 천천히 따라오지 않게).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            if (_cam == null) _cam = GetComponent<Camera>();
            float halfWidth = _cam.orthographicSize * _cam.aspect;
            float x = Mathf.Clamp(target.position.x, arenaMinX + halfWidth, arenaMaxX - halfWidth);
            if (arenaMaxX - arenaMinX < halfWidth * 2f) x = (arenaMinX + arenaMaxX) * 0.5f;
            _basePos = new Vector3(x, WantedY(), _basePos.z);
            transform.position = _basePos;
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
                float ty = 1f - Mathf.Exp(-followSharpness * 0.6f * dt);
                _basePos = new Vector3(Mathf.Lerp(_basePos.x, x, t), Mathf.Lerp(_basePos.y, WantedY(), ty), _basePos.z);
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
