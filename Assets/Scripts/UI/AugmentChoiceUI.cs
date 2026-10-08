using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 레벨업 증강 3택 화면. ←/→ 선택, Enter 결정 (기획서 4.5).
    /// 게임이 멈춘 상태에서 동작하므로 전부 실시간 기준.
    /// </summary>
    public class AugmentChoiceUI
    {
        private static readonly Color Gold = new(1f, 0.8f, 0.25f);
        private static readonly Color Cyan = new(0.45f, 0.9f, 1f);

        private const float CardWidth = 420f;
        private const float CardHeight = 500f;
        private const float CardSpacing = 48f;
        private const float InputLockTime = 0.3f; // 전투 중 연타하던 키로 잘못 고르는 것 방지

        private class Card
        {
            public RectTransform root;
            public Image frame;
            public Image bg;
            public Image ownerTag;
            public Text ownerText;
            public Text categoryText;
            public Text nameText;
            public Text descText;
            public Text noteText;
        }

        private readonly GameObject _root;
        private readonly RectTransform _cardArea;
        private readonly Text _title;
        private readonly Text _subtitle;
        private readonly List<Card> _cards = new();

        private AugmentManager _manager;
        private PlayerLevel _level;
        private IReadOnlyList<AugmentDefinition> _options;
        private int _selected;
        private float _openTime;
        private float _t;

        public int Selected => _selected;

        public AugmentChoiceUI(RectTransform canvasRoot)
        {
            var dim = UiKit.Image("AugmentChoice", canvasRoot, new Color(0.02f, 0.02f, 0.05f, 0.78f));
            UiKit.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            _title = UiKit.Text("Title", dim.rectTransform, 72, TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;
            _title.color = Cyan;
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1400f, 90f));

            _subtitle = UiKit.Text("Subtitle", dim.rectTransform, 28, TextAnchor.MiddleCenter);
            _subtitle.color = new Color(1f, 1f, 1f, 0.75f);
            UiKit.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(1400f, 40f));

            _cardArea = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
            _cardArea.SetParent(dim.rectTransform, false);
            UiKit.Place(_cardArea, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(0f, CardHeight));

            // 카드 바로 아래 (하단 스킬바와 겹치지 않게)
            var hint = UiKit.Text("Hint", dim.rectTransform, 26, TextAnchor.MiddleCenter);
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            hint.text = "←/→ 선택     Enter 결정";
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f - CardHeight * 0.5f - 50f), new Vector2(800f, 40f));

            for (int i = 0; i < 3; i++) _cards.Add(CreateCard(_cardArea));
            _root.SetActive(false);
        }

        public void Bind(AugmentManager manager, PlayerLevel level)
        {
            _manager = manager;
            _level = level;
            if (_manager == null) return;
            _manager.ChoiceOpened += Open;
            _manager.ChoiceClosed += () => _root.SetActive(false);
        }

        private void Open(IReadOnlyList<AugmentDefinition> options)
        {
            _options = options;
            _selected = 0;
            _openTime = Time.unscaledTime;
            _t = 0f;
            _root.SetActive(true);

            int lv = _level != null ? _level.Level : 0;
            _title.text = "LEVEL UP!";
            string remaining = _manager.PendingChoices > 1 ? $"   (남은 선택 {_manager.PendingChoices})" : "";
            _subtitle.text = $"Lv.{lv} — 증강을 하나 고르세요{remaining}";

            float total = options.Count * CardWidth + (options.Count - 1) * CardSpacing;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool used = i < options.Count;
                card.root.gameObject.SetActive(used);
                if (!used) continue;

                card.root.anchoredPosition = new Vector2(-total * 0.5f + CardWidth * 0.5f + i * (CardWidth + CardSpacing), 0f);
                Fill(card, options[i]);
            }
        }

        private void Fill(Card card, AugmentDefinition def)
        {
            Color ownerColor = def.Owner switch
            {
                AugmentOwner.Player => new Color(0.95f, 0.38f, 0.32f),
                _ => new Color(0.6f, 0.62f, 0.7f),
            };
            card.ownerTag.color = ownerColor;
            card.ownerText.text = def.OwnerLabel;
            card.categoryText.text = def.CategoryLabel;
            card.nameText.text = def.Name;
            card.descText.text = def.Description;

            int stacks = _manager.StacksOf(def);
            string stackInfo = def.MaxStacks > 1 ? $"중첩 {stacks} → {stacks + 1} / {def.MaxStacks}" : "";
            string implInfo = def.IsImplemented ? "" : "※ 효과 미구현 (껍데기)";
            card.noteText.text = string.IsNullOrEmpty(stackInfo) ? implInfo : $"{stackInfo}\n{implInfo}";
            card.bg.color = Color.Lerp(ownerColor, new Color(0.06f, 0.06f, 0.09f), 0.82f);
        }

        /// <summary>Hud.Update에서 매 프레임 호출 (실시간 dt).</summary>
        public void Tick(float dt)
        {
            if (!_root.activeSelf || _options == null || _manager == null || !_manager.IsChoosing) return;
            _t += dt;

            if (Time.unscaledTime - _openTime >= InputLockTime)
            {
                int count = _options.Count;
                if (Input.GetKeyDown(Controls.Left) && _selected > 0)
                {
                    _selected--;
                    Sfx.Play(SfxId.UiMove);
                }
                if (Input.GetKeyDown(Controls.Right) && _selected < count - 1)
                {
                    _selected++;
                    Sfx.Play(SfxId.UiMove);
                }
                if (Input.GetKeyDown(Controls.Confirm) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ConfirmSelected();
                    return;
                }
            }

            Animate();
        }

        /// <summary>현재 선택을 확정 (테스트에서도 사용).</summary>
        public void ConfirmSelected()
        {
            if (_manager == null || !_manager.IsChoosing) return;
            var def = _options[_selected];
            Sfx.Play(SfxId.UiConfirm);
            if (_manager.Choose(_selected))
                Hud.Popup($"증강 획득: {def.Name}", Gold, false);
        }

        public void SetSelected(int index)
        {
            if (_options == null) return;
            _selected = Mathf.Clamp(index, 0, _options.Count - 1);
            Animate();
        }

        private void Animate()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                var card = _cards[i];
                bool selected = i == _selected;

                // 등장: 카드가 차례로 아래에서 올라온다
                float appear = Mathf.Clamp01((_t - i * 0.06f) / 0.22f);
                float ease = 1f - (1f - appear) * (1f - appear);
                float lift = selected ? 18f : 0f;
                var p = card.root.anchoredPosition;
                card.root.anchoredPosition = new Vector2(p.x, Mathf.Lerp(-80f, lift, ease));

                float target = selected ? 1.06f : 0.96f;
                float s = Mathf.Lerp(card.root.localScale.x, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
                card.root.localScale = new Vector3(s, s, 1f);

                card.frame.color = selected ? Gold : new Color(0.35f, 0.35f, 0.4f);
                // 카드 판은 등장 후 불투명 (뒤 게임 화면이 비치지 않게), 선택 안 된 카드는 글자만 흐리게
                SetAlpha(card, ease, ease * (selected ? 1f : 0.55f));
            }
        }

        private static void SetAlpha(Card card, float panelAlpha, float textAlpha)
        {
            foreach (var g in card.root.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                c.a = g is Text ? textAlpha : panelAlpha;
                g.color = c;
            }
        }

        private static Card CreateCard(RectTransform parent)
        {
            var card = new Card();
            card.frame = UiKit.Image("Card", parent, Color.white);
            card.root = card.frame.rectTransform;
            card.root.anchorMin = card.root.anchorMax = new Vector2(0.5f, 0.5f);
            card.root.pivot = new Vector2(0.5f, 0.5f);
            card.root.sizeDelta = new Vector2(CardWidth, CardHeight);

            card.bg = UiKit.Image("Bg", card.root, Color.black);
            UiKit.Stretch(card.bg.rectTransform, 5f);
            var inner = card.bg.rectTransform;

            card.ownerTag = UiKit.Image("OwnerTag", inner, Color.gray);
            UiKit.Place(card.ownerTag.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(90f, 38f));
            card.ownerText = UiKit.Text("Owner", card.ownerTag.rectTransform, 24, TextAnchor.MiddleCenter, outline: false);
            card.ownerText.fontStyle = FontStyle.Bold;
            card.ownerText.color = new Color(0.08f, 0.08f, 0.1f);
            UiKit.Stretch(card.ownerText.rectTransform);

            card.categoryText = UiKit.Text("Category", inner, 22, TextAnchor.MiddleRight, outline: false);
            card.categoryText.color = new Color(1f, 1f, 1f, 0.6f);
            UiKit.Place(card.categoryText.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(240f, 38f));

            card.nameText = UiKit.Text("Name", inner, 44, TextAnchor.MiddleCenter);
            card.nameText.fontStyle = FontStyle.Bold;
            UiKit.Place(card.nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(380f, 60f));

            card.descText = UiKit.Text("Desc", inner, 27, TextAnchor.UpperCenter, outline: false);
            card.descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.descText.lineSpacing = 1.15f;
            card.descText.color = new Color(1f, 1f, 1f, 0.9f);
            UiKit.Place(card.descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(350f, 200f));

            card.noteText = UiKit.Text("Note", inner, 21, TextAnchor.LowerCenter, outline: false);
            card.noteText.color = new Color(0.7f, 0.7f, 0.75f);
            UiKit.Place(card.noteText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(380f, 60f));

            return card;
        }
    }
}
