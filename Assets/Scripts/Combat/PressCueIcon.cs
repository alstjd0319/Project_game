using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 근접 몬스터 머리 위의 "지금 눌러" 느낌표. 예비 모션이 시작되면 <b>노란 느낌표</b>가 톡 튀어나와 둥실거리고(공격이 온다),
    /// 베기 직전에 <b>빨갛게</b> 바뀌며 한 번 커졌다 가라앉는다(지금 패링!). 베기가 나가거나 몬스터가 끊기면 사라진다.
    /// 그림은 코드로 그린 둥근 느낌표(색을 마음대로 바꿀 수 있게)이고 검은 테두리·뒤 빛무리가 붙는다. 판정 없음, 연출은 게임 시간 기준.
    /// </summary>
    public class PressCueIcon : MonoBehaviour
    {
        private static readonly Color Yellow = new(1f, 0.82f, 0.22f);
        private static readonly Color Red = new(1f, 0.16f, 0.12f);
        private const float PopTime = 0.14f, TurnTime = 0.09f, FadeTime = 0.12f, PunchTime = 0.22f;

        private static Sprite _fillSprite, _outlineSprite;

        private EnemyController _enemy;
        private SpriteRenderer _fill, _outline, _glow;
        private float _age;
        private float _redAt = -1f;   // 빨갛게 바뀐 시각(_age 기준), 아직이면 -1
        private float _fadeAt = -1f;  // 사라지기 시작한 시각

        public bool IsRed => _redAt >= 0f;
        /// <summary>지금 빨간 정도 0~1 (테스트·디버그).</summary>
        public float Redness => _redAt < 0f ? 0f : Mathf.Clamp01((_age - _redAt) / TurnTime);

        private bool _manual;

        /// <param name="manual">true면 몬스터의 예비 모션 상태와 상관없이 <see cref="FadeOut"/>을 부를 때까지 남는다 (보스 특수 패턴)</param>
        public static PressCueIcon Spawn(EnemyController enemy, bool manual = false)
        {
            var go = new GameObject("PressCueIcon");
            var icon = go.AddComponent<PressCueIcon>();
            icon._enemy = enemy;
            icon._manual = manual;
            icon.Build();
            icon.Follow();
            return icon;
        }

        /// <summary>빨갛게 — 지금 패링을 누를 때.</summary>
        public void TurnRed()
        {
            if (_redAt < 0f) _redAt = _age;
        }

        private void Build()
        {
            EnsureSprites();
            _glow = Layer("Glow", FxSprites.Glow, 40);
            _outline = Layer("Outline", _outlineSprite, 41);
            _fill = Layer("Fill", _fillSprite, 42);
            _glow.transform.localScale = new Vector3(1.4f, 1.8f, 1f);
            _outline.color = new Color(0.08f, 0.04f, 0.04f, 0.9f);
        }

        private SpriteRenderer Layer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order + 10;
            return sr;
        }

        private void Follow()
        {
            if (_enemy == null) return;
            // 몬스터 머리 위 (예비 모션에서 몸이 커져도 머리를 따라간다) + 살짝 둥실
            float bob = Mathf.Sin(_age * 7f) * 0.05f;
            var p = _enemy.transform.position;
            transform.position = new Vector3(p.x, p.y + _enemy.transform.localScale.y * 0.5f + 1.2f + bob, 0f);
        }

        /// <summary>바로 사라지기 시작한다 (연속 공격에서 다음 느낌표로 넘어갈 때).</summary>
        public void FadeOut()
        {
            if (_fadeAt < 0f) _fadeAt = _age;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Follow();

            // 몬스터가 예비 모션을 끝냈거나(베기가 나감) 끊기면 서서히 사라진다
            if (_fadeAt < 0f && (_enemy == null || _enemy.IsDead || (!_manual && !_enemy.IsWindingUp))) _fadeAt = _age;

            // 톡 튀어나오기: 0 → 1.25 → 1
            float pop = Mathf.Clamp01(_age / PopTime);
            float scale = pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.5f) * 1.25f : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((_age - PopTime) / 0.1f));

            // 빨갛게 바뀌는 순간 한 번 커졌다 가라앉는다
            float turn = Redness;
            if (_redAt >= 0f)
            {
                float k = Mathf.Clamp01((_age - _redAt) / PunchTime);
                scale *= 1f + 0.4f * (1f - k) * (1f - k) + 0.04f * Mathf.Sin(_age * 22f) * k;
            }

            Color c = Color.Lerp(Yellow, Red, turn);
            float alpha = _fadeAt < 0f ? 1f : Mathf.Clamp01(1f - (_age - _fadeAt) / FadeTime);
            _fill.color = new Color(c.r, c.g, c.b, alpha);
            _outline.color = new Color(0.08f, 0.04f, 0.04f, 0.9f * alpha);
            // 빛무리: 노랑일 땐 은은하게, 빨강이 되면 환하게 맥동
            float glow = Mathf.Lerp(0.18f, 0.55f + 0.15f * Mathf.Sin(_age * 18f), turn);
            _glow.color = new Color(c.r, c.g * 0.8f, c.b * 0.7f, glow * alpha);
            transform.localScale = Vector3.one * scale;

            if (_fadeAt >= 0f && _age - _fadeAt >= FadeTime) Destroy(gameObject);
        }

        // ───────────── 코드로 그린 느낌표 ─────────────

        private static void EnsureSprites()
        {
            if (_fillSprite != null && _outlineSprite != null) return;
            const int w = 20, h = 36, ppu = 32;
            var fill = new bool[w, h];
            // 위가 넓고 아래로 좁아지는 막대 (y=11..33) + 둥근 점 (중심 y=5, 반지름 3.6)
            for (int y = 11; y <= 33; y++)
            {
                float k = (y - 11) / 22f;                               // 0 = 막대 아래, 1 = 위
                float half = Mathf.Lerp(2.0f, 4.6f, k);
                float round = y >= 32 ? 1.2f * (y - 31) : 0f;           // 위 끝을 둥글게
                for (int x = 0; x < w; x++)
                    if (Mathf.Abs(x + 0.5f - w * 0.5f) <= half - round) fill[x, y] = true;
            }
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - w * 0.5f, dy = y - 5f;
                    if (dx * dx + dy * dy <= 3.6f * 3.6f) fill[x, y] = true;
                }
            var outline = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (fill[x, y]) { outline[x, y] = true; continue; }
                    for (int dy = -1; dy <= 1 && !outline[x, y]; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h && fill[nx, ny]) { outline[x, y] = true; break; }
                        }
                }
            _fillSprite = Make(fill, w, h, ppu);
            _outlineSprite = Make(outline, w, h, ppu);
        }

        private static Sprite Make(bool[,] mask, int w, int h, int ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = mask[x, y] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
