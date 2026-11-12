using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 웨이브를 깬 뒤 뜨는 강화 3택 창 (<see cref="WaveUpgrades"/>). 열려 있는 동안 게임이 멈춘다 (GameManager.SetMenuOpen).
    /// 조작: 1 / 2 / 3 바로 고르기 · ←/→ 선택 · Enter/Space 확인 · 마우스 올리기·클릭.
    /// </summary>
    public class UpgradeChoicePanel : MonoBehaviour
    {
        private const float CardW = 380f, CardH = 470f, Gap = 40f;

        private static UpgradeChoicePanel _instance;

        public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.enabled;
        public static IReadOnlyList<UpgradeId> Options => _instance != null ? _instance._options : Array.Empty<UpgradeId>();
        public static int Selected => _instance != null ? _instance._selected : -1;

        private class CardView
        {
            public Image Frame, Glow, Icon, Band;
            public Text Category, Name, Level, Desc;
        }

        private Canvas _canvas;
        private Text _title, _hint;
        private readonly CardView[] _cards = new CardView[3];
        private readonly List<UpgradeId> _options = new();
        private Action<UpgradeId> _onChosen;
        private int _selected;
        private bool _openedThisFrame;

        public static void Open(IList<UpgradeId> options, string title, Action<UpgradeId> onChosen)
        {
            if (options == null || options.Count == 0) return;
            if (_instance == null) _instance = new GameObject("UpgradeChoicePanel").AddComponent<UpgradeChoicePanel>();
            _instance.Show(options, title, onChosen);
        }

        /// <summary>index번 카드를 고른다 (테스트·클릭).</summary>
        public static bool Choose(int index) => _instance != null && _instance.Pick(index);

        private void Awake()
        {
            Build();
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Show(IList<UpgradeId> options, string title, Action<UpgradeId> onChosen)
        {
            _options.Clear();
            _options.AddRange(options);
            _onChosen = onChosen;
            _title.text = title;
            _selected = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < _options.Count;
                _cards[i].Frame.gameObject.SetActive(on);
                if (on) Fill(_cards[i], _options[i], i);
            }
            Highlight();
            _canvas.enabled = true;
            _openedThisFrame = true;
            Sfx.Play(SfxId.LevelUp, 0.6f);
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(true);
        }

        private void Fill(CardView c, UpgradeId id, int index)
        {
            var def = WaveUpgrades.Def(id);
            int now = WaveUpgrades.Level(id);
            Color cat = WaveUpgrades.CategoryColors[(int)def.Category];
            c.Band.color = new Color(cat.r, cat.g, cat.b, 0.85f);
            c.Category.text = $"{index + 1}  ·  {WaveUpgrades.CategoryNames[(int)def.Category]}";
            c.Name.text = def.Name;
            c.Name.color = Color.Lerp(cat, Color.white, 0.55f);
            c.Level.text = now == 0 ? "새로 얻음" : $"{WaveUpgrades.LevelText(now)} → {WaveUpgrades.LevelText(now + 1)}";
            c.Desc.text = def.Describe(now + 1);
            var sprite = GameAssets.Ui != null ? GameAssets.Ui.Skill(def.Icon) : null;
            c.Icon.sprite = sprite;
            c.Icon.color = sprite != null ? Color.white : new Color(cat.r, cat.g, cat.b, 0.5f);
            c.Glow.color = new Color(cat.r, cat.g, cat.b, 0f);
        }

        private bool Pick(int index)
        {
            if (!IsOpen || index < 0 || index >= _options.Count) return false;
            var id = _options[index];
            WaveUpgrades.Add(id);
            Sfx.Play(SfxId.UiConfirm, 0.9f);
            _canvas.enabled = false;
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(false);
            var def = WaveUpgrades.Def(id);
            Hud.Popup($"{def.Name} {WaveUpgrades.LevelText(WaveUpgrades.Level(id))}", WaveUpgrades.CategoryColors[(int)def.Category], false);
            var cb = _onChosen;
            _onChosen = null;
            cb?.Invoke(id);
            return true;
        }

        private void Update()
        {
            if (!IsOpen) return;
            // 고를 카드가 숨 쉬듯 빛난다 (시간이 멈춰 있으니 unscaled)
            float pulse = 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
            for (int i = 0; i < _options.Count; i++)
            {
                var g = _cards[i].Glow.color;
                g.a = i == _selected ? pulse : 0f;
                _cards[i].Glow.color = g;
            }
            if (_openedThisFrame)
            {
                _openedThisFrame = false; // 웨이브를 깬 키 입력으로 바로 고르지 않게
                return;
            }
            for (int i = 0; i < _options.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Pick(i); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Move(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Pick(_selected);
        }

        private void Move(int d)
        {
            int next = Mathf.Clamp(_selected + d, 0, _options.Count - 1);
            if (next == _selected) return;
            _selected = next;
            Sfx.Play(SfxId.UiMove, 0.5f);
            Highlight();
        }

        private void Hover(int i)
        {
            if (i == _selected || i >= _options.Count) return;
            _selected = i;
            Highlight();
        }

        private void Highlight()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                bool sel = i == _selected;
                _cards[i].Frame.color = sel ? new Color(0.16f, 0.12f, 0.26f, 0.98f) : new Color(0.08f, 0.06f, 0.13f, 0.95f);
                _cards[i].Frame.rectTransform.localScale = Vector3.one * (sel ? 1.04f : 1f);
            }
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 210;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var dim = UiKit.Image("Dim", root, new Color(0.02f, 0.01f, 0.06f, 0.72f));
            UiKit.Stretch(dim.rectTransform);

            _title = UiKit.Text("Title", root, 46, TextAnchor.MiddleCenter);
            _title.fontStyle = UiKit.BoldStyle;
            _title.color = new Color(0.85f, 0.75f, 1f);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1400f, 70f));
            _title.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            float total = CardW * 3f + Gap * 2f;
            for (int i = 0; i < 3; i++)
            {
                float x = -total * 0.5f + CardW * 0.5f + i * (CardW + Gap);
                _cards[i] = BuildCard(root, i, new Vector2(x, -10f));
            }

            _hint = UiKit.Text("Hint", root, 24, TextAnchor.MiddleCenter);
            _hint.color = new Color(0.75f, 0.72f, 0.85f);
            _hint.text = "1 · 2 · 3 바로 고르기     ←/→ 선택 · Enter 확인";
            UiKit.Place(_hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(1400f, 40f));
            _hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private CardView BuildCard(RectTransform root, int index, Vector2 pos)
        {
            var c = new CardView();
            c.Glow = UiKit.Image($"CardGlow{index}", root, new Color(1f, 1f, 1f, 0f));
            c.Glow.sprite = FxSprites.Glow;
            UiKit.Place(c.Glow.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(CardW * 1.6f, CardH * 1.45f));
            c.Glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            c.Frame = UiKit.Image($"Card{index}", root, new Color(0.08f, 0.06f, 0.13f, 0.95f));
            c.Frame.raycastTarget = true;
            var rt = c.Frame.rectTransform;
            UiKit.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(CardW, CardH));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var outline = c.Frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.45f, 0.95f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
            var button = c.Frame.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            int captured = index;
            button.onClick.AddListener(() => Pick(captured));
            var trigger = c.Frame.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Hover(captured));
            trigger.triggers.Add(enter);

            c.Band = UiKit.Image("Band", rt, Color.white);
            UiKit.Place(c.Band.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CardW, 52f));
            c.Category = UiKit.Text("Category", c.Band.rectTransform, 24, TextAnchor.MiddleCenter, outline: false);
            UiKit.Stretch(c.Category.rectTransform);
            c.Category.color = new Color(0.08f, 0.05f, 0.12f);

            c.Icon = UiKit.Image("Icon", rt, Color.white);
            c.Icon.preserveAspect = true;
            UiKit.Place(c.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(128f, 128f));

            c.Name = UiKit.Text("Name", rt, 38, TextAnchor.MiddleCenter);
            c.Name.fontStyle = UiKit.BoldStyle;
            UiKit.Place(c.Name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(CardW - 20f, 50f));

            c.Level = UiKit.Text("Level", rt, 24, TextAnchor.MiddleCenter);
            c.Level.color = new Color(1f, 0.85f, 0.45f);
            UiKit.Place(c.Level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(CardW - 20f, 34f));

            c.Desc = UiKit.Text("Desc", rt, 24, TextAnchor.UpperCenter);
            c.Desc.color = new Color(0.88f, 0.86f, 0.95f);
            c.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.Desc.lineSpacing = 1.15f;
            UiKit.Place(c.Desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -318f), new Vector2(CardW - 40f, 140f));
            return c;
        }
    }
}
