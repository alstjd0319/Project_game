using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 디버그 겸 기본 HUD. 코드로 uGUI 캔버스를 만든다.
    /// 체력 / 공용 게이지(20칸) / 현재 캐릭터 / 스킬바 / 판정 팝업 / 월드 텍스트 / 일시정지·게임오버.
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
        private SkillBar _skillBar;
        private AugmentChoiceUI _augmentUi;
        private TuningPanel _tuningPanel;
        private AugmentListPanel _augmentList;
        private PlayerLevel _level;
        private AugmentManager _augments;
        private Text _levelText;
        private Image _xpFill;
        private Text _xpText;
        private Text _augmentListText;
        private float _xpFlash;
        private int _shownAugmentCount = -1;
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
            _augments = FindAnyObjectByType<AugmentManager>();
            _augmentUi.Bind(_augments, _level);
            _augmentList.Bind(_augments);
            if (_level != null) _level.XpGained += _ => _xpFlash = 1f;
            _tuningPanel.Bind(FindAnyObjectByType<PlayerDefense>(), _party, _gauge, _level, _augments);

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
            _augmentUi.Tick(dt);
            _tuningPanel.Tick();
            _augmentList.Tick();
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
                _characterText.text = $"{cur.displayName}  <size=22>[{cur.defenseName}]{PassiveLabel(cur.kind)}</size>";
                _characterText.color = cur.color;
            }

            if (_gauge != null)
            {
                for (int i = 0; i < _gaugeCells.Count; i++)
                {
                    bool filled = i < _gauge.Value;
                    _gaugeCells[i].color = filled
                        ? (i < 10 ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.55f, 0.2f))
                        : new Color(1f, 1f, 1f, 0.12f);
                }
                _gaugeText.text = SkillGauge.Infinite
                    ? $"게이지 {_gauge.Value} / {_gauge.Max}  <color=#FFD966>∞ 무한 (연습)</color>"
                    : $"게이지 {_gauge.Value} / {_gauge.Max}";
            }

            if (_level != null)
            {
                _levelText.text = $"Lv.{_level.Level}";
                _xpFill.rectTransform.anchorMax = new Vector2((float)_level.Xp / _level.XpToNext, 1f);
                _xpText.text = $"EXP {_level.Xp} / {_level.XpToNext}";
                _xpFlash = Mathf.Max(0f, _xpFlash - Time.unscaledDeltaTime * 4f);
                _xpFill.color = Color.Lerp(new Color(0.45f, 0.9f, 1f), Color.white, _xpFlash);
            }

            if (_augments != null && _augments.Acquired.Count != _shownAugmentCount)            {
                // 자세한 목록은 C 창에서. 여기엔 개수만 (증강이 쌓여도 패널을 넘치지 않게)
                _shownAugmentCount = _augments.Acquired.Count;
                _augmentListText.text = _shownAugmentCount == 0
                    ? "<color=#888888>증강 없음  ·  [C] 목록</color>"
                    : $"증강 {_shownAugmentCount}개  <color=#999999>·  [C] 목록</color>";
            }
        }

        /// <summary>기본값(배율 1, 감소 0)이 아닌 패시브만 표시. 예: "  공격 ×1.5 · 받는 피해 -30%" / "  이속 +30%"</summary>
        private static string PassiveLabel(CharacterKind kind)
        {
            var t = GameTuning.Current;
            float atk = t.AttackMultiplier(kind);
            float red = t.DamageReduction(kind);
            var parts = new List<string>();
            if (!Mathf.Approximately(atk, 1f)) parts.Add($"공격 ×{atk:0.##}");
            if (red > 0.001f) parts.Add($"받는 피해 -{red * 100f:0}%");
            // 이동 속도는 전사 대비로 (전사가 기준)
            float speedRatio = t.MoveSpeed(kind) / t.MoveSpeed(CharacterKind.Warrior);
            if (kind != CharacterKind.Warrior && speedRatio > 1.001f) parts.Add($"이속 +{(speedRatio - 1f) * 100f:0}%");
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

            // 좌상단 상태 패널: 캐릭터 / 체력 / 게이지
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

            // 게이지 20칸 (10칸째 뒤에 구분 간격 → 수동 스왑 기준점)
            const float cell = 24f, gap = 4f, midGap = 12f;
            for (int i = 0; i < 20; i++)
            {
                var img = UiKit.Image($"Gauge{i}", panel, Color.white);
                float x = 16 + i * (cell + gap) + (i >= 10 ? midGap : 0f);
                UiKit.Place(img.rectTransform, new Vector2(0, 1), new Vector2(x, -104), new Vector2(cell, cell));
                _gaugeCells.Add(img);
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

            // 획득한 증강 목록
            _augmentListText = UiKit.Text("Augments", panel, 20, TextAnchor.UpperLeft);
            _augmentListText.color = new Color(1f, 0.8f, 0.25f, 0.9f);
            UiKit.Place(_augmentListText.rectTransform, new Vector2(0, 1), new Vector2(16, -200), new Vector2(590, 28));

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
            help.text = "←/→ 이동   Space 점프 (2단)   ↓+Space 발판 내려가기   ESC 일시정지   C 증강 목록   F1 튜닝";
            UiKit.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(900f, 32f));

            // C 증강 목록 (증강 선택 화면의 어두운 막 아래에 깔리도록 먼저 생성)
            _augmentList = new AugmentListPanel(_root);

            // 증강 선택 화면 (일시정지·게임오버 패널보다 아래)
            _augmentUi = new AugmentChoiceUI(_root);

            // F1 튜닝 패널
            _tuningPanel = new TuningPanel(_root);

            _pausePanel = CreateOverlay("PausePanel", "일시 정지\n<size=28>ESC 계속   R 재시작</size>");
            _gameOverPanel = CreateOverlay("GameOverPanel", "쓰러졌다\n<size=28>R 재시작</size>");
        }

        private GameObject CreateOverlay(string name, string message)
        {
            var bg = UiKit.Image(name, _root, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(bg.rectTransform);
            var text = UiKit.Text("Text", bg.rectTransform, 72, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform);
            text.text = message;
            bg.gameObject.SetActive(false);
            return bg.gameObject;
        }
    }
}
