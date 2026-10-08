using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 퍼펙트 패링(입력 직후 <see cref="GameTuning.perfectWindow"/> 안에 성공) 전용 연출.
    /// 일반 성공 연출(HitFeel·Fx) 위에 얹는 층이라 판정·게이지·반격은 건드리지 않는다. 전부 unscaled time.
    /// </summary>
    public class SuccessEffects : MonoBehaviour
    {
        private const int DimOrder = 9;      // 적(8)·바닥(0) 위, 플레이어(10) 아래 → 플레이어만 밝게 남는다

        [Header("퍼펙트 패링")]
        [SerializeField, Tooltip("일반 성공 대비 연출 배율 (히트스탑·셰이크·줌·파편 수)")] private float parryIntensity = 1.6f;
        [SerializeField] private string parryText = "퍼펙트 패링 성공!";
        [SerializeField] private Color parryColor = new(1f, 0.82f, 0.3f);

        [Header("공통 하이라이트")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.45f;
        [SerializeField, Tooltip("카메라가 순간 당겨지는 비율 (0.12 = 12%)")] private float zoomPunch = 0.12f;
        [SerializeField] private float zoomRecover = 0.5f;
        [SerializeField] private int speedLineCount = 14;

        private Camera _cam;
        private SpriteRenderer _dim;
        private Coroutine _highlight;
        private Coroutine _zoom;
        private float _baseOrtho;

        private void OnDisable()
        {
            if (_zoom != null && _cam != null) _cam.orthographicSize = _baseOrtho;
            if (_dim != null) _dim.gameObject.SetActive(false);
        }

        public void PlayPerfect(Vector2 hitPoint)
        {
            Color color = parryColor;
            float k = parryIntensity;

            Hud.WorldText(transform.position + Vector3.up * 2.4f, parryText, color, 1.5f);
            Hud.ScreenFlash(new Color(color.r, color.g, color.b, Mathf.Min(0.6f, 0.35f * k)));
            SpeedLines(hitPoint, color);

            var p = GameTuning.Current.heavyParry;
            if (HitFeel.Instance != null)
                HitFeel.Instance.Play(new HitFeelProfile
                {
                    hitStop = p.hitStop * k, slowMotion = p.slowMotion * k, slowScale = p.slowScale,
                    shake = p.shake * k, shakeDuration = p.shakeDuration,
                });
            Fx.Sparks(hitPoint, color, Mathf.RoundToInt(18 * k), 11f * k);
            Fx.Ring(hitPoint, color, 0.3f, 2.2f * k, 0.1f, 0.35f);

            if (_highlight != null) StopCoroutine(_highlight);
            _highlight = StartCoroutine(Highlight((p.hitStop + p.slowMotion) * k + 0.15f));
            PunchZoom(k);
        }

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
