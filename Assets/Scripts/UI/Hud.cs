using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 디버그 겸 기본 HUD. 코드로 uGUI 캔버스를 만든다.
    /// 체력 / 공용 도력(20칸) / 현재 캐릭터 / 스킬바 / 판정 팝업 / 월드 텍스트 / 일시정지·게임오버.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private static Hud _instance;

        private PlayerParty _party;
        private SkillGauge _gauge;

        private RectTransform _root;
        private Image _hpFill;
        private Text _hpText;
        private Text _characterText;
        private Text _gaugeText;
        private readonly List<Image> _gaugeCells = new();
        private readonly List<Image> _gaugeRings = new();
        private SkillBar _skillBar;
        private TuningPanel _tuningPanel;
        private PlayerLevel _level;
        private Text _levelText;
        private Image _xpFill;
        private Text _xpText;
        private float _xpFlash;
        private Text _popupText;
        private float _popupTimer;
        private float _popupScale;
        private Image _screenFlash;
        private Color _flashColor;
        private float _flashTimer;
        private GameObject _pausePanel;
        private GameObject _gameOverPanel;

        private readonly List<FloatingText> _floating = new();

        private class FloatingText
        {
            public Text text;
            public Vector3 worldPos;
            public float t;
            public float life;
            public Color color;
            public float scale;
        }

        private void Awake()
        {
            _instance = this;
            BuildCanvas();
        }

        private void OnDestroy() => _tuningPanel?.Dispose(); // 정적 이벤트 구독 해제 (씬 재시작 시)

        private void Start()
        {
            _party = FindAnyObjectByType<PlayerParty>();
            _gauge = FindAnyObjectByType<SkillGauge>();
            _skillBar.Bind(_party, _gauge, FindAnyObjectByType<PlayerDefense>(), FindAnyObjectByType<PlayerCombat>());
            _level = FindAnyObjectByType<PlayerLevel>();
            if (_level != null)
            {
                _level.XpGained += _ => _xpFlash = 1f;
                // 레벨업은 지금 보상이 없다 (증강 폐지) — 알림만. 스킬 트리가 생기면 여기에 연결한다.
                _level.LeveledUp += lv =>
                {
                    Popup($"레벨 업!  Lv.{lv}", new Color(0.45f, 0.9f, 1f), false);
                    Sfx.Play(SfxId.LevelUp, 0.7f);
                };
            }
            _tuningPanel.Bind(FindAnyObjectByType<PlayerDefense>(), _party, _gauge, _level);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PauseChanged += p => _pausePanel.SetActive(p);
                GameManager.Instance.GameOver += () => _gameOverPanel.SetActive(true);
            }
        }

        // ───────────── 정적 API ─────────────

        /// <summary>화면 상단 중앙의 판정 문구 (패링!/강공격 패링! 등).</summary>
        public static void Popup(string message, Color color, bool big)
        {
            if (_instance == null) return;
            _instance._popupText.text = message;
            _instance._popupText.color = color;
            _instance._popupText.fontSize = big ? 64 : 46;
            _instance._popupTimer = big ? 1.0f : 0.7f;
            _instance._popupScale = big ? 1.6f : 1.35f;
        }

        public static void WorldText(Vector3 worldPos, string message, Color color, float scale = 1f)
        {
            if (_instance == null) return;
            var text = UiKit.Text("Floating", _instance._root, 30, TextAnchor.MiddleCenter);
            text.text = message;
            text.color = color;
            text.rectTransform.sizeDelta = new Vector2(400, 60);
            _instance._floating.Add(new FloatingText
            {
                text = text, worldPos = worldPos, life = 0.8f, color = color, scale = scale,
            });
        }

        public static void ScreenFlash(Color color)
        {
            if (_instance == null) return;
            _instance._flashColor = color;
            _instance._flashTimer = 1f;
        }

        // ───────────── 갱신 ─────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateStatus();
            _skillBar.Tick(dt);
            _tuningPanel.Tick();
            UpdatePopup(dt);
            UpdateFloating(dt);

            _flashTimer = Mathf.Max(0f, _flashTimer - dt * 5f);
            var fc = _flashColor;
            fc.a *= _flashTimer;
            _screenFlash.color = fc;
        }

        private void UpdateStatus()
        {
            if (_party != null)
            {
                _hpFill.rectTransform.anchorMax = new Vector2((float)_party.Hp / _party.MaxHp, 1f);
                _hpText.text = $"HP {_party.Hp} / {_party.MaxHp}";
                var cur = _party.Current;
                _combat ??= FindAnyObjectByType<PlayerCombat>();
                bool ranged = _combat != null && _combat.IsRanged;
                _characterText.text = $"{cur.displayName}  <size=22>[{cur.defenseName}] {(ranged ? "원거리" : "근접")}{PassiveLabel(cur.kind, ranged)}</size>";
                _characterText.color = _combat != null ? _combat.WeaponColor : cur.color;
            }

            if (_gauge != null)
            {
                // 도력 구슬(염주): 찬 구슬은 은은하게 숨 쉬듯 빛나고, 가장 최근에 찬 구슬이 가장 밝다. 앞 10개는 옥빛, 뒤 10개는 귀문의 보라빛
                float t = Time.unscaledTime;
                for (int i = 0; i < _gaugeCells.Count; i++)
                {
                    bool filled = i < _gauge.Value;
                    var core = _gaugeCells[i];
                    if (filled)
                    {
                        Color c = i < 10 ? new Color(0.55f, 0.95f, 0.88f) : new Color(0.78f, 0.55f, 1f);
                        float pulse = 0.85f + 0.15f * Mathf.Sin(t * 3f + i * 0.5f);
                        bool newest = i == _gauge.Value - 1;
                        c.a = newest ? 1f : 0.85f * pulse;
                        core.color = c;
                        core.rectTransform.localScale = Vector3.one * (newest ? 1.15f + 0.1f * Mathf.Sin(t * 5f) : 1f);
                        _gaugeRings[i].color = new Color(c.r, c.g, c.b, 0.9f);
                    }
                    else
                    {
                        core.color = Color.clear;
                        core.rectTransform.localScale = Vector3.one;
                        _gaugeRings[i].color = new Color(0.6f, 0.5f, 0.8f, 0.3f);
                    }
                }
                _gaugeText.text = SkillGauge.Infinite
                    ? $"<color=#CDB8FF>도력</color> {_gauge.Value} / {_gauge.Max}  <color=#9DF5E0>∞ 무한 (연습)</color>"
                    : $"<color=#CDB8FF>도력</color> {_gauge.Value} / {_gauge.Max}";
            }

            if (_level != null)
            {
                _levelText.text = $"Lv.{_level.Level}";
                _xpFill.rectTransform.anchorMax = new Vector2((float)_level.Xp / _level.XpToNext, 1f);
                _xpText.text = $"EXP {_level.Xp} / {_level.XpToNext}";
                _xpFlash = Mathf.Max(0f, _xpFlash - Time.unscaledDeltaTime * 4f);
                _xpFill.color = Color.Lerp(new Color(0.45f, 0.9f, 1f), Color.white, _xpFlash);
            }
        }

        private PlayerCombat _combat;

        /// <summary>기본값(배율 1, 감소 0)이 아닌 패시브만 표시. 예: "  공격 ×1.5 · 받는 피해 -30%" / "  이속 +30%"</summary>
        private static string PassiveLabel(CharacterKind kind, bool ranged)
        {
            var t = GameTuning.Current;
            float atk = ranged ? t.rangedAttackMultiplier : t.AttackMultiplier(kind);
            float red = t.DamageReduction(kind);
            var parts = new List<string>();
            if (!Mathf.Approximately(atk, 1f)) parts.Add($"공격 ×{atk:0.##}");
            if (red > 0.001f) parts.Add($"받는 피해 -{red * 100f:0}%");
            // 이동 속도는 플레이어 대비로 (플레이어가 기준)
            float speedRatio = t.MoveSpeed(kind) / t.MoveSpeed(CharacterKind.Player);
            if (kind != CharacterKind.Player && speedRatio > 1.001f) parts.Add($"이속 +{(speedRatio - 1f) * 100f:0}%");
            return parts.Count == 0 ? "" : $"  <color=#FFCC66>{string.Join(" · ", parts)}</color>";
        }
        private void UpdatePopup(float dt)
        {
            if (_popupTimer <= 0f)
            {
                _popupText.enabled = false;
                return;
            }
            _popupText.enabled = true;
            _popupTimer -= dt;
            _popupScale = Mathf.Lerp(_popupScale, 1f, 1f - Mathf.Exp(-18f * dt));
            _popupText.rectTransform.localScale = Vector3.one * _popupScale;
            var c = _popupText.color;
            c.a = Mathf.Clamp01(_popupTimer / 0.25f);
            _popupText.color = c;
        }

        private void UpdateFloating(float dt)
        {
            var cam = Camera.main;
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                var f = _floating[i];
                f.t += dt;
                float k = f.t / f.life;
                if (k >= 1f || cam == null)
                {
                    Destroy(f.text.gameObject);
                    _floating.RemoveAt(i);
                    continue;
                }

                Vector3 world = f.worldPos + Vector3.up * (0.8f * (1f - (1f - k) * (1f - k)));
                Vector2 screen = cam.WorldToScreenPoint(world);
                // 오버레이 캔버스면 UI 카메라 없음(null), 카메라 렌더 캔버스(캡처 등)면 그 카메라 기준
                var canvas = _root.GetComponent<Canvas>();
                var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, uiCam, out var local);
                f.text.rectTransform.anchoredPosition = local;
                float pop = k < 0.15f ? Mathf.Lerp(1.5f, 1f, k / 0.15f) : 1f;
                f.text.rectTransform.localScale = Vector3.one * f.scale * pop;
                var c = f.color;
                c.a = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                f.text.color = c;
            }
        }

        // ───────────── 생성 ─────────────

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            // 화면 플래시 (맨 아래)
            _screenFlash = UiKit.Image("ScreenFlash", _root, Color.clear);
            UiKit.Stretch(_screenFlash.rectTransform);

            // 좌상단 상태 패널: 캐릭터 / 체력 / 도력
            var panel = UiKit.Image("StatusPanel", _root, new Color(0f, 0f, 0f, 0.45f)).rectTransform;
            UiKit.Place(panel, new Vector2(0, 1), new Vector2(24, -24), new Vector2(620, 236));

            _characterText = UiKit.Text("Character", panel, 36, TextAnchor.UpperLeft);
            UiKit.Place(_characterText.rectTransform, new Vector2(0, 1), new Vector2(16, -10), new Vector2(590, 48));

            var hpBg = UiKit.Image("HpBg", panel, new Color(1f, 1f, 1f, 0.12f)).rectTransform;
            UiKit.Place(hpBg, new Vector2(0, 1), new Vector2(16, -64), new Vector2(588, 30));
            _hpFill = UiKit.Image("HpFill", hpBg, new Color(0.9f, 0.25f, 0.3f));
            UiKit.Stretch(_hpFill.rectTransform);
            _hpText = UiKit.Text("HpText", hpBg, 22, TextAnchor.MiddleCenter);
            UiKit.Stretch(_hpText.rectTransform);

            // 도력 구슬 20개 (염주처럼 한 줄에 꿰어 있다, 10개째 뒤에 매듭 간격)
            const float cell = 24f, gap = 4f, midGap = 12f;
            var thread = UiKit.Image("GaugeThread", panel, new Color(0.7f, 0.6f, 0.95f, 0.35f)).rectTransform;
            UiKit.Place(thread, new Vector2(0, 1), new Vector2(10, -104 - cell * 0.5f + 1f), new Vector2(600, 2));
            for (int i = 0; i < 20; i++)
            {
                float x = 16 + i * (cell + gap) + (i >= 10 ? midGap : 0f);
                var ring = UiKit.Image($"GaugeRing{i}", panel, Color.white);
                ring.sprite = FxSprites.Ring;
                UiKit.Place(ring.rectTransform, new Vector2(0, 1), new Vector2(x, -104), new Vector2(cell, cell));
                _gaugeRings.Add(ring);
                var core = UiKit.Image($"Gauge{i}", panel, Color.clear);
                core.sprite = FxSprites.Glow;
                UiKit.Place(core.rectTransform, new Vector2(0, 1), new Vector2(x + 3f, -107), new Vector2(cell - 6f, cell - 6f));
                _gaugeCells.Add(core);
            }
            _gaugeText = UiKit.Text("GaugeText", panel, 22, TextAnchor.UpperLeft);
            UiKit.Place(_gaugeText.rectTransform, new Vector2(0, 1), new Vector2(16, -134), new Vector2(590, 30));

            // 레벨 + 경험치 바
            _levelText = UiKit.Text("Level", panel, 26, TextAnchor.MiddleLeft);
            _levelText.fontStyle = FontStyle.Bold;
            _levelText.color = new Color(0.45f, 0.9f, 1f);
            UiKit.Place(_levelText.rectTransform, new Vector2(0, 1), new Vector2(16, -166), new Vector2(80, 26));
            var xpBg = UiKit.Image("XpBg", panel, new Color(1f, 1f, 1f, 0.12f)).rectTransform;
            UiKit.Place(xpBg, new Vector2(0, 1), new Vector2(96, -170), new Vector2(508, 18));
            _xpFill = UiKit.Image("XpFill", xpBg, new Color(0.45f, 0.9f, 1f));
            UiKit.Stretch(_xpFill.rectTransform);
            _xpText = UiKit.Text("XpText", xpBg, 16, TextAnchor.MiddleCenter);
            UiKit.Stretch(_xpText.rectTransform);

            // 하단 스킬바 (Shift / A / S / D)
            _skillBar = new SkillBar(_root);

            // 판정 팝업
            _popupText = UiKit.Text("Popup", _root, 52, TextAnchor.MiddleCenter);
            _popupText.rectTransform.anchorMin = _popupText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _popupText.rectTransform.anchoredPosition = new Vector2(0, -170);
            _popupText.rectTransform.sizeDelta = new Vector2(1200, 100);
            _popupText.enabled = false;

            // 우상단 조작 안내 (스킬 키는 하단 스킬바에 표시되므로 이동·점프·일시정지만)
            var help = UiKit.Text("Help", _root, 22, TextAnchor.UpperRight);
            help.color = new Color(1f, 1f, 1f, 0.55f);
            help.text = "←/→ 이동   Space 점프 (2단)   ↓+Space 발판 내려가기   Q 무기 전환   ESC 일시정지   F1 튜닝";
            UiKit.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(900f, 32f));

            // F1 튜닝 패널
            _tuningPanel = new TuningPanel(_root);

            _pausePanel = CreateOverlay("PausePanel", "일시 정지\n<size=28>ESC 계속   R 다시 시작   M 시작 화면</size>",
                ("계속  (ESC)", () => GameManager.Instance?.SetPaused(false)),
                ("다시 시작  (R)", () => GameManager.Instance?.Restart()),
                ("시작 화면  (M)", () => GameManager.Instance?.GoToStartMenu()));
            _gameOverPanel = CreateOverlay("GameOverPanel", "쓰러졌다\n<size=28>R 다시 시작   M 시작 화면</size>",
                ("다시 시작  (R)", () => GameManager.Instance?.Restart()),
                ("시작 화면  (M)", () => GameManager.Instance?.GoToStartMenu()));
        }

        /// <summary>화면 전체를 덮는 안내 + 아래 버튼들 (키로도 되고 마우스로도 된다). 버튼은 가운데 줄로 늘어선다.</summary>
        private GameObject CreateOverlay(string name, string message, params (string label, Action action)[] buttons)
        {
            var bg = UiKit.Image(name, _root, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(bg.rectTransform);
            var text = UiKit.Text("Text", bg.rectTransform, 72, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform);
            text.text = message;

            const float width = 340f, height = 70f, gap = 24f;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, action) = buttons[i];
                var button = UiKit.Button($"Button_{i}", bg.rectTransform, label, 30, new Color(0.30f, 0.16f, 0.55f, 0.95f));
                float x = (i - (buttons.Length - 1) * 0.5f) * (width + gap);
                UiKit.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(x, -170f), new Vector2(width, height));
                ((RectTransform)button.transform).pivot = new Vector2(0.5f, 0.5f);
                var captured = action;
                button.onClick.AddListener(() => captured());
            }
            bg.gameObject.SetActive(false);
            return bg.gameObject;
        }
    }
}
