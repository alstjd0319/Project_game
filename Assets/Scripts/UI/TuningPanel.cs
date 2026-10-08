using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// F1 튜닝 패널. 플레이 중에 타격감·판정·적 수치를 슬라이더로 바꾸고, 바꾸는 즉시 GameTuning에 반영 + 자동 저장.
    /// 연습 스위치(무적 등)는 저장하지 않는다.
    /// </summary>
    public class TuningPanel
    {
        public static TuningPanel Instance { get; private set; }

        private const float Width = 560f;
        private const float RowHeight = 44f;
        private const float SaveDelay = 0.4f;

        private static readonly Color PanelColor = new(0.06f, 0.07f, 0.1f, 0.88f);
        private static readonly Color TabOff = new(0.2f, 0.22f, 0.28f);
        private static readonly Color TabOn = new(0.95f, 0.75f, 0.25f);
        private static readonly Color Accent = new(0.45f, 0.9f, 1f);

        private static readonly string[] TabNames = { "타격감", "판정", "캐릭터", "적", "연습", "증강" };
        public const int AugmentTab = 5;
        private static readonly string[] ProfileNames = { "일반 패링", "강공격 패링", "피격", "스킬 적중" };
        private const int ProfilesPerRow = 3;

        // ───────────── 행 ─────────────

        private abstract class Row
        {
            public abstract void Refresh();
        }

        private sealed class SliderRow : Row
        {
            public Slider slider;
            public Text value;
            public Func<float> get;
            public string format;

            public override void Refresh()
            {
                float v = get();
                slider.SetValueWithoutNotify(v);
                value.text = v.ToString(format);
            }
        }

        private sealed class ToggleRow : Row
        {
            public Image box;
            public Text check;
            public Func<bool> get;

            public override void Refresh()
            {
                bool on = get();
                box.color = on ? TabOn : new Color(1f, 1f, 1f, 0.15f);
                check.text = on ? "√" : "";
            }
        }

        /// <summary>증강 골라 받기 버튼 하나 — 중첩 수를 보여준다.</summary>
        private sealed class AugmentRow : Row
        {
            public AugmentDefinition def;
            public Image image;
            public Text label;
            public Func<AugmentManager> manager;
            public Color color;

            public override void Refresh()
            {
                var m = manager();
                int stacks = m != null ? m.StacksOf(def) : 0;
                bool maxed = stacks >= def.MaxStacks;
                string count = stacks > 0 ? $"<color=#FFD966>{stacks}/{def.MaxStacks}</color>" : $"<color=#999999>0/{def.MaxStacks}</color>";
                label.text = maxed ? $"{def.Name}  <color=#FFD966>최대</color>" : $"{def.Name}  {count}";
                image.color = maxed ? color * new Color(0.5f, 0.5f, 0.5f, 1f) : color;
            }
        }

        // ───────────── 상태 ─────────────

        private readonly GameObject _root;
        private readonly RectTransform _content;
        private readonly RectTransform _viewport;
        private readonly RectTransform _panelRect;
        private readonly ScrollRect _scroll;
        private readonly List<float> _pageHeights = new();
        private readonly List<Button> _tabButtons = new();
        private readonly List<RectTransform> _tabPages = new();
        private readonly List<List<Row>> _tabRows = new();
        private readonly List<Image> _profileButtons = new();
        private readonly Text _status;
        private readonly Text _timingLog;
        private readonly Text _resetLabel;
        private readonly Queue<string> _timings = new();

        private int _tab;
        private int _profile;
        private float _dirtySince = -1f;
        private float _resetArmedUntil = -1f;
        private float _y;
        /// <summary>
        /// 내용 영역 최대 높이 — 넘치면 휠 스크롤. 패널 아래끝이 바닥에 선 적의 머리 위(1080p 기준 약 710px)에서 멈추는 값.
        /// 튜닝하는 동안 판정 대상(적·공격)이 패널에 가리면 튜닝 자체가 의미 없어지므로.
        /// </summary>
        private const float MaxContentHeight = 420f;
        private PlayerDefense _defense;
        private PlayerParty _party;
        private SkillGauge _gauge;
        private PlayerLevel _level;
        private AugmentManager _augments;
        private Text _augmentInfo;

        private static GameTuning T => GameTuning.Current;

        private HitFeelProfile SelectedProfile => _profile switch
        {
            1 => T.heavyParry,
            2 => T.playerHurt,
            3 => T.skillHit,
            _ => T.normalParry,
        };

        private bool SelectedIsHeavy => _profile == 1;

        public bool IsVisible => _root.activeSelf;

        public TuningPanel(RectTransform canvasRoot)
        {
            Instance = this;
            EnsureEventSystem();

            var bg = UiKit.Image("TuningPanel", canvasRoot, PanelColor);
            bg.raycastTarget = true; // 패널 위 클릭이 뒤로 새지 않게
            _root = bg.gameObject;
            var root = bg.rectTransform;
            UiKit.Place(root, new Vector2(1f, 1f), new Vector2(-24f, -72f), new Vector2(Width, 700f));

            var title = UiKit.Text("Title", root, 30, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.color = Accent;
            title.text = "튜닝 패널  <size=20><color=#999999>F1로 닫기</color></size>";
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(Width - 40f, 40f));

            _status = UiKit.Text("Status", root, 17, TextAnchor.MiddleLeft, outline: false);
            _status.color = new Color(1f, 1f, 1f, 0.5f);
            UiKit.Place(_status.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -54f), new Vector2(Width - 40f, 24f));

            // 탭
            float tabWidth = (Width - 40f - (TabNames.Length - 1) * 8f) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var tab = UiKit.Button($"Tab_{TabNames[i]}", root, TabNames[i], 22, TabOff);
                UiKit.Place(tab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(20f + i * (tabWidth + 8f), -88f), new Vector2(tabWidth, 42f));
                tab.onClick.AddListener(() => SelectTab(index));
                _tabButtons.Add(tab);
            }

            _panelRect = root;
            var viewportImage = UiKit.Image("Viewport", root, new Color(0f, 0f, 0f, 0f));
            viewportImage.raycastTarget = true; // 휠 입력
            _viewport = viewportImage.rectTransform;
            UiKit.Place(_viewport, new Vector2(0f, 1f), new Vector2(20f, -146f), new Vector2(Width - 40f, MaxContentHeight));
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(_viewport, false);
            UiKit.Place(_content, new Vector2(0f, 1f), Vector2.zero, new Vector2(Width - 40f, MaxContentHeight));

            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false;
            _scroll.scrollSensitivity = 40f;

            BuildHitFeelTab();
            BuildJudgeTab(out _timingLog);
            BuildCharacterTab();
            BuildEnemyTab();
            BuildPracticeTab();
            BuildAugmentTab();
            EndPage();

            // 패널 높이는 SelectTab에서 지금 탭 내용에 맞춘다 (상한 넘으면 스크롤)

            // 하단: 기본값으로 (두 번 눌러야 실행)
            var reset = UiKit.Button("Reset", root, "기본값으로", 20, new Color(0.45f, 0.18f, 0.18f));
            UiKit.Place(reset.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(230f, 40f));
            _resetLabel = reset.GetComponentInChildren<Text>();
            reset.onClick.AddListener(OnResetClicked);

            var hint = UiKit.Text("Hint", root, 16, TextAnchor.MiddleRight, outline: false);
            hint.color = new Color(1f, 1f, 1f, 0.4f);
            hint.text = "바꾸는 즉시 적용 · 자동 저장\n연습 스위치는 저장 안 됨";
            UiKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-20f, 18f), new Vector2(280f, 40f));

            GameTuning.Changed += RefreshAll;
            SelectTab(0);
            UpdateStatus("");
            _root.SetActive(false);
        }

        public void Bind(PlayerDefense defense, PlayerParty party, SkillGauge gauge, PlayerLevel level, AugmentManager augments)
        {
            _defense = defense;
            _party = party;
            _gauge = gauge;
            _level = level;
            _augments = augments;
            if (_defense != null) _defense.TimingReported += OnTiming;
            if (_augments != null)
            {
                // 레벨업 선택으로 얻어도 탭의 중첩 표시가 맞게
                _augments.AugmentAcquired += _ => RefreshAll();
                _augments.AugmentsCleared += RefreshAll;
            }
        }

        public void Dispose() => GameTuning.Changed -= RefreshAll;

        public void SetVisible(bool visible)
        {
            _root.SetActive(visible);
            if (visible) RefreshAll();
        }

        public void SelectTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _tabPages.Count - 1);
            for (int i = 0; i < _tabPages.Count; i++)
            {
                _tabPages[i].gameObject.SetActive(i == _tab);
                _tabButtons[i].GetComponent<Image>().color = i == _tab ? TabOn : TabOff;
                _tabButtons[i].GetComponentInChildren<Text>().color = i == _tab ? new Color(0.1f, 0.08f, 0f) : Color.white;
            }
            FitToPage();
            RefreshAll();
        }

        /// <summary>지금 탭 높이에 맞춰 패널 크기를 정하고, 상한을 넘으면 스크롤로.</summary>
        private void FitToPage()
        {
            if (_tab >= _pageHeights.Count) return;
            float pageHeight = _pageHeights[_tab];
            float viewHeight = Mathf.Min(pageHeight, MaxContentHeight);
            _content.sizeDelta = new Vector2(Width - 40f, pageHeight);
            _viewport.sizeDelta = new Vector2(Width - 40f, viewHeight);
            _panelRect.sizeDelta = new Vector2(Width, 146f + viewHeight + 76f);
            _scroll.verticalNormalizedPosition = 1f;
            IsScrollable = pageHeight > MaxContentHeight;
        }

        public bool IsScrollable { get; private set; }
        public int TabCount => _tabPages.Count;
        public float PanelHeight => _panelRect.sizeDelta.y;

        /// <summary>Hud.Update에서 호출 (실시간 dt).</summary>
        public void Tick()
        {
            if (Input.GetKeyDown(Controls.TuningPanel)) SetVisible(!IsVisible);

            // 클릭한 UI가 선택된 채 남으면 Space/Enter가 그 버튼을 다시 누르므로 매번 선택 해제
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && !Input.GetMouseButton(0))
                es.SetSelectedGameObject(null);

            if (_dirtySince >= 0f && Time.unscaledTime - _dirtySince >= SaveDelay)
            {
                _dirtySince = -1f;
                GameTuning.Save();
                UpdateStatus($"저장됨 {DateTime.Now:HH:mm:ss}");
            }

            if (_resetArmedUntil >= 0f && Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = -1f;
                _resetLabel.text = "기본값으로";
            }
        }

        // ───────────── 탭 구성 ─────────────

        private void BuildHitFeelTab()
        {
            BeginPage();
            Header("어떤 순간의 타격감?");
            int rows = (ProfileNames.Length + ProfilesPerRow - 1) / ProfilesPerRow;
            var rowRt = NewRowRect(rows * 44f);
            float w = (Width - 40f - (ProfilesPerRow - 1) * 8f) / ProfilesPerRow;
            for (int i = 0; i < ProfileNames.Length; i++)
            {
                int index = i;
                var b = UiKit.Button($"Profile_{i}", rowRt, ProfileNames[i], 19, TabOff);
                var pos = new Vector2((i % ProfilesPerRow) * (w + 8f), -(i / ProfilesPerRow) * 44f);
                UiKit.Place(b.GetComponent<RectTransform>(), new Vector2(0f, 1f), pos, new Vector2(w, 38f));
                b.onClick.AddListener(() => SelectProfile(index));
                _profileButtons.Add(b.GetComponent<Image>());
            }
            Space(8f);

            AddSlider("히트스탑 (초)", 0f, 0.2f, 0.005f, "0.000", () => SelectedProfile.hitStop, v => SelectedProfile.hitStop = v);
            AddSlider("슬로우모션 길이 (초)", 0f, 0.4f, 0.01f, "0.00", () => SelectedProfile.slowMotion, v => SelectedProfile.slowMotion = v);
            AddSlider("슬로우모션 속도 (배)", 0.05f, 1f, 0.05f, "0.00", () => SelectedProfile.slowScale, v => SelectedProfile.slowScale = v);
            AddSlider("화면 흔들림 세기", 0f, 1f, 0.02f, "0.00", () => SelectedProfile.shake, v => SelectedProfile.shake = v);
            AddSlider("화면 흔들림 길이 (초)", 0f, 0.6f, 0.01f, "0.00", () => SelectedProfile.shakeDuration, v => SelectedProfile.shakeDuration = v);
            Space(10f);
            AddButton("▶  미리보기 (지금 설정으로 한 번 재생)", Preview);
            SelectProfile(0);
        }

        private void BuildJudgeTab(out Text timingLog)
        {
            BeginPage();
            Header("방어 판정");
            AddSlider("방어 활성 시간 (초)", 0.1f, 0.6f, 0.01f, "0.00", () => T.defenseActiveTime, v => T.defenseActiveTime = v);
            AddSlider("헛스윙 쿨타임 (초)", 0f, 2f, 0.05f, "0.00", () => T.whiffCooldown, v => T.whiffCooldown = v);
            AddToggle("입력 버퍼 (살짝 이른 입력 보정)", () => T.inputBufferEnabled, v => T.inputBufferEnabled = v, persist: true);
            AddSlider("버퍼 길이 (초)", 0f, 0.2f, 0.005f, "0.000", () => T.inputBuffer, v => T.inputBuffer = v);
            AddToggle("판정 타이밍 표시 (캐릭터 머리 위)", () => T.showTiming, v => T.showTiming = v, persist: true);
            AddToggle("이동 스킬(대시) 중 무적", () => T.dashInvulnerable, v => T.dashInvulnerable = v, persist: true);
            AddSlider("대시 후 무적 여유 (초)", 0f, 0.3f, 0.01f, "0.00", () => T.dashInvulnerableExtra, v => T.dashInvulnerableExtra = v);
            Space(10f);
            Header("최근 판정");
            timingLog = UiKit.Text("TimingLog", _tabPages[^1], 20, TextAnchor.UpperLeft, outline: false);
            timingLog.color = new Color(1f, 1f, 1f, 0.8f);
            timingLog.lineSpacing = 1.1f;
            UiKit.Place(timingLog.rectTransform, new Vector2(0f, 1f), new Vector2(8f, _y), new Vector2(Width - 60f, 200f));
            timingLog.text = "<color=#777777>아직 없음 — 방어에 성공하거나 너무 일찍/늦게 누르면 여기 쌓인다</color>";
            Space(190f);
        }

        private void BuildCharacterTab()
        {
            BeginPage();
            Header("플레이어 — 근접은 강하고 단단함 / 원거리는 멀리서 안전하게");
            AddSlider("공격력 배율", 0.5f, 3f, 0.05f, "0.00", () => T.playerAttackMultiplier, v => T.playerAttackMultiplier = v);
            AddSlider("받는 피해 감소", 0f, 0.8f, 0.05f, "0%", () => T.playerDamageReduction, v => T.playerDamageReduction = v);
            AddSlider("이동 속도", 2f, 10f, 0.25f, "0.00", () => T.playerMoveSpeed, v => T.playerMoveSpeed = v);
            AddSlider("원거리 공격력 배율", 0.5f, 3f, 0.05f, "0.00", () => T.rangedAttackMultiplier, v => T.rangedAttackMultiplier = v);
            AddSlider("무기 전환 쿨타임 (초)", 0f, 2f, 0.05f, "0.00", () => T.weaponSwitchCooldown, v => T.weaponSwitchCooldown = v);
            Space(10f);
            var note = UiKit.Text("Note", Page, 17, TextAnchor.UpperLeft, outline: false);
            note.color = new Color(1f, 1f, 1f, 0.5f);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.text = "공격력 배율은 반격과 일반공격에 곱해진다.\n받는 피해 감소는 맞는 순간 적용된다.";
            UiKit.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(8f, _y), new Vector2(Width - 60f, 84f));
            Space(88f);
        }

        private void BuildEnemyTab()
        {
            BeginPage();
            Header("몬스터 수 (근접 · 원거리 각각)");
            AddSlider("동시에 유지할 수", 1f, 10f, 1f, "0", () => T.enemiesPerKind, v => T.enemiesPerKind = Mathf.RoundToInt(v));
            Header("근접 — 예비 모션 = 반응시간 (베기는 한 번에)");
            AddSlider("일반 예비 모션 (초)", 0.15f, 1f, 0.01f, "0.00", () => T.meleeNormalReaction, v => T.meleeNormalReaction = v);
            AddSlider("강공격 예비 모션 (초)", 0.3f, 1.5f, 0.01f, "0.00", () => T.meleeHeavyReaction, v => T.meleeHeavyReaction = v);
            Header("공격 빈도 / 예비 모션");
            AddSlider("강공격 확률", 0f, 1f, 0.05f, "0%", () => T.heavyChance, v => T.heavyChance = v);
            AddSlider("공격 간격 최소 (초)", 0.3f, 4f, 0.05f, "0.00", () => T.attackIntervalMin, v => T.attackIntervalMin = v);
            AddSlider("공격 간격 최대 (초)", 0.3f, 5f, 0.05f, "0.00", () => T.attackIntervalMax, v => T.attackIntervalMax = v);
            AddSlider("원거리 예비 · 일반 (초)", 0f, 0.6f, 0.01f, "0.00", () => T.normalWindup, v => T.normalWindup = v);
            AddSlider("원거리 예비 · 강공격 (초)", 0f, 1f, 0.01f, "0.00", () => T.heavyWindup, v => T.heavyWindup = v);
            Header("원거리 투사체 속도 (유닛/초)");
            AddSlider("일반", 2f, 16f, 0.5f, "0.0", () => T.rangedNormalSpeed, v => T.rangedNormalSpeed = v);
            AddSlider("강공격", 2f, 16f, 0.5f, "0.0", () => T.rangedHeavySpeed, v => T.rangedHeavySpeed = v);
        }

        private void BuildPracticeTab()
        {
            BeginPage();
            Header("연습 스위치 (저장 안 됨)");
            AddToggle("무적 (체력이 줄지 않음)", () => T.godMode, v => T.godMode = v, persist: false);
            AddToggle("적 무한 체력", () => T.enemyInvincible, v => T.enemyInvincible = v, persist: false);
            AddToggle("강공격만 나오기", () => T.heavyOnly, v => T.heavyOnly = v, persist: false);
            AddToggle("적이 공격하지 않음", () => T.enemyPassive, v => T.enemyPassive = v, persist: false);
            AddToggle("근접 몬스터 끄기 (원거리만 연습)", () => T.hideMelee, v => T.hideMelee = v, persist: false);
            AddToggle("원거리 몬스터 끄기 (근접만 연습)", () => T.hideRanged, v => T.hideRanged = v, persist: false);
            AddToggle("게이지 무한 (써도 줄지 않음)", () => T.infiniteGauge, v =>
            {
                T.infiniteGauge = v;
                if (v && _gauge != null) _gauge.Add(_gauge.Max); // 켜면 가득 채워서 보이는 것도 맞게
            }, persist: false);
            AddToggle("판정 상자 보기 (그림 위에 겹쳐 표시)", () => T.showHitboxes, v => T.showHitboxes = v, persist: false);
            Space(10f);
            Header("바로 실행");
            AddButton("게이지 가득 채우기", () => { if (_gauge != null) _gauge.Add(_gauge.Max); });
            AddButton("체력 회복", () => { if (_party != null) _party.RestoreFullHp(); });
            AddButton("레벨업 +1 (증강 선택 확인용)", () => { if (_level != null) _level.AddXp(_level.XpToNext - _level.Xp); });
        }

        private void BuildAugmentTab()
        {
            BeginPage();
            Header("증강 골라 받기 — 누를 때마다 1중첩 (저장 안 됨)");

            _augmentInfo = UiKit.Text("AugmentInfo", Page, 17, TextAnchor.UpperLeft, outline: false);
            _augmentInfo.color = new Color(1f, 1f, 1f, 0.6f);
            _augmentInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(_augmentInfo.rectTransform, new Vector2(0f, 1f), new Vector2(8f, _y - 2f), new Vector2(Width - 60f, 44f));
            _augmentInfo.text = "레벨업 선택과 똑같이 효과가 붙는다. 받은 증강은 C 목록 창에도 바로 나온다.";
            Space(46f);
            AddButton("획득한 증강 전부 제거", () =>
            {
                if (_augments == null) return;
                _augments.ClearAll();
                _augmentInfo.text = "증강을 전부 제거했다.";
            });

            AddAugmentGroup(AugmentOwner.Common, "공용", new Color(0.3f, 0.33f, 0.42f));
            AddAugmentGroup(AugmentOwner.Player, "플레이어", new Color(0.5f, 0.2f, 0.18f));
        }

        /// <summary>그룹 이름은 왼쪽 칸에 — 제목 줄을 따로 두면 12개가 한 화면(높이 상한)에 안 들어간다.</summary>
        private void AddAugmentGroup(AugmentOwner owner, string title, Color color)
        {
            const int columns = 2;
            const float labelWidth = 56f;
            var defs = new List<AugmentDefinition>();
            foreach (var d in AugmentCatalog.All) if (d.Owner == owner) defs.Add(d);

            int rows = (defs.Count + columns - 1) / columns;
            var rowRt = NewRowRect(rows * 44f);
            Space(6f);

            var label = UiKit.Text("GroupLabel", rowRt, 20, TextAnchor.MiddleLeft, outline: false);
            label.color = Color.Lerp(color, Color.white, 0.55f);
            label.fontStyle = FontStyle.Bold;
            UiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(labelWidth, rows * 44f - 6f));
            label.text = title;

            float w = (Width - 40f - labelWidth - (columns - 1) * 8f) / columns;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                var b = UiKit.Button($"Augment_{def.Id}", rowRt, def.Name, 19, color);
                var pos = new Vector2(labelWidth + (i % columns) * (w + 8f), -(i / columns) * 44f);
                UiKit.Place(b.GetComponent<RectTransform>(), new Vector2(0f, 1f), pos, new Vector2(w, 38f));
                var row = new AugmentRow
                {
                    def = def, image = b.GetComponent<Image>(), label = b.GetComponentInChildren<Text>(),
                    manager = () => _augments, color = color,
                };
                row.label.supportRichText = true;
                b.onClick.AddListener(() => GrantFromPanel(def));
                _tabRows[^1].Add(row);
            }
        }

        /// <summary>튜닝 패널에서 증강 하나를 바로 받는다 (테스트에서도 호출).</summary>
        public bool GrantFromPanel(AugmentDefinition def)
        {
            if (_augments == null) return false;
            bool ok = _augments.TryGrant(def);
            _augmentInfo.text = ok
                ? $"<color=#FFD966>{def.Name}</color> ({def.OwnerLabel}) — {def.Description}"
                : $"{def.Name}은(는) 이미 최대 중첩({def.MaxStacks})이다.";
            if (ok) Sfx.Play(SfxId.LevelUp, 0.5f);
            RefreshAll();
            return ok;
        }

        // ───────────── 행 빌더 ─────────────

        private void EndPage() => _pageHeights.Add(-_y);

        private void BeginPage()
        {
            if (_tabPages.Count > 0) EndPage(); // 앞 페이지 높이 기록
            var page = new GameObject($"Page_{_tabPages.Count}", typeof(RectTransform)).GetComponent<RectTransform>();
            page.SetParent(_content, false);
            UiKit.Stretch(page);
            _tabPages.Add(page);
            _tabRows.Add(new List<Row>());
            _y = 0f;
        }

        private RectTransform Page => _tabPages[^1];

        private RectTransform NewRowRect(float height = RowHeight)
        {
            var rt = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(Page, false);
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, _y), new Vector2(Width - 40f, height));
            _y -= height;
            return rt;
        }

        private void Space(float h) => _y -= h;

        private void Header(string text)
        {
            var rt = NewRowRect(36f);
            var t = UiKit.Text("Header", rt, 20, TextAnchor.LowerLeft, outline: false);
            t.color = Accent;
            t.fontStyle = FontStyle.Bold;
            UiKit.Stretch(t.rectTransform);
            t.text = text;
        }

        private void AddSlider(string label, float min, float max, float step, string format, Func<float> get, Action<float> set)
        {
            var rt = NewRowRect();
            var l = UiKit.Text("Label", rt, 20, TextAnchor.MiddleLeft, outline: false);
            UiKit.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(210f, RowHeight));

            var slider = UiKit.Slider("Slider", rt, new Vector2(200f, 30f), Accent);
            UiKit.Place((RectTransform)slider.transform, new Vector2(0f, 0.5f), new Vector2(222f, 0f), new Vector2(200f, 30f));
            slider.minValue = min;
            slider.maxValue = max;

            var value = UiKit.Text("Value", rt, 20, TextAnchor.MiddleRight, outline: false);
            value.fontStyle = FontStyle.Bold;
            UiKit.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(80f, RowHeight));
            l.text = label;

            var row = new SliderRow { slider = slider, value = value, get = get, format = format };
            slider.onValueChanged.AddListener(v =>
            {
                v = step > 0f ? Mathf.Round(v / step) * step : v;
                set(v);
                row.Refresh();
                MarkDirty();
            });
            _tabRows[^1].Add(row);
        }

        private void AddToggle(string label, Func<bool> get, Action<bool> set, bool persist)
        {
            var rt = NewRowRect();
            var button = UiKit.Button("Toggle", rt, "", 20, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(button.GetComponent<RectTransform>());

            var box = UiKit.Image("Box", rt, Color.white);
            UiKit.Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(28f, 28f));
            var check = UiKit.Text("Check", box.rectTransform, 22, TextAnchor.MiddleCenter, outline: false);
            check.color = new Color(0.1f, 0.08f, 0f);
            check.fontStyle = FontStyle.Bold;
            UiKit.Stretch(check.rectTransform);

            var l = UiKit.Text("Label", rt, 20, TextAnchor.MiddleLeft, outline: false);
            UiKit.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(Width - 100f, RowHeight));
            l.text = label;

            var row = new ToggleRow { box = box, check = check, get = get };
            button.onClick.AddListener(() =>
            {
                set(!get());
                row.Refresh();
                if (persist) MarkDirty();
                else GameTuning.NotifyChanged();
            });
            _tabRows[^1].Add(row);
        }

        private void AddButton(string label, Action onClick)
        {
            var rt = NewRowRect(48f);
            var b = UiKit.Button("Button", rt, label, 20, new Color(0.22f, 0.26f, 0.34f));
            UiKit.Place(b.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(Width - 40f, 40f));
            b.onClick.AddListener(() => onClick());
        }

        // ───────────── 동작 ─────────────

        private void SelectProfile(int index)
        {
            _profile = index;
            for (int i = 0; i < _profileButtons.Count; i++)
            {
                _profileButtons[i].color = i == index ? TabOn : TabOff;
                _profileButtons[i].GetComponentInChildren<Text>().color = i == index ? new Color(0.1f, 0.08f, 0f) : Color.white;
            }
            RefreshAll();
        }

        private void Preview()
        {
            var p = SelectedProfile;
            if (HitFeel.Instance != null) HitFeel.Instance.Play(p);
            bool heavy = SelectedIsHeavy;
            Sfx.Play(_profile switch
            {
                1 => SfxId.HeavyParry,
                2 => SfxId.Hurt,
                3 => SfxId.EnemyHit,
                _ => SfxId.Parry,
            });
            if (_defense == null) return;

            {
                Vector2 pos = _defense.transform.position + Vector3.right * 0.8f;
                Fx.Flash(pos, heavy ? 1.2f : 0.7f, heavy ? 3.2f : 1.8f, Color.white, heavy ? 0.16f : 0.1f);
                Fx.Sparks(pos, heavy ? new Color(1f, 0.85f, 0.3f) : Color.white, heavy ? 22 : 12, heavy ? 12f : 8f);
            }
        }

        private void OnResetClicked()
        {
            if (_resetArmedUntil < 0f)
            {
                _resetArmedUntil = Time.unscaledTime + 2.5f;
                _resetLabel.text = "한 번 더 누르면 초기화";
                return;
            }
            _resetArmedUntil = -1f;
            _resetLabel.text = "기본값으로";
            GameTuning.ResetToDefaults();
            UpdateStatus("기본값으로 되돌림 · 저장됨");
        }

        private void OnTiming(DefenseTiming timing)
        {
            string color = timing.Kind switch
            {
                TimingKind.InWindow => "#8CF2FF",
                TimingKind.Buffered => "#FFD966",
                _ => "#FF8C8C",
            };
            _timings.Enqueue($"<color={color}>{timing}</color>");
            while (_timings.Count > 7) _timings.Dequeue();
            _timingLog.text = string.Join("\n", _timings.ToArray());
        }

        private void MarkDirty()
        {
            _dirtySince = Time.unscaledTime;
            UpdateStatus("저장 대기…");
        }

        private void UpdateStatus(string state)
        {
            string file = GameTuning.FileIOEnabled ? "Tuning/tuning.json" : "파일 저장 꺼짐 (테스트)";
            _status.text = string.IsNullOrEmpty(state) ? file : $"{file}  ·  {state}";
        }

        private void RefreshAll()
        {
            if (_tabRows.Count == 0) return;
            foreach (var row in _tabRows[_tab]) row.Refresh();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(null);
        }
    }
}
