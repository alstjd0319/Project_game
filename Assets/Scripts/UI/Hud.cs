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
        private Text _runInfo;
        // 보스 체력바 (화면 상단, 퍼센트)
        private GameObject _bossRoot;
        private Image _bossFill;
        private Text _bossText;
        private System.Func<float> _bossRatio;
        private System.Func<int> _bossPhase;
        private System.Func<string> _bossExtra;
        private string _bossName;
        private float _bossShown = 1f;
        private Text _help;
        private PlayerLevel _level;
        private Text _levelText;
        private Image _xpFill;
        private Text _xpText;
        private float _xpFlash;
        private Text _popupText;
        private Text _promptText;
        private Image _promptBacking;
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

        /// <summary>설정 창을 연다 (게임은 멈추고 일시정지 메뉴는 숨겨, 창만 따로 보인다).</summary>
        public void OpenSettings()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPaused) GameManager.Instance.SetPaused(true);
            _pausePanel.SetActive(false);
            _tuningPanel.SetVisible(true);
        }

        /// <summary>설정 창을 닫고 일시정지 메뉴로 돌아간다 (ESC로 한 번 더 누르면 계속).</summary>
        public void CloseSettings()
        {
            _tuningPanel.SetVisible(false);
            _pausePanel.SetActive(GameManager.Instance != null && GameManager.Instance.IsPaused);
        }

        // ───────────── 정적 API ─────────────

        /// <summary>마지막으로 띄운 팝업 글자 (테스트용).</summary>
        public static string PopupMessage => _instance != null ? _instance._popupText.text : null;

        /// <summary>화면 상단 중앙의 판정 문구 (패링!/강공격 패링! 등).</summary>
        public static void Popup(string message, Color color, bool big)
        {
            if (_instance == null) return;
            _instance._popupText.text = message;
            _instance._popupText.color = color;
            _instance._popupText.fontSize = big ? 64 : 46;
            _instance._popupTimer = big ? 1.0f : 0.7f;
            // 보스 체력바(화면 위쪽)가 떠 있으면 그 아래로 내려 가리지 않게
            _instance._popupText.rectTransform.anchoredPosition = new Vector2(0f, BossBarVisible ? -330f : -170f);
            _instance._popupScale = big ? 1.6f : 1.35f;
        }

        /// <summary>화면 상단에 보스 체력바(용 장식 틀 + 퍼센트)를 띄운다. ratio = 체력 비율, phase = 지금 페이즈.</summary>
        /// <param name="extra">체력 글자 뒤에 붙일 한 줄 (연속 패링 수 등, 빈 문자열이면 없음)</param>
        public static void ShowBossBar(string name, System.Func<float> ratio, System.Func<int> phase, System.Func<string> extra = null)
        {
            if (_instance == null || _instance._bossRoot == null) return;
            _instance._bossExtra = extra;
            _instance._bossName = name;
            _instance._bossRatio = ratio;
            _instance._bossPhase = phase;
            _instance._bossShown = ratio != null ? ratio() : 1f;
            _instance._bossRoot.SetActive(true);
        }

        public static void HideBossBar()
        {
            if (_instance == null || _instance._bossRoot == null) return;
            _instance._bossRoot.SetActive(false);
            _instance._bossRatio = null;
        }

        public static bool BossBarVisible => _instance != null && _instance._bossRoot != null && _instance._bossRoot.activeSelf;
        public static string BossBarText => _instance != null && _instance._bossText != null ? _instance._bossText.text : "";

        /// <summary>화면 상단 중앙에 붙어 있는 한 줄 (방 번호·종류 등). null이면 숨긴다.</summary>
        public static void SetRunInfo(string message)
        {
            if (_instance == null || _instance._runInfo == null) return;
            _instance._runInfo.text = message;
            _instance._runInfo.enabled = !string.IsNullOrEmpty(message);
            if (_instance._help != null) _instance._help.enabled = string.IsNullOrEmpty(message); // 런 중엔 상단 중앙을 방 정보가 쓰므로 조작 안내는 숨긴다
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

        /// <summary>화면 아래쪽 안내 문구 (상호작용 등). null이면 숨긴다.</summary>
        public static void SetPrompt(string message)
        {
            if (_instance == null || _instance._promptText == null) return;
            bool show = !string.IsNullOrEmpty(message);
            _instance._promptBacking.gameObject.SetActive(show);
            if (show) _instance._promptText.text = message;
        }

        public static void ScreenFlash(Color color)
        {
            if (_instance == null) return;
            _instance._flashColor = color;
            _instance._flashTimer = 1f;
        }

        // ───────────── 갱신 ─────────────

        private void UpdateBossBar(float dt)
        {
            if (_bossRoot == null || !_bossRoot.activeSelf || _bossRatio == null) return;
            float ratio = Mathf.Clamp01(_bossRatio());
            _bossShown = Mathf.MoveTowards(_bossShown, ratio, dt * 1.5f); // 줄어드는 모습이 보이게 살짝 따라간다
            _bossFill.fillAmount = _bossShown;
            int phase = _bossPhase != null ? _bossPhase() : 1;
            // 2페이즈는 채움 색이 더 붉게 타오른다 (주황 계열 — 빨강은 적 공격 경고 전용)
            _bossFill.color = phase >= 2 ? new Color(1f, 0.38f, 0.12f) : new Color(1f, 0.62f, 0.2f);
            int pct = Mathf.CeilToInt(ratio * 100f);
            string extra = _bossExtra != null ? _bossExtra() : "";
            _bossText.text = $"{_bossName}  <b>{pct}%</b>" + (phase >= 2 ? "  <size=22><color=#FFB070>2페이즈</color></size>" : "")
                             + (string.IsNullOrEmpty(extra) ? "" : $"   <size=22>{extra}</size>");
        }

        private void BuildBossBar()
        {
            var art = GameAssets.Ui;
            const float width = 980f, height = 151f;
            var root = UiKit.Image("BossBar", _root, art != null && art.dragonFrame != null ? Color.white : new Color(0.1f, 0.08f, 0.14f, 0.9f));
            if (art != null && art.dragonFrame != null) root.sprite = art.dragonFrame;
            UiKit.Place(root.rectTransform, new Vector2(0.5f, 1f), new Vector2(190f, -112f), new Vector2(width, height)); // 화면 위쪽, 왼쪽 위 상태 창(오른쪽 끝 약 644px)과 겹치지 않게 조금 오른쪽으로
            root.rectTransform.pivot = new Vector2(0.5f, 1f);
            _bossRoot = root.gameObject;

            // 틀 안쪽 홈(용 장식 틀 그림의 어두운 띠)에 채움 — 틀보다 먼저 그려야 하므로 틀의 형제로 앞에 둔다
            var groove = UiKit.Image("BossFillBack", root.rectTransform, new Color(0.05f, 0.03f, 0.08f, 0.9f)).rectTransform;
            groove.anchorMin = new Vector2(0.125f, 0.57f);
            groove.anchorMax = new Vector2(0.875f, 0.765f);
            groove.offsetMin = groove.offsetMax = Vector2.zero;
            _bossFill = UiKit.Image("BossFill", groove, new Color(1f, 0.62f, 0.2f));
            _bossFill.sprite = GameAssets.Square;
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            UiKit.Stretch(_bossFill.rectTransform);
            _bossText = UiKit.Text("BossText", groove, 26, TextAnchor.MiddleCenter);
            UiKit.Stretch(_bossText.rectTransform);
            _bossRoot.SetActive(false);
        }

        private void Update()
        {
            UpdateBossBar(Time.unscaledDeltaTime);
            float dt = Time.unscaledDeltaTime;
            UpdateStatus();
            _skillBar.Tick(dt);
            _tuningPanel.Tick();
            if (GameManager.Instance != null && !GameManager.Instance.IsGameOver && !GameManager.Instance.IsMenuOpen)
            {
                if (Input.GetKeyDown(Controls.TuningPanel)) { if (_tuningPanel.IsVisible) CloseSettings(); else OpenSettings(); }
                else if (Input.GetKeyDown(Controls.Pause) && _tuningPanel.IsVisible) CloseSettings(); // 설정창에서 ESC = 일시정지 메뉴로
            }
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
                SetBarFill(_hpFill, (float)_party.Hp / _party.MaxHp);
                _hpText.text = $"HP {_party.Hp} / {_party.MaxHp}";
                var cur = _party.Current;
                _combat ??= FindAnyObjectByType<PlayerCombat>();
                bool ranged = _combat != null && _combat.IsRanged;
                _characterText.text = $"{(ranged ? "원거리" : "근접")} · {cur.defenseName}{PassiveLabel(cur.kind, ranged)}";
                // 작은 글씨라 무기 색을 밝게 섞어 배경 위에서도 읽히게
                _characterText.color = Color.Lerp(_combat != null ? _combat.WeaponColor : cur.color, Color.white, 0.55f);
            }

            if (_gauge != null)
            {
                // 도력 구슬(염주): 찬 구슬은 은은하게 숨 쉬듯 빛나고, 가장 최근에 찬 구슬이 가장 밝다. 앞 10개는 옥빛, 뒤 10개는 귀문의 보라빛
                float t = Time.unscaledTime;
                // 도력이 늘었으면 새로 얻은 구슬만 잠깐 반짝이게 표시해 둔다
                for (int i = _lastGaugeValue; i < _gauge.Value && i < _orbSparkUntil.Length; i++) _orbSparkUntil[i] = t + OrbSparkTime;
                _lastGaugeValue = _gauge.Value;
                for (int i = 0; i < _gaugeCells.Count; i++)
                {
                    bool filled = i < _gauge.Value;
                    var core = _gaugeCells[i];
                    if (_orbSprites)
                    {
                        // 귀문 UI 구슬: 빈 구슬 위에 찬 구슬을 겹친다.
                        // 갖고 있는 구슬은 전부 똑같은 모양(개수가 한눈에 보이게) — 반짝임은 막 얻은 순간 0.6초만 나온다
                        var art = GameAssets.Ui;
                        bool sparking = filled && t < _orbSparkUntil[i];
                        core.sprite = sparking ? art.orbSpark : art.orbFull;
                        core.color = filled ? Color.white : Color.clear;
                        float k = sparking ? 1.5f - 0.5f * Mathf.Clamp01(1f - (_orbSparkUntil[i] - t) / OrbSparkTime) : 1f; // 커졌다가 제 크기로
                        core.rectTransform.localScale = Vector3.one * k;
                        if (sparking) core.transform.SetAsLastSibling();
                    }
                    else if (filled)
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
                SetBarFill(_xpFill, (float)_level.Xp / _level.XpToNext);
                _xpText.text = $"EXP {_level.Xp} / {_level.XpToNext}";
                _xpFlash = Mathf.Max(0f, _xpFlash - Time.unscaledDeltaTime * 4f);
                // 그림 바는 밝히기만 할 수 있어 평소를 살짝 어둡게 두고 경험치를 얻으면 원래 밝기로
                var xpBase = _orbSprites ? new Color(0.78f, 0.76f, 0.9f) : new Color(0.45f, 0.9f, 1f);
                _xpFill.color = Color.Lerp(xpBase, Color.white, _xpFlash);
            }
        }

        private PlayerCombat _combat;
        private bool _orbSprites;
        private const float OrbSparkTime = 0.6f;
        private readonly float[] _orbSparkUntil = new float[20];
        private int _lastGaugeValue;

        /// <summary>바 채움: 그림 바는 fillAmount로, 네모 바는 오른쪽 끝(anchorMax)으로 줄인다.</summary>
        private static void SetBarFill(Image fill, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (fill.type == Image.Type.Filled) fill.fillAmount = ratio;
            else fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        }

        /// <summary>기본값(배율 1, 감소 0)이 아닌 패시브만 표시. 예: "  공격 ×1.5 · 받는 피해 -30%" / "  이속 +30%"</summary>
        private static string PassiveLabel(CharacterKind kind, bool ranged)
        {
            var t = GameTuning.Current;
            // 실제로 곱해지는 배율 (초반 배율 × 무기 × 스킬 트리·아이템) — 성장하는 게 숫자로 보이게
            float atk = t.startDamageScale * (ranged ? t.rangedAttackMultiplier : t.AttackMultiplier(kind)) * SkillTreeState.AttackMultiplier(ranged);
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

            // 좌상단 상태: 네모 상자 대신 화면 모서리를 부드럽게 어둡게 해서(가장자리가 보이지 않는 그림자) 배경이 밝아도 읽히게
            var shade = UiKit.Image("StatusShade", _root, new Color(0f, 0f, 0f, 0.62f));
            shade.sprite = FxSprites.Glow;
            shade.rectTransform.anchorMin = shade.rectTransform.anchorMax = new Vector2(0f, 1f);
            shade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            shade.rectTransform.anchoredPosition = new Vector2(250f, -110f);
            shade.rectTransform.sizeDelta = new Vector2(1250f, 560f);

            // 좌상단 상태 패널: 체력 / 도력 / 레벨 / 무기 정보 (판 위에 바로 그린다)
            var panel = UiKit.Image("StatusPanel", _root, Color.clear).rectTransform;
            UiKit.Place(panel, new Vector2(0, 1), new Vector2(24, -24), new Vector2(620, 236));

            _characterText = UiKit.Text("Character", panel, 20, TextAnchor.UpperLeft);
            UiKit.Place(_characterText.rectTransform, new Vector2(0, 1), new Vector2(16, -200), new Vector2(590, 30));

            var ui = GameAssets.Ui;
            if (ui != null && ui.hpFrame != null && ui.hpFill != null && ui.orbEmpty != null && ui.orbFull != null)
                BuildStatusWithArt(panel, ui);
            else
                BuildStatusPlain(panel);

            // 하단 스킬바 (Shift / A / S / D)
            _skillBar = new SkillBar(_root);

            // 상호작용 안내 (스킬바 위)
            _promptBacking = UiKit.Image("PromptBacking", _root, new Color(0.05f, 0.03f, 0.12f, 0.8f));
            _promptBacking.rectTransform.anchorMin = _promptBacking.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _promptBacking.rectTransform.anchoredPosition = new Vector2(0, 330);
            _promptBacking.rectTransform.sizeDelta = new Vector2(620, 64);
            _promptText = UiKit.Text("Prompt", _promptBacking.rectTransform, 34, TextAnchor.MiddleCenter);
            UiKit.Stretch(_promptText.rectTransform);
            _promptText.color = new Color(0.85f, 0.78f, 1f);
            _promptBacking.gameObject.SetActive(false);

            // 판정 팝업
            _popupText = UiKit.Text("Popup", _root, 52, TextAnchor.MiddleCenter);
            _popupText.rectTransform.anchorMin = _popupText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _popupText.rectTransform.anchoredPosition = new Vector2(0, -170);
            _popupText.rectTransform.sizeDelta = new Vector2(1200, 100);
            _popupText.enabled = false;

            // 상단 중앙: 방 번호·종류 (런 중에만 채워진다)
            _runInfo = UiKit.Text("RunInfo", _root, 32, TextAnchor.UpperCenter);
            _runInfo.color = new Color(0.95f, 0.9f, 1f);
            UiKit.Place(_runInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 80f));
            _runInfo.rectTransform.pivot = new Vector2(0.5f, 1f);
            _runInfo.enabled = false;

            BuildBossBar();

            // 우상단 조작 안내 (스킬 키는 하단 스킬바에 표시되므로 이동·점프·일시정지만)
            var help = UiKit.Text("Help", _root, 22, TextAnchor.UpperRight);
            _help = help;
            help.color = new Color(1f, 1f, 1f, 0.55f);
            help.text = "←/→ 이동   Space 점프 (2단)   ↓+Space 발판 내려가기   Q 무기 전환   ESC 일시정지 · 설정";
            UiKit.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(900f, 32f));

            // 설정 창 (ESC 일시정지 메뉴의 "설정" 버튼 / F1) — 따로 뜨는 창
            _tuningPanel = new TuningPanel(_root);
            _tuningPanel.CloseRequested += CloseSettings;

            // 플레이 씬에서만 "사당으로" 버튼이 있다 (사당에서는 이미 사당)
            bool hubButton = !GameManager.InHub;
            var toHub = ("사당으로  (H)", (Action)(() => GameManager.LoadHub()));
            var pauseButtons = new List<(string, Action)>
            {
                ("계속  (ESC)", () => GameManager.Instance?.SetPaused(false)),
                ("다시 시작  (R)", () => GameManager.Instance?.Restart()),
            };
            var settings = ("설정  (F1)", (Action)OpenSettings);
            var overButtons = new List<(string, Action)> { ("다시 시작  (R)", () => GameManager.Instance?.Restart()) };
            if (hubButton)
            {
                pauseButtons.Add(toHub);
                overButtons.Add(toHub);
            }
            pauseButtons.Add(settings);
            pauseButtons.Add(("시작 화면  (M)", () => GameManager.Instance?.GoToStartMenu()));
            overButtons.Add(("시작 화면  (M)", () => GameManager.Instance?.GoToStartMenu()));
            string hubHint = hubButton ? "   H 사당" : "";
            _pausePanel = CreateOverlay("PausePanel", $"일시 정지\n<size=28>ESC 계속   F1 설정   R 다시 시작{hubHint}   M 시작 화면</size>", pauseButtons.ToArray());
            _gameOverPanel = CreateOverlay("GameOverPanel", $"쓰러졌다\n<size=28>R 다시 시작{hubHint}   M 시작 화면</size>", overButtons.ToArray());
        }

        /// <summary>그림이 없을 때의 예전 모양 (네모 바·글자 도력).</summary>
        private void BuildStatusPlain(RectTransform panel)
        {
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
        }

        /// <summary>귀문 UI 그림: 옥 장식 체력 틀 + 초록 채움, 구슬 도력, 구리 바 + 보라 채움 경험치.</summary>
        private void BuildStatusWithArt(RectTransform panel, UiArt ui)
        {
            _orbSprites = true;
            UiKit.Place(panel, new Vector2(0, 1), new Vector2(30, -26), new Vector2(620, 236));

            // 체력: 하트 + 틀 (틀의 어두운 홈 위에 초록 바가 차오른다)
            var heart = UiKit.Image("HeartIcon", panel, Color.white);
            heart.sprite = ui.iconHeart;
            heart.preserveAspect = true;
            UiKit.Place(heart.rectTransform, new Vector2(0, 1), new Vector2(12, -6), new Vector2(40, 64));
            var frame = UiKit.Image("HpFrame", panel, Color.white);
            frame.sprite = ui.hpFrame;
            UiKit.Place(frame.rectTransform, new Vector2(0, 1), new Vector2(62, -12), new Vector2(544, 63));
            _hpFill = UiKit.Image("HpFill", frame.rectTransform, Color.white);
            _hpFill.sprite = ui.hpFill;
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            // 틀 그림(924×107) 안쪽 홈: 가로 9.9%~90%, 세로 28%~78.5%
            _hpFill.rectTransform.anchorMin = new Vector2(0.099f, 0.215f);
            _hpFill.rectTransform.anchorMax = new Vector2(0.9f, 0.72f);
            _hpFill.rectTransform.offsetMin = _hpFill.rectTransform.offsetMax = Vector2.zero;
            _hpText = UiKit.Text("HpText", frame.rectTransform, 22, TextAnchor.MiddleCenter);
            _hpText.rectTransform.anchorMin = _hpFill.rectTransform.anchorMin;
            _hpText.rectTransform.anchorMax = _hpFill.rectTransform.anchorMax;
            _hpText.rectTransform.offsetMin = _hpText.rectTransform.offsetMax = Vector2.zero;

            // 도력 구슬 20개 (빈 구슬을 먼저 모두 깔고, 그 위에 찬 구슬을 얹는다)
            const float cell = 26f, gap = 3f, midGap = 10f, rowY = -84f;
            var thread = UiKit.Image("GaugeThread", panel, new Color(0.55f, 0.36f, 0.22f, 0.9f)).rectTransform;
            UiKit.Place(thread, new Vector2(0, 1), new Vector2(10, rowY - cell * 0.5f + 1f), new Vector2(604, 3));
            for (int i = 0; i < 20; i++)
            {
                float x = 16 + i * (cell + gap) + (i >= 10 ? midGap : 0f);
                var empty = UiKit.Image($"GaugeRing{i}", panel, Color.white);
                empty.sprite = ui.orbEmpty;
                UiKit.Place(empty.rectTransform, new Vector2(0, 1), new Vector2(x, rowY), new Vector2(cell, cell));
                _gaugeRings.Add(empty);
            }
            for (int i = 0; i < 20; i++)
            {
                float x = 16 + i * (cell + gap) + (i >= 10 ? midGap : 0f);
                var full = UiKit.Image($"Gauge{i}", panel, Color.clear);
                full.sprite = ui.orbFull;
                // 기준점을 구슬 가운데로 둔다 — 반짝이는 구슬이 커질 때 왼쪽 위 모서리가 아니라 제자리에서 커지게
                UiKit.Place(full.rectTransform, new Vector2(0, 1), new Vector2(x + cell * 0.5f, rowY - cell * 0.5f), new Vector2(cell, cell));
                full.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _gaugeCells.Add(full);
            }
            var lantern = UiKit.Image("LanternIcon", panel, Color.white);
            lantern.sprite = ui.iconLantern;
            lantern.preserveAspect = true;
            UiKit.Place(lantern.rectTransform, new Vector2(0, 1), new Vector2(14, -112), new Vector2(22, 44));
            _gaugeText = UiKit.Text("GaugeText", panel, 22, TextAnchor.MiddleLeft);
            UiKit.Place(_gaugeText.rectTransform, new Vector2(0, 1), new Vector2(42, -118), new Vector2(560, 32));

            // 레벨 + 경험치: 구리 바 위에 보라 채움
            _levelText = UiKit.Text("Level", panel, 26, TextAnchor.MiddleLeft);
            _levelText.fontStyle = FontStyle.Bold;
            _levelText.color = new Color(0.8f, 0.7f, 1f);
            UiKit.Place(_levelText.rectTransform, new Vector2(0, 1), new Vector2(16, -158), new Vector2(80, 28));
            var xpBg = UiKit.Image("XpBg", panel, Color.white);
            xpBg.sprite = ui.barEmpty != null ? ui.barEmpty : null;
            UiKit.Place(xpBg.rectTransform, new Vector2(0, 1), new Vector2(96, -160), new Vector2(508, 24));
            _xpFill = UiKit.Image("XpFill", xpBg.rectTransform, Color.white);
            _xpFill.sprite = ui.purpleFill;
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            _xpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _xpFill.rectTransform.anchorMin = new Vector2(0.006f, 0.12f);
            _xpFill.rectTransform.anchorMax = new Vector2(0.994f, 0.88f);
            _xpFill.rectTransform.offsetMin = _xpFill.rectTransform.offsetMax = Vector2.zero;
            _xpText = UiKit.Text("XpText", xpBg.rectTransform, 16, TextAnchor.MiddleCenter);
            UiKit.Stretch(_xpText.rectTransform);
            UiKit.Place(_characterText.rectTransform, new Vector2(0, 1), new Vector2(16, -194), new Vector2(590, 30));
        }


        /// <summary>화면 전체를 덮는 안내 + 아래 버튼들 (키로도 되고 마우스로도 된다). 버튼은 가운데 줄로 늘어선다.</summary>
        private GameObject CreateOverlay(string name, string message, params (string label, Action action)[] buttons)
        {
            var bg = UiKit.Image(name, _root, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(bg.rectTransform);
            var text = UiKit.Text("Text", bg.rectTransform, 72, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform);
            text.text = message;

            const float width = 310f, height = 70f, gap = 20f;
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
