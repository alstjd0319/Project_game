using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 바닥·벽·발판의 보이는 그림. 판정(콜라이더가 있는 네모)은 그대로 두고, 그 위에 <see cref="FloorSet"/> 타일 그림을 자식으로 깐다
    /// → 보이는 땅 윗면 = 판정 윗면. 네모는 연습 스위치 "판정 상자 보기"일 때만 반투명하게 보인다.
    /// 구간은 배경과 같은 규칙(<see cref="forcedZone"/> ≥ 0이면 고정, 아니면 GameTuning.backgroundZone)이고, 바뀌면 다시 깐다.
    /// </summary>
    public class FloorDecorator : MonoBehaviour
    {
        public enum Kind { Ground, Wall, Platform }

        [System.Serializable]
        public class Segment
        {
            public Transform solid;
            public Kind kind;
        }

        [SerializeField] private FloorSet[] sets;
        [SerializeField] private Segment[] segments;
        [SerializeField, Tooltip("0 이상이면 GameTuning 대신 이 구간을 고정으로 쓴다 (사당: 0)")] private int forcedZone = -1;

        private const int FillOrder = 1, TopOrder = 2, PlatformOrder = 3;
        private const float FillDepth = 3.5f;    // 바닥 표면 아래로 속 채움을 깔 깊이(유닛, 화면 아래 끝까지)
        private const float WallTop = 12f;       // 벽 그림이 올라가는 높이(표면 기준)
        /// <summary>바닥 판정의 깊이 (윗면 y=0에서 아래로). 속 채움 그림(FillDepth)보다 조금 깊게 — 추락 판정(y −4.5)보다도 아래까지.</summary>
        public const float GroundDepth = 5f;

        private int _zone = -1;
        private readonly HashSet<Transform> _unusedPlatforms = new();
        private Transform _art;
        private readonly List<SpriteRenderer> _boxes = new();
        private readonly List<bool> _boxHasArt = new();   // 그 판정 네모에 그림이 깔렸는가 (없으면 네모를 그대로 보인다)

        public int Zone => _zone;
        /// <summary>지금 그려 둔 타일 개수 (테스트·디버그).</summary>
        public int TileCount { get; private set; }

        private void Awake()
        {
            foreach (var seg in segments)
            {
                _boxes.Add(seg.solid != null ? seg.solid.GetComponent<SpriteRenderer>() : null);
                _boxHasArt.Add(false);
            }
        }

        private void LateUpdate()
        {
            if (sets == null || sets.Length == 0) return;
            int wanted = Mathf.Clamp(forcedZone >= 0 ? forcedZone : GameTuning.Current.backgroundZone, 0, sets.Length - 1);
            if (wanted != _zone) Build(wanted);

            // 그림이 깔린 판정 네모는 숨기고, "판정 상자 보기"일 때만 반투명하게 겹쳐 본다 (그림이 없는 건 예전 네모 그대로). 발판 그림이 없는 구간(사당)은 발판을 아예 끈다
            for (int i = 0; i < _boxes.Count; i++)
            {
                var box = _boxes[i];
                if (box == null) continue;
                bool art = _boxHasArt[i];
                box.enabled = !art || GameTuning.Current.showHitboxes;
                var c = box.color;
                c.a = art ? 0.35f : 0.55f;
                box.color = c;
            }
        }

        /// <summary>
        /// 방의 발판 배치를 바꾼다: 발판 오브젝트(Platform 종류 세그먼트)를 순서대로 레이아웃에 맞춰 옮기고 늘리고, 남는 발판은 판정째 끈다. 그림은 다음 프레임에 다시 깐다.
        /// </summary>
        public void SetPlatformLayout(IReadOnlyList<PlatformSpec> layout)
        {
            int used = 0;
            foreach (var seg in segments)
            {
                if (seg.kind != Kind.Platform || seg.solid == null) continue;
                _unusedPlatforms.Remove(seg.solid);
                if (used < layout.Count)
                {
                    var p = layout[used++];
                    seg.solid.position = new Vector3(p.X, p.Y, 0f);
                    seg.solid.localScale = new Vector3(p.Width, 0.4f, 1f);
                }
                else _unusedPlatforms.Add(seg.solid);
            }
            Physics2D.SyncTransforms();
            _zone = -1; // 다시 깔기
        }

        /// <summary>
        /// 바닥을 구멍을 뺀 구간들로 나눈다: 바닥 오브젝트(Ground 종류 세그먼트)를 순서대로 구간에 맞춰 옮기고 늘리고, 남는 건 판정째 끈다.
        /// 구간이 하나(전체)면 원래 바닥 그대로다. 그림은 다음 프레임에 다시 깐다.
        /// </summary>
        public void SetGroundIntervals(IReadOnlyList<(float x0, float x1)> intervals)
        {
            int used = 0;
            foreach (var seg in segments)
            {
                if (seg.kind != Kind.Ground || seg.solid == null) continue;
                if (used < intervals.Count)
                {
                    var (x0, x1) = intervals[used++];
                    // 바닥 판정을 그림(속 채움)만큼 깊게: 윗면은 y=0 그대로, 아래로 GroundDepth. 구멍에 떨어질 때 구멍 옆 땅(벽)을 몸이 뚫고 지나가지 않게
                    seg.solid.position = new Vector3((x0 + x1) * 0.5f, -GroundDepth * 0.5f, 0f);
                    seg.solid.localScale = new Vector3(x1 - x0, GroundDepth, 1f);
                    _unusedPlatforms.Remove(seg.solid);
                }
                else _unusedPlatforms.Add(seg.solid);
            }
            Physics2D.SyncTransforms();
            _zone = -1;
        }

        /// <summary>지금 쓰는(켜진) 발판 개수 (테스트·디버그).</summary>
        public int ActivePlatformCount
        {
            get
            {
                int n = 0;
                foreach (var seg in segments)
                    if (seg.kind == Kind.Platform && seg.solid != null && seg.solid.gameObject.activeSelf) n++;
                return n;
            }
        }

        private void Build(int zone)
        {
            if (_art != null) Destroy(_art.gameObject);
            _art = new GameObject("FloorArt").transform;
            _art.SetParent(transform, false);
            _zone = zone;
            TileCount = 0;
            var set = sets[zone];
            for (int i = 0; i < _boxHasArt.Count; i++) _boxHasArt[i] = false;
            // 발판 그림이 없는 구간(사당)은 발판 자체를 없앤다 (판정도 함께). 있는 구간에선 다시 켠다.
            bool platforms = set != null && set.HasPlatforms;
            foreach (var seg in segments)
                if (seg.kind == Kind.Platform && seg.solid != null)
                    seg.solid.gameObject.SetActive(!_unusedPlatforms.Contains(seg.solid) && (platforms || set == null || set.top.Length == 0));
                else if (seg.kind == Kind.Ground && seg.solid != null)
                    seg.solid.gameObject.SetActive(!_unusedPlatforms.Contains(seg.solid)); // 구멍을 뺀 구간에 안 쓰이는 바닥 조각은 끈다
            if (set == null || set.top.Length == 0) return;

            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (seg.solid == null) continue;
                switch (seg.kind)
                {
                    case Kind.Ground:
                        if (!_unusedPlatforms.Contains(seg.solid)) { BuildGround(set, seg.solid); _boxHasArt[i] = true; }
                        break;
                    case Kind.Wall:
                        if (set.fill.Length > 0) { BuildWall(set, seg.solid); _boxHasArt[i] = true; }
                        break;
                    case Kind.Platform:
                        if (set.HasPlatforms && !_unusedPlatforms.Contains(seg.solid)) { BuildPlatform(set, seg.solid); _boxHasArt[i] = true; }
                        break;
                }
            }
        }

        private SpriteRenderer Tile(Sprite sprite, Vector2 pos, float scale, int order, bool flip = false)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(_art, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale * (flip ? -1f : 1f), scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            TileCount++;
            return sr;
        }

        private static float Width(Sprite s, float scale) => s.bounds.size.x * scale;

        private void BuildGround(FloorSet set, Transform solid)
        {
            float scale = set.unitScale;
            float x0 = solid.position.x - solid.localScale.x * 0.5f;
            float x1 = solid.position.x + solid.localScale.x * 0.5f;
            float surface = solid.position.y + solid.localScale.y * 0.5f;

            // 속 채움 (윗면 뒤에서 표면 아래로 깐다)
            if (set.fill.Length > 0)
            {
                float rowTop = surface - 0.9f;                       // 윗면 몸통(1유닛)과 살짝 겹치게
                float bottom = surface - FillDepth;
                int row = 0;
                for (float y = rowTop; y > bottom; row++)
                {
                    float h = 0f;
                    int i = row;
                    for (float x = x0; x < x1; i++)
                    {
                        var s = set.fill[(i * 7 + row * 3) % set.fill.Length];
                        float w = Width(s, scale);
                        var tile = Tile(s, new Vector2(x + w * 0.5f, y), scale, FillOrder);
                        if (set.fillDarken > 0f) // 깊어질수록 어둡게 (채움이 한 줄 반복인 구간이 아래로 갈수록 자연스럽게 가라앉는다)
                            tile.color = Color.Lerp(Color.white, new Color(0.14f, 0.13f, 0.22f), Mathf.Clamp01((rowTop - y) / (rowTop - bottom)) * set.fillDarken);
                        x += w;
                        h = s.bounds.size.y * scale;
                    }
                    y -= Mathf.Max(0.2f, h) - 0.02f;
                }
            }

            // 윗면: 판정 윗면에 표면 줄을 맞춰 이어 붙인다 (타일이 둘 이상이면 번갈아 — 같은 타일이 연달아 반복돼 보이지 않게)
            int k = 0;
            for (float x = x0; x < x1; k++)
            {
                var s = set.top[(k * 5 + k / 3) % set.top.Length];
                float w = Width(s, scale);
                Tile(s, new Vector2(x + w * 0.5f, surface), scale, TopOrder);
                x += w;
            }
        }

        private void BuildWall(FloorSet set, Transform solid)
        {
            if (set.fill.Length == 0) return;
            float scale = set.unitScale;
            float dir = Mathf.Sign(solid.position.x);                 // 오른쪽 벽 +1, 왼쪽 벽 -1
            float innerX = solid.position.x - dir * solid.localScale.x * 0.5f;
            // 벽 그림은 안쪽 면에서 바깥으로 깐다. 바닥 표면(y=0 기준)부터 위로 쌓는다 — 속 채움 타일 (위 가운데 피벗이라 위에서 아래로)
            float bottom = -FillDepth;
            int col = 0;
            for (float y = WallTop; y > bottom; col++)
            {
                var s = set.fill[(col * 3 + 1) % set.fill.Length];
                float w = Width(s, scale);
                Tile(s, new Vector2(innerX + dir * w * 0.5f, y), scale, TopOrder);
                y -= s.bounds.size.y * scale - 0.02f;
            }
        }

        private void BuildPlatform(FloorSet set, Transform solid)
        {
            float scale = set.unitScale;
            float width = solid.localScale.x;
            float surface = solid.position.y + solid.localScale.y * 0.5f;
            var l = set.platL[0];
            var r = set.platR[0];
            float wl = Width(l, scale), wr = Width(r, scale);
            float wm = Width(set.platM[0], scale);
            // 가운데 조각 개수를 정해 전체 폭을 판정 폭에 가깝게 하고, 가로세로 같은 배율(fit)로 맞춘다 (일그러지지 않게)
            int mids = Mathf.Max(0, Mathf.RoundToInt((width - wl - wr) / wm));
            float natural = wl + wr + mids * wm;
            float fit = width / natural;
            float x = solid.position.x - width * 0.5f;
            Tile(l, new Vector2(x + wl * fit * 0.5f, surface), scale * fit, PlatformOrder);
            x += wl * fit;
            for (int i = 0; i < mids; i++)
            {
                var m = set.platM[i % set.platM.Length];
                float wi = Width(m, scale) * fit;
                Tile(m, new Vector2(x + wi * 0.5f, surface), scale * fit, PlatformOrder);
                x += wi;
            }
            Tile(r, new Vector2(x + wr * fit * 0.5f, surface), scale * fit, PlatformOrder);
        }
    }
}
