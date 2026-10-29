using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 배경 3겹(먼 · 중간 · 가까운)을 카메라를 따라가며 다른 속도로 흘려 보낸다 (시차 스크롤).
    /// 각 겹은 좌우로 이어 붙인 같은 그림 여러 장이고, 카메라 위치에 맞춰 한 칸씩 돌려 끝없이 반복된다.
    /// 판정과 무관한 그림이라 판정 규칙(보이는 그림 = 판정)에 해당하지 않는다.
    /// </summary>
    // CameraRig.LateUpdate(흔들림 포함)가 카메라 위치를 정한 뒤에 따라가야 떨리지 않는다.
    [DefaultExecutionOrder(200)]
    public class ParallaxBackground : MonoBehaviour
    {
        // 화면(1080p) 안에서 땅이 100만큼 흐를 때 각 겹이 흐르는 비율 — 안내.md의 미리보기 속도 기준 (15 · 40 · 70/140 · 땅 100)
        private const float FarScroll = 0.15f;
        private const float MidScroll = 0.4f;
        private const float NearBehindScroll = 0.7f;
        private const float NearFrontScroll = 1.4f;

        private const int FarOrder = -30, MidOrder = -20, NearBehindOrder = -10, NearFrontOrder = 14;

        [SerializeField] private BackgroundSet[] sets;
        [SerializeField, Tooltip("0 이상이면 GameTuning 대신 이 구간을 고정으로 쓴다 (시작 화면: 사당)")] private int forcedZone = -1;
        [SerializeField, Tooltip("보스방(불가살이) 배경 — 구간 순환(F1 배경 바꾸기)에는 들어가지 않고, 보스방에서만 UseBossBackground로 켠다")] private BackgroundSet bossSet;

        [SerializeField, Tooltip("보상방 배경 — 보상방에서만 UseRewardBackground로 켠다")] private BackgroundSet rewardSet;
        [SerializeField, Tooltip("상점방 배경 — 상점방에서만 UseShopBackground로 켠다")] private BackgroundSet shopSet;

        private BackgroundSet _special; // 방 전용 배경 (보스방·보상방) — 있으면 구간 배경 대신
        private bool _useBoss => _special != null;
        private const int BossZone = -2;

        /// <summary>보스방 배경으로 바꾼다 (런의 마지막 방).</summary>
        public void UseBossBackground() => UseSpecial(bossSet);

        /// <summary>보상방 배경(달빛 드는 낡은 사당 기둥)으로 바꾼다.</summary>
        public void UseRewardBackground() => UseSpecial(rewardSet);

        /// <summary>상점방 배경(소라 껍데기 동굴 장터)으로 바꾼다.</summary>
        public void UseShopBackground() => UseSpecial(shopSet);

        private void UseSpecial(BackgroundSet set)
        {
            if (set == null) return;
            _special = set;
            _zone = -1; // 다시 깔기
        }

        public bool IsBossBackground => _special != null && _special == bossSet && _zone == BossZone;
        public bool IsRewardBackground => _special != null && _special == rewardSet && _zone == BossZone;
        public bool IsShopBackground => _special != null && _special == shopSet && _zone == BossZone;

        private sealed class Layer
        {
            public Transform[] copies;
            public float width;
            public float scroll;
        }

        private readonly System.Collections.Generic.List<Layer> _layers = new();
        private Camera _cam;
        private int _zone = -1;
        private float _builtOrtho = -1f;
        /// <summary>배경 그림 높이(12유닛)가 화면 높이와 맞는 카메라 크기. 카메라를 넓게 빼면(보스방) 그만큼 배경도 키운다.</summary>
        private const float ReferenceOrtho = 6f;

        public int ZoneCount => sets != null ? sets.Length : 0;
        public int Zone => _zone;
        public string ZoneName => _zone >= 0 && _zone < ZoneCount ? sets[_zone].zoneName : "";
        public int LayerCount => _layers.Count;

        private void LateUpdate()
        {
            if (sets == null || sets.Length == 0) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            int wanted = _useBoss ? BossZone : Mathf.Clamp(forcedZone >= 0 ? forcedZone : RunState.Zone > 0 ? RunState.Zone : GameTuning.Current.backgroundZone, 0, sets.Length - 1);
            if (wanted != _zone || _layers.Count == 0 || Mathf.Abs(_cam.orthographicSize - _builtOrtho) > 0.01f) Build(wanted);

            Vector3 camPos = _cam.transform.position;
            foreach (var layer in _layers)
            {
                // 카메라를 따라가되 scroll만큼만 월드에 대해 흐르게: 오프셋을 한 칸 폭으로 감아 돌린다
                float offset = Mathf.Repeat(camPos.x * layer.scroll, layer.width);
                float baseX = camPos.x - offset;
                int n = layer.copies.Length;
                for (int i = 0; i < n; i++)
                    layer.copies[i].position = new Vector3(baseX + (i - n / 2) * layer.width, camPos.y, 0f);
            }
        }

        private void Build(int zone)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            _layers.Clear();
            _zone = zone;
            _builtOrtho = _cam.orthographicSize;
            var set = zone == BossZone ? _special : sets[zone];
            float halfW = _cam.orthographicSize * Mathf.Max(_cam.aspect, MaxAspect);

            AddLayer("Far", set.far, FarScroll, FarOrder, halfW);
            AddLayer("Mid", set.mid, MidScroll, MidOrder, halfW);
            AddLayer("Near", set.near, set.nearInFront ? NearFrontScroll : NearBehindScroll,
                set.nearInFront ? NearFrontOrder : NearBehindOrder, halfW);
        }

        /// <summary>
        /// 한 겹에 필요한 그림 장수 (홀수, 가운데 기준). 오프셋을 한 칸 폭으로 감아 돌리므로 한쪽으로 최악일 때도
        /// 화면 반폭(halfScreenW)을 덮어야 한다: 한쪽 최소 덮는 폭 = ((n - 1) / 2 - 0.5) × 칸 폭.
        /// </summary>
        public static int RequiredCopies(float tileWidth, float halfScreenW) =>
            2 * Mathf.CeilToInt(halfScreenW / tileWidth + 0.5f) + 1;

        // 창을 나중에 넓혀도(최대 32:9 초광폭) 빈틈이 안 생기게 넉넉히 잡는다
        private const float MaxAspect = 32f / 9f;

        private void AddLayer(string name, Sprite sprite, float scroll, int order, float halfW)
        {
            if (sprite == null) return;
            float k = _cam.orthographicSize / ReferenceOrtho;
            float w = sprite.bounds.size.x * k;
            int n = RequiredCopies(w, halfW);

            var layer = new Layer { copies = new Transform[n], width = w, scroll = scroll };
            var root = new GameObject($"Layer_{name}").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"{name}_{i}");
                go.transform.SetParent(root, false);
                go.transform.localScale = new Vector3(k, k, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
                layer.copies[i] = go.transform;
            }
            _layers.Add(layer);
        }
    }
}
