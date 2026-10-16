using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 사당 NPC(만신)에게 말을 걸면 열리는 스킬 트리 창. 체력 · 공격력 · 자원 세 줄에 노드가 3개씩 이어져 있고,
    /// 레벨업으로 얻은 포인트로 노드를 익혀 스탯을 올린다 (<see cref="SkillTreeState"/>). 열려 있는 동안 게임은 멈춘다(GameManager.SetMenuOpen).
    /// 조작: 마우스 클릭(선택) · ←/→/↑/↓ 선택 이동 · Enter/Space 익히기 · E/ESC 닫기.
    /// 그림은 GameAssets.Ui(귀문 UI 시트)의 두루마리·노드 틀·이음 줄을 쓰고, 없으면 색 네모로 그린다.
    /// </summary>
    public class SkillTreePanel : MonoBehaviour
    {
        private const int Rows = 4, Cols = 3;
        private const float PanelW = 1150f, PanelH = 721f;

        private static SkillTreePanel _instance;

        public static bool IsOpen => _instance != null && _instance._canvas.enabled;

        /// <summary>선택한 노드 번호 (<see cref="SkillTreeState.Nodes"/> 기준).</summary>
        public static int SelectedIndex => _instance != null ? _instance._selected : -1;

        private static readonly Color Ink = new(0.22f, 0.14f, 0.08f);
        private static readonly Color InkSoft = new(0.4f, 0.3f, 0.2f);

        private class NodeView
        {
            public Image Frame, Icon, Cursor;
            public Text Rank;
            public Image LineToNext;
        }

        private Canvas _canvas;
        private Text _title, _speech, _points, _infoName, _infoDesc, _buyLabel;
        private Image _buyImage;
        private Button _buy;
        private readonly NodeView[] _views = new NodeView[Rows * Cols];
        private int _selected;
        private bool _openedThisFrame;

        public static SkillTreePanel Open(string npcName)
        {
            if (_instance == null)
            {
                var go = new GameObject("SkillTreePanel");
                _instance = go.AddComponent<SkillTreePanel>();
            }
            _instance.Show(npcName);
            return _instance;
        }

        public static void Close()
        {
            if (_instance != null) _instance.Hide();
        }

        private void Awake()
        {
            BuildCanvas();
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private static int Level()
        {
            var level = FindAnyObjectByType<PlayerLevel>();
            return level != null ? level.Level : 1;
        }

        private void Show(string npcName)
        {
            _title.text = $"스킬 트리  <size=26>— {npcName}</size>";
            _speech.text = "\"레벨을 올려 얻은 기운으로 몸을 단련하거라.\"";
            _selected = 0;
            Refresh();
            _canvas.enabled = true;
            _openedThisFrame = true;
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(true);
        }

        private void Hide()
        {
            if (!_canvas.enabled) return;
            _canvas.enabled = false;
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(false);
        }

        private void Update()
        {
            if (!_canvas.enabled) return;
            if (_openedThisFrame)
            {
                _openedThisFrame = false; // 열자마자 같은 입력으로 닫히지 않게
                return;
            }
            if (Input.GetKeyDown(Controls.Interact) || Input.GetKeyDown(Controls.Pause)) { Hide(); return; }

            int row = _selected / Cols, col = _selected % Cols;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Select(row, Mathf.Max(0, col - 1));
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Select(row, Mathf.Min(Cols - 1, col + 1));
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Select(Mathf.Max(0, row - 1), col);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Select(Mathf.Min(Rows - 1, row + 1), col);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) TryBuySelected();
        }

        private void Select(int row, int col)
        {
            int index = SkillTreeState.IndexOf((SkillBranch)row, col);
            if (index < 0 || index == _selected) return;
            _selected = index;
            Sfx.Play(SfxId.UiMove, 0.5f);
            Refresh();
        }

        /// <summary>선택한 노드를 익힌다 (포인트가 모자라거나 아직 잠겼으면 소리만).</summary>
        public bool TryBuySelected()
        {
            if (!SkillTreeState.TryBuy(_selected, Level()))
            {
                Sfx.Play(SfxId.UiMove, 0.3f);
                return false;
            }
            var party = FindAnyObjectByType<PlayerParty>();
            if (party != null) party.SetMaxHpBonus(SkillTreeState.MaxHpBonus, true);
            Sfx.Play(SfxId.UiConfirm, 0.8f);
            Refresh();
            return true;
        }

        private void Refresh()
        {
            int level = Level();
            _points.text = SkillTreeState.PointsUnlimited ? "포인트  <b>무제한</b>  <size=22>(설정)</size>"
                : $"남은 포인트  <b>{SkillTreeState.Available(level)}</b>  <size=22>(레벨 {level})</size>";
            var art = GameAssets.Ui;
            for (int i = 0; i < SkillTreeState.Nodes.Length; i++)
            {
                var node = SkillTreeState.Nodes[i];
                var v = _views[i];
                bool unlocked = SkillTreeState.Unlocked(i), maxed = SkillTreeState.Maxed(i);
                int rank = SkillTreeState.Rank(i);
                Sprite frame = art == null ? null : !unlocked ? art.nodeLocked : maxed ? art.nodeOwned : art.nodeOpen;
                v.Frame.sprite = frame;
                v.Frame.color = art != null ? Color.white : !unlocked ? new Color(0.35f, 0.35f, 0.38f) : maxed ? new Color(0.9f, 0.75f, 0.3f) : new Color(0.2f, 0.55f, 0.45f);
                v.Icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                v.Rank.text = $"{rank}/{node.MaxRank}";
                v.Rank.color = unlocked ? Ink : InkSoft;
                v.Cursor.enabled = i == _selected;
                if (v.LineToNext != null)
                {
                    bool lit = rank >= 1; // 앞 노드를 익히면 이음 줄이 빛난다
                    v.LineToNext.sprite = art == null ? null : lit ? art.lineGlow : art.lineDim;
                    v.LineToNext.color = art != null ? Color.white : lit ? new Color(1f, 0.8f, 0.3f) : new Color(0.4f, 0.4f, 0.35f);
                }
            }

            var sel = SkillTreeState.Nodes[_selected];
            int selRank = SkillTreeState.Rank(_selected);
            _infoName.text = $"{sel.Name}  <size=24>({SkillTreeState.BranchNames[(int)sel.Branch]})</size>";
            string next = selRank >= sel.MaxRank ? "<color=#8A5A00>다 익혔다</color>"
                : $"다음 랭크  {sel.Effect(selRank + 1)}  ·  비용 {sel.Cost}P";
            string lockText = SkillTreeState.Unlocked(_selected) ? "" : "\n<color=#8A2A2A>앞 노드를 먼저 익혀야 한다</color>";
            _infoDesc.text = $"지금  {sel.Effect(selRank)}\n{next}{lockText}";
            bool can = SkillTreeState.CanBuy(_selected, level);
            _buy.interactable = can;
            _buyImage.color = can ? new Color(0.2f, 0.5f, 0.42f) : new Color(0.45f, 0.42f, 0.4f);
            _buyLabel.text = selRank >= sel.MaxRank ? "완료" : "익히기  (Enter)";
        }

        // 왼쪽 위 기준 좌표(px)로 배치: 중심 (cx, cy)
        private static void At(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image Pic(string name, RectTransform parent, Sprite sprite, Color fallback)
        {
            var img = UiKit.Image(name, parent, sprite != null ? Color.white : fallback);
            img.sprite = sprite;
            return img;
        }

        private void BuildCanvas()
        {
            var art = GameAssets.Ui;
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var dim = UiKit.Image("Dim", root, new Color(0.02f, 0.01f, 0.06f, 0.7f));
            UiKit.Stretch(dim.rectTransform);

            // 두루마리 창 (그림이 없으면 어두운 네모 + 보라 테두리)
            var panel = Pic("Panel", root, art != null ? art.skillScroll : null, new Color(0.09f, 0.07f, 0.16f, 0.97f)).rectTransform;
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));
            panel.pivot = new Vector2(0.5f, 0.5f);
            bool plain = art == null || art.skillScroll == null;
            Color ink = plain ? Color.white : Ink;

            _title = UiKit.Text("Title", panel, 40, TextAnchor.MiddleLeft, outline: plain);
            _title.fontStyle = FontStyle.Bold; _title.color = ink;
            At(_title.rectTransform, 530f, 112f, 560f, 56f);
            _speech = UiKit.Text("Speech", panel, 22, TextAnchor.MiddleLeft, outline: plain);
            _speech.color = plain ? new Color(0.85f, 0.82f, 0.95f) : InkSoft;
            At(_speech.rectTransform, 590f, 152f, 700f, 34f);
            _points = UiKit.Text("Points", panel, 30, TextAnchor.MiddleRight, outline: plain);
            _points.color = ink;
            At(_points.rectTransform, 700f, 112f, 420f, 44f);

            // 세 줄 × 노드 3개
            for (int r = 0; r < Rows; r++)
            {
                float cy = 210f + r * 96f;
                var label = UiKit.Text($"Branch{r}", panel, 34, TextAnchor.MiddleCenter, outline: plain);
                label.fontStyle = FontStyle.Bold; label.color = ink;
                label.text = SkillTreeState.BranchNames[r];
                At(label.rectTransform, 200f, cy, 130f, 44f);
                for (int c = 0; c < Cols; c++)
                {
                    int index = SkillTreeState.IndexOf((SkillBranch)r, c);
                    float cx = 340f + c * 200f;
                    var v = new NodeView();
                    _views[index] = v;
                    if (c < Cols - 1)
                    {
                        v.LineToNext = Pic($"Line{index}", panel, art != null ? art.lineDim : null, Color.gray);
                        At(v.LineToNext.rectTransform, cx + 100f, cy, 100f, 14f);
                    }
                    v.Frame = Pic($"Node{index}", panel, null, Color.gray);
                    At(v.Frame.rectTransform, cx, cy, 80f, 71f);
                    v.Frame.raycastTarget = true;
                    int captured = index;
                    var btn = v.Frame.gameObject.AddComponent<Button>();
                    btn.targetGraphic = v.Frame;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() => { _selected = captured; Sfx.Play(SfxId.UiMove, 0.5f); Refresh(); });

                    Sprite iconSprite = art == null ? null : r == 0 ? art.iconHeart : r == 1 ? art.iconStaff : r == 2 ? art.iconTalisman : art.iconLantern;
                    v.Icon = Pic("Icon", v.Frame.rectTransform, iconSprite, new Color(1f, 1f, 1f, 0.6f));
                    v.Icon.preserveAspect = true;
                    v.Icon.rectTransform.anchorMin = new Vector2(0.22f, 0.2f);
                    v.Icon.rectTransform.anchorMax = new Vector2(0.78f, 0.8f);
                    v.Icon.rectTransform.offsetMin = v.Icon.rectTransform.offsetMax = Vector2.zero;

                    v.Cursor = Pic("Cursor", panel, art != null ? art.nodeSelected : null, new Color(1f, 0.85f, 0.3f, 0.35f));
                    At(v.Cursor.rectTransform, cx, cy, 92f, 82f);

                    v.Rank = UiKit.Text("Rank", panel, 20, TextAnchor.MiddleCenter, outline: plain);
                    v.Rank.fontStyle = FontStyle.Bold;
                    At(v.Rank.rectTransform, cx, cy + 47f, 100f, 24f);
                }
            }

            // 선택한 노드 설명 + 익히기 / 닫기
            _infoName = UiKit.Text("InfoName", panel, 28, TextAnchor.MiddleLeft, outline: plain);
            _infoName.fontStyle = FontStyle.Bold; _infoName.color = ink;
            At(_infoName.rectTransform, 585f, 578f, 690f, 34f);
            _infoDesc = UiKit.Text("InfoDesc", panel, 20, TextAnchor.UpperLeft, outline: plain);
            _infoDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            _infoDesc.color = ink;
            At(_infoDesc.rectTransform, 585f, 620f, 690f, 50f);

            _buy = UiKit.Button("Buy", panel, "", 26, new Color(0.2f, 0.5f, 0.42f));
            _buyImage = _buy.GetComponent<Image>();
            _buyLabel = _buy.GetComponentInChildren<Text>();
            At(_buy.GetComponent<RectTransform>(), 905f, 300f, 190f, 52f);
            _buy.onClick.AddListener(() => TryBuySelected());

            var close = UiKit.Button("Close", panel, "닫기  (E / ESC)", 22, new Color(0.34f, 0.26f, 0.55f));
            At(close.GetComponent<RectTransform>(), 905f, 365f, 190f, 44f);
            close.onClick.AddListener(Hide);
        }
    }
}
