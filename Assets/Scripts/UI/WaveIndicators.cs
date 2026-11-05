using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 화면 밖 몬스터와 활성 구역을 화면 가장자리에 표시한다 (미니맵 대신 — 눈을 캐릭터에서 떼면 패링을 놓치므로 곁눈으로 읽히게).
    /// 가까운 순서로 5개, 남은 몬스터가 3마리 이하면 모두. 색은 종류별이고 빨강은 공격 경고 전용이라 안 쓴다.
    /// </summary>
    public class WaveIndicators : MonoBehaviour
    {
        private const int MaxShown = 5, ShowAllAt = 3, Pool = 12;
        private static readonly Color MeleeColor = new(0.95f, 0.95f, 0.95f), RangedColor = new(0.35f, 0.95f, 0.9f), EliteColor = new(0.8f, 0.45f, 1f);
        private static Sprite _arrow;

        private readonly List<Image> _marks = new();
        private Image _zoneArrow;
        private Text _zoneLabel;
        private Camera _cam;

        /// <summary>지금 보이는 몬스터 표식 수 (테스트).</summary>
        public int ShownCount { get; private set; }
        public bool ZoneArrowShown => _zoneArrow != null && _zoneArrow.enabled;

        private void Start()
        {
            var go = new GameObject("WaveIndicators", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var root = (RectTransform)go.transform;
            for (int i = 0; i < Pool; i++) _marks.Add(Arrow("Mark", root));
            _zoneArrow = Arrow("ZoneArrow", root);
            _zoneArrow.color = new Color(0.8f, 0.7f, 1f);
            _zoneLabel = UiKit.Text("ZoneLabel", root, 26, TextAnchor.MiddleCenter);
            _zoneLabel.color = new Color(0.9f, 0.85f, 1f);
            _zoneLabel.rectTransform.sizeDelta = new Vector2(320f, 40f);
            _zoneLabel.enabled = false;
        }

        private static Image Arrow(string name, RectTransform parent)
        {
            var img = UiKit.Image(name, parent, Color.white);
            img.sprite = ArrowSprite;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            var wave = WaveController.Instance;
            if (_cam == null || wave == null) return;
            ShowEnemies(wave);
            ShowZone(wave);
        }

        private void ShowEnemies(WaveController wave)
        {
            Vector2 camPos = _cam.transform.position;
            var off = new List<EnemyController>();
            foreach (var e in wave.Alive)
            {
                if (e == null || e.IsDead) continue;
                if (!Inside(_cam.WorldToViewportPoint(e.transform.position))) off.Add(e);
            }
            off.Sort((a, b) => Vector2.Distance(a.transform.position, camPos).CompareTo(Vector2.Distance(b.transform.position, camPos)));
            int limit = wave.Remaining <= ShowAllAt ? Pool : MaxShown;
            ShownCount = Mathf.Min(off.Count, limit, Pool);
            for (int i = 0; i < _marks.Count; i++)
            {
                var img = _marks[i];
                img.enabled = i < ShownCount;
                if (!img.enabled) continue;
                var e = off[i];
                float dist = Vector2.Distance(e.transform.position, camPos);
                float near = Mathf.InverseLerp(40f, 12f, dist); // 가까울수록 크고 진하게
                var c = e.IsElite ? EliteColor : e.Kind == EnemyKind.Ranged ? RangedColor : MeleeColor;
                c.a = Mathf.Lerp(0.55f, 1f, near);
                img.color = c;
                Place(img.rectTransform, _cam.WorldToViewportPoint(e.transform.position), Mathf.Lerp(26f, 44f, near));
            }
        }

        private void ShowZone(WaveController wave)
        {
            var def = wave.Def;
            bool show = false;
            float targetX = 0f;
            if (def != null && wave.CurrentPhase == WaveController.Phase.Fighting && !def.Boss)
            {
                float camX = _cam.transform.position.x, best = float.MaxValue;
                bool inside = false;
                foreach (int z in def.Zones)
                {
                    var zone = WavePlan.ZoneList[z];
                    if (zone.Contains(camX)) inside = true;
                    float d = Mathf.Abs(zone.CenterX - camX);
                    if (d < best) { best = d; targetX = zone.CenterX; }
                }
                show = !inside;
            }
            _zoneArrow.enabled = show;
            _zoneLabel.enabled = show;
            if (!show) return;
            var v = _cam.WorldToViewportPoint(new Vector3(targetX, _cam.transform.position.y, 0f));
            Place(_zoneArrow.rectTransform, v, 64f);
            _zoneLabel.text = def.ZoneName;
            var rt = _zoneLabel.rectTransform;
            float x = v.x > 0.5f ? 0.86f : 0.14f;
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0.56f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static bool Inside(Vector3 v) => v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;

        /// <summary>화면 가운데에서 대상 쪽으로 뻗은 선이 가장자리와 만나는 곳에 대상을 가리키게 놓는다.</summary>
        private static void Place(RectTransform rt, Vector3 viewport, float size)
        {
            var dir = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            float t = Mathf.Min(0.46f / Mathf.Max(0.0001f, Mathf.Abs(dir.x)), 0.4f / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
            var at = new Vector2(0.5f, 0.5f) + dir * t;
            rt.anchorMin = rt.anchorMax = at;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y * 9f, dir.x * 16f) * Mathf.Rad2Deg);
        }

        private static Sprite ArrowSprite
        {
            get
            {
                if (_arrow != null) return _arrow;
                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[s * s];
                // 오른쪽을 가리키는 삼각형
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float half = (s - 1 - x) * 0.5f;
                        px[y * s + x] = Mathf.Abs(y - s * 0.5f) <= half ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _arrow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _arrow;
            }
        }
    }
}
