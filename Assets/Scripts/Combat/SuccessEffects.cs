using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 퍼펙트 방어(입력 직후 <see cref="GameTuning.perfectWindow"/> 안에 성공) 전용 연출.
    /// 일반 성공 연출(HitFeel·Fx) 위에 얹는 층이라 판정·게이지·반격은 건드리지 않는다.
    /// 전부 unscaled time — 퍼펙트 회피는 히트스탑으로 세상이 멈춘 채 연출이 이어진다.
    /// </summary>
    public class SuccessEffects : MonoBehaviour
    {
        private const int DimOrder = 9;      // 적(8)·바닥(0) 위, 플레이어(10) 아래 → 플레이어만 밝게 남는다
        private const int OverlayOrder = 30;

        [Header("퍼펙트 패링")]
        [SerializeField, Tooltip("일반 성공 대비 연출 배율 (히트스탑·셰이크·줌·파편 수)")] private float parryIntensity = 1.6f;
        [SerializeField] private string parryText = "퍼펙트 패링 성공!";
        [SerializeField] private Color parryColor = new(1f, 0.82f, 0.3f);

        [Header("회피 후퇴 (Project_Game 기준)")]
        [SerializeField, Tooltip("퍼펙트가 아닐 때 물러나는 거리")] private float normalDodgeDistance = 0.8f;
        [SerializeField, Tooltip("퍼펙트가 아닐 때 후퇴 모션 길이 (실시간 초)")] private float normalDodgeDuration = 0.25f;
        [SerializeField, Tooltip("퍼펙트 회피 기본 후퇴 거리")] private float backstepDistance = 1.5f;
        [SerializeField, Tooltip("퍼펙트 회피에서 거리에 곱하는 배율")] private float perfectDistanceMultiplier = 1.5f;
        [SerializeField] private float afterimageInterval = 0.06f;
        [SerializeField] private float afterimageLife = 0.3f;
        [SerializeField] private Color afterimageColor = new(0.4f, 1f, 0.6f, 0.55f);

        [Header("퍼펙트 회피 (플레이어만 움직이는 시간 정지)")]
        [SerializeField, Tooltip("세상이 멈추는 실시간 길이 (초) = 후퇴 모션 길이")] private float dodgeFreeze = 0.52f;
        [SerializeField, Tooltip("멈춘 뒤 원래 속도로 돌아오는 구간 (초)")] private float dodgeSlow = 0.15f;
        [SerializeField] private float dodgeSlowScale = 0.3f;

        [Header("하늘 바람개비 (시간 정지 표시 — 게임 시간으로 돌아서 세상이 멈추면 같이 멈춘다)")]
        [SerializeField] private Vector2 pinwheelViewportPos = new(0.65f, 0.65f);
        [SerializeField] private float pinwheelSize = 0.5f;
        [SerializeField] private float pinwheelSpinSpeed = 540f;
        [SerializeField] private string dodgeText = "퍼펙트 회피!";
        [SerializeField] private Color dodgeColor = new(0.75f, 1f, 0.85f);

        [Header("공통 하이라이트")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.45f;
        [SerializeField, Tooltip("카메라가 순간 당겨지는 비율 (0.12 = 12%)")] private float zoomPunch = 0.12f;
        [SerializeField] private float zoomRecover = 0.5f;
        [SerializeField] private int speedLineCount = 14;

        private Camera _cam;
        private GameObject _pinwheel;
        private SpriteRenderer _dim;
        private Coroutine _highlight;
        private Coroutine _zoom;
        private float _baseOrtho;

        public float DodgeDistance(bool perfect) => perfect ? backstepDistance * perfectDistanceMultiplier : normalDodgeDistance;
        public float DodgeDuration(bool perfect) => perfect ? dodgeFreeze : normalDodgeDuration;

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) BuildPinwheel(cam);
        }

        /// <summary>물러나는 동안 잔상과 속도선을 남긴다 (실시간).</summary>
        public void PlayBackstepTrail(float duration, int dir) => StartCoroutine(TrailRoutine(duration, dir));

        private IEnumerator TrailRoutine(float duration, int dir)
        {
            var sr = GetComponent<SpriteRenderer>();
            for (float t = 0f; t < duration; t += afterimageInterval)
            {
                Fx.Afterimage(transform.position, transform.localScale, new Color(afterimageColor.r, afterimageColor.g, afterimageColor.b, afterimageColor.a * 2f),
                    afterimageLife, sr.sprite, sr.flipX);
                Fx.Streaks(transform.position - new Vector3(dir * 0.15f, 0f, 0f), new Vector2(dir, 0f), new Color(1f, 1f, 1f, 0.85f), 2, 6f, 0.25f);
                yield return new WaitForSecondsRealtime(afterimageInterval);
            }
        }

        private void BuildPinwheel(Camera cam)
        {
            var root = new GameObject("SkyPinwheel");
            _pinwheel = root;
            root.transform.SetParent(cam.transform, false);
            float halfH = cam.orthographicSize;
            root.transform.localPosition = new Vector3(pinwheelViewportPos.x * halfH * cam.aspect, pinwheelViewportPos.y * halfH, cam.nearClipPlane + 2f);

            var hub = new GameObject("Hub").transform;
            hub.SetParent(root.transform, false);
            hub.gameObject.AddComponent<SpinWithGameTime>().degreesPerSecond = pinwheelSpinSpeed;
            var blade = BladeSprite();
            for (int i = 0; i < 4; i++)
            {
                var b = new GameObject("Blade");
                b.transform.SetParent(hub, false);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * i);
                b.transform.localScale = Vector3.one * pinwheelSize;
                var r = b.AddComponent<SpriteRenderer>();
                r.sprite = blade;
                r.color = i % 2 == 0 ? new Color(0.4f, 1f, 0.6f) : Color.white;
                r.sortingOrder = 42;
            }

            float len = pinwheelSize * 1.2f;
            var stick = Box.Create("Stick", Vector2.zero, new Vector2(0.05f, len), Color.white, 41);
            stick.transform.SetParent(root.transform, false);
            stick.transform.localPosition = new Vector3(0f, -len * 0.5f, 0f);
            root.SetActive(false); // 퍼펙트 회피로 시간이 멈춘 동안에만 보인다 (평소엔 화면에 떠 있을 이유가 없다)
        }

        // 중심에서 오른쪽 아래로 뻗는 직각삼각형 날개 (90도씩 4장 → 바람개비)
        private static Sprite _blade;
        private static Sprite BladeSprite()
        {
            if (_blade != null) return _blade;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = y <= x ? Color.white : Color.clear;
            tex.SetPixels(px);
            tex.Apply();
            _blade = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.zero, size);
            return _blade;
        }

        private void OnDisable()
        {
            if (_zoom != null && _cam != null) _cam.orthographicSize = _baseOrtho;
            if (_dim != null) _dim.gameObject.SetActive(false);
        }

        public void PlayPerfect(CharacterKind defender, Vector2 hitPoint, Vector2 attackDirection)
        {
            bool dodge = defender == CharacterKind.Archer;
            Color color = dodge ? dodgeColor : parryColor;
            float k = dodge ? 1f : parryIntensity;

            Hud.WorldText(transform.position + Vector3.up * 2.4f, dodge ? dodgeText : parryText, color, 1.5f);
            Hud.ScreenFlash(new Color(color.r, color.g, color.b, Mathf.Min(0.6f, 0.35f * k)));
            SpeedLines(dodge ? (Vector2)transform.position : hitPoint, color);

            if (dodge)
            {
                if (_pinwheel != null) _pinwheel.SetActive(true);
                // 세상 정지 → 느린 복귀. HitFeel이 timeScale 복구까지 책임진다.
                if (HitFeel.Instance != null)
                    HitFeel.Instance.Play(new HitFeelProfile
                    {
                        hitStop = dodgeFreeze, slowMotion = dodgeSlow, slowScale = dodgeSlowScale,
                        shake = 0.1f, shakeDuration = 0.2f,
                    });
            }
            else
            {
                var p = GameTuning.Current.heavyParry;
                if (HitFeel.Instance != null)
                    HitFeel.Instance.Play(new HitFeelProfile
                    {
                        hitStop = p.hitStop * k, slowMotion = p.slowMotion * k, slowScale = p.slowScale,
                        shake = p.shake * k, shakeDuration = p.shakeDuration,
                    });
                Fx.Sparks(hitPoint, color, Mathf.RoundToInt(18 * k), 11f * k);
                Fx.Ring(hitPoint, color, 0.3f, 2.2f * k, 0.1f, 0.35f);
            }

            float total = dodge ? dodgeFreeze + dodgeSlow : p0(GameTuning.Current.heavyParry) * k;
            if (_highlight != null) StopCoroutine(_highlight);
            _highlight = StartCoroutine(Highlight(total + 0.15f));
            PunchZoom(k);
        }

        private static float p0(HitFeelProfile p) => p.hitStop + p.slowMotion;

        private void SpeedLines(Vector2 origin, Color color)
        {
            color.a = 0.9f;
            for (int i = 0; i < speedLineCount; i++)
            {
                float angle = 360f / speedLineCount * i + Random.Range(-8f, 8f);
                Vector2 dir = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var go = Box.Create("Fx_SpeedLine", origin + dir * 0.5f, new Vector2(0.9f, 0.04f), color, 50);
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                go.AddComponent<FxPiece>().Init(dir * 9f, 0.22f, 1f, 1.8f, 2f, 0f);
            }
        }

        // 화면을 살짝 어둡게 해 플레이어를 하이라이트 (시간이 멈춰 있어도 실시간으로 사라진다)
        private IEnumerator Highlight(float seconds)
        {
            EnsureDim();
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(Mathf.Min(t / 0.05f, (seconds - t) / 0.15f));
                SetDim(dimAlpha * k);
                yield return null;
            }
            SetDim(0f);
            if (_pinwheel != null) _pinwheel.SetActive(false);
            _highlight = null;
        }

        private void EnsureDim()
        {
            _cam = _cam != null ? _cam : Camera.main;
            if (_dim != null || _cam == null) return;
            var go = Box.Create("Fx_Dim", Vector2.zero, Vector2.one, Color.clear, DimOrder);
            _dim = go.GetComponent<SpriteRenderer>();
            go.transform.SetParent(_cam.transform, false);
            float h = _cam.orthographicSize * 2f * 1.5f;
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            go.transform.localScale = new Vector3(h * _cam.aspect, h, 1f);
        }

        private void SetDim(float alpha)
        {
            if (_dim == null) return;
            _dim.gameObject.SetActive(alpha > 0f);
            _dim.color = new Color(0f, 0f, 0f, alpha);
        }

        private void PunchZoom(float k)
        {
            _cam = _cam != null ? _cam : Camera.main;
            if (_cam == null || !_cam.orthographic) return;
            if (_zoom != null) StopCoroutine(_zoom);
            else _baseOrtho = _cam.orthographicSize;
            _zoom = StartCoroutine(ZoomRoutine(k));
        }

        private IEnumerator ZoomRoutine(float k)
        {
            float punched = _baseOrtho * (1f - Mathf.Min(0.4f, zoomPunch * k));
            for (float t = 0f; t < zoomRecover; t += Time.unscaledDeltaTime)
            {
                float e = 1f - Mathf.Pow(1f - t / zoomRecover, 3f);
                _cam.orthographicSize = Mathf.Lerp(punched, _baseOrtho, e);
                yield return null;
            }
            _cam.orthographicSize = _baseOrtho;
            _zoom = null;
        }
    }
}

namespace ParryRL
{
    /// <summary>게임 시간(timeScale)에 따라 도는 회전 — 시간이 멈추면 같이 멈춘다.</summary>
    public class SpinWithGameTime : UnityEngine.MonoBehaviour
    {
        public float degreesPerSecond = 540f;
        private void Update() => transform.Rotate(0f, 0f, degreesPerSecond * UnityEngine.Time.deltaTime);
    }
}
