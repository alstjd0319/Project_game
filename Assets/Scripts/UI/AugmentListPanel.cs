using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 획득한 증강 목록 창. 평소엔 접혀 있고 C로 켜고 끈다 (게임은 멈추지 않음).
    /// 같은 증강은 한 줄로 묶어 중첩 수를 보여준다.
    /// </summary>
    public class AugmentListPanel
    {
        public static AugmentListPanel Instance { get; private set; }

        private const float Width = 480f;
        private const float HeaderHeight = 92f;
        private const float RowHeight = 62f; // 초기값 — 실제 높이는 설명 줄 수에 따라 늘어남
        private const float RowGap = 6f;
        private const float Padding = 16f;
        /// <summary>
        /// 목록 영역 최대 높이 — 이보다 길면 스크롤. 창 아래끝이 바닥에 선 적의 머리 위(1080p 기준 약 710px)에서 멈추는 값.
        /// 판정 대상(적·공격)이 UI에 가리지 않게 하기 위함.
        /// </summary>
        private const float MaxListHeight = 330f;

        private static readonly Color PanelColor = new(0.06f, 0.07f, 0.1f, 0.88f);
        private static readonly Color Gold = new(1f, 0.8f, 0.25f);

        private readonly GameObject _root;
        private readonly RectTransform _rootRect;
        private readonly RectTransform _viewport;
        private readonly RectTransform _list;
        private readonly ScrollRect _scroll;
        private readonly Text _title;
        private readonly Text _summary;
        private readonly Text _empty;
        private readonly List<GameObject> _rows = new();

        private AugmentManager _manager;

        public bool IsVisible => _root.activeSelf;
        public int RowCount => _rows.Count;
        public bool IsScrollable { get; private set; }
        /// <summary>창 전체 높이 (캔버스 단위) — 화면을 넘치지 않는지 테스트용.</summary>
        public float PanelHeight => _rootRect.sizeDelta.y;

        public AugmentListPanel(RectTransform canvasRoot)
        {
            Instance = this;

            var bg = UiKit.Image("AugmentListPanel", canvasRoot, PanelColor);
            _root = bg.gameObject;
            _rootRect = bg.rectTransform;
            // 좌상단 상태 패널 바로 아래 (우측 튜닝 패널과 겹치지 않게)
            UiKit.Place(_rootRect, new Vector2(0f, 1f), new Vector2(24f, -272f), new Vector2(Width, 200f));

            _title = UiKit.Text("Title", _rootRect, 28, TextAnchor.MiddleLeft);
            _title.fontStyle = FontStyle.Bold;
            _title.color = Gold;
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -12f), new Vector2(Width - Padding * 2f, 38f));

            _summary = UiKit.Text("Summary", _rootRect, 18, TextAnchor.MiddleLeft, outline: false);
            _summary.color = new Color(1f, 1f, 1f, 0.6f);
            UiKit.Place(_summary.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -52f), new Vector2(Width - Padding * 2f, 26f));

            // 목록은 높이 상한이 있는 뷰포트 안에서 휠로 스크롤 (증강이 많아도 화면·적을 덮지 않게)
            var viewportImage = UiKit.Image("Viewport", _rootRect, new Color(0f, 0f, 0f, 0f));
            viewportImage.raycastTarget = true; // 휠 입력을 받기 위해
            _viewport = viewportImage.rectTransform;
            UiKit.Place(_viewport, new Vector2(0f, 1f), new Vector2(Padding, -HeaderHeight), new Vector2(Width - Padding * 2f, 0f));
            _viewport.gameObject.AddComponent<RectMask2D>();

            _list = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
            _list.SetParent(_viewport, false);
            UiKit.Place(_list, new Vector2(0f, 1f), Vector2.zero, new Vector2(Width - Padding * 2f, 0f));

            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _list;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false;
            _scroll.scrollSensitivity = 40f;

            _empty = UiKit.Text("Empty", _rootRect, 20, TextAnchor.UpperLeft, outline: false);
            _empty.color = new Color(1f, 1f, 1f, 0.45f);
            _empty.text = "아직 없음 — 몬스터를 잡아 레벨업하면 여기 쌓인다.";
            UiKit.Place(_empty.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -HeaderHeight - 4f), new Vector2(Width - Padding * 2f, 30f));

            _root.SetActive(false);
        }

        public void Bind(AugmentManager manager)
        {
            _manager = manager;
            if (_manager == null) return;
            _manager.AugmentAcquired += _ => { if (IsVisible) Rebuild(); };
            _manager.AugmentsCleared += () => { if (IsVisible) Rebuild(); };
        }

        public void SetVisible(bool visible)
        {
            _root.SetActive(visible);
            if (!visible) return;
            Rebuild();
            _scroll.verticalNormalizedPosition = 1f; // 열 때는 맨 위부터
        }

        /// <summary>Hud.Update에서 호출.</summary>
        public void Tick()
        {
            if (Input.GetKeyDown(Controls.AugmentList)) SetVisible(!IsVisible);

            // 게임 뷰 크기(= 캔버스 배율)가 바뀌면 글자 폭이 달라지므로 다시 배치
            if (IsVisible && !Mathf.Approximately(CanvasScale, _builtForScale)) Rebuild();
        }

        private float _builtForScale;

        private float CanvasScale
        {
            get
            {
                var canvas = _rootRect.GetComponentInParent<Canvas>();
                return canvas != null ? canvas.scaleFactor : 1f;
            }
        }

        private void Rebuild()
        {
            foreach (var row in _rows) Object.Destroy(row);
            _rows.Clear();

            var acquired = _manager != null ? _manager.Acquired : (IReadOnlyList<AugmentDefinition>)new List<AugmentDefinition>();

            // 획득 순서를 유지하면서 같은 증강끼리 묶기
            var order = new List<AugmentDefinition>();
            var stacks = new Dictionary<AugmentDefinition, int>();
            int common = 0, player = 0;
            foreach (var def in acquired)
            {
                if (!stacks.ContainsKey(def))
                {
                    stacks[def] = 0;
                    order.Add(def);
                }
                stacks[def]++;
                switch (def.Owner)
                {
                    case AugmentOwner.Player: player++; break;
                    default: common++; break;
                }
            }

            _title.text = $"획득한 증강 ({acquired.Count})  <size=18><color=#999999>C로 닫기</color></size>";
            _summary.text = $"공용 {common}  ·  <color=#F26152>플레이어 {player}</color>";
            _empty.enabled = order.Count == 0;

            float y = 0f;
            foreach (var def in order)
            {
                var row = CreateRow(def, stacks[def], y, out float rowHeight);
                _rows.Add(row);
                y -= rowHeight + RowGap;
            }

            float listHeight = order.Count == 0 ? 36f : -y - RowGap;
            float viewHeight = Mathf.Min(listHeight, MaxListHeight);
            _list.sizeDelta = new Vector2(Width - Padding * 2f, listHeight);
            _viewport.sizeDelta = new Vector2(Width - Padding * 2f, viewHeight);
            _rootRect.sizeDelta = new Vector2(Width, HeaderHeight + viewHeight + Padding);
            IsScrollable = listHeight > MaxListHeight;
            if (IsScrollable) _summary.text += "   <color=#8CF2FF>· 휠로 스크롤</color>";
            _builtForScale = CanvasScale;
        }

        /// <summary>
        /// 한 줄 = [대상] 이름 ×N / 설명(여러 줄 가능) / 분류·구현 여부.
        /// 설명 높이는 실제 렌더링 높이(preferredHeight)로 잡는다 — 게임 뷰가 작으면 같은 글도 더 넓게 그려져 줄바꿈이 생기기 때문.
        /// </summary>
        private GameObject CreateRow(AugmentDefinition def, int stack, float y, out float rowHeight)
        {
            const float innerLeft = 16f;
            float innerWidth = Width - Padding * 2f - innerLeft - 10f;
            Color ownerColor = OwnerColor(def.Owner);

            var bg = UiKit.Image($"Row_{def.Id}", _list, Color.Lerp(ownerColor, new Color(0.06f, 0.06f, 0.09f), 0.8f));
            UiKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(Width - Padding * 2f, RowHeight));
            var row = bg.rectTransform;

            // 1줄: 대상 태그 + 이름 + 중첩
            var tag = UiKit.Text("Owner", row, 16, TextAnchor.MiddleLeft, outline: false);
            tag.color = ownerColor;
            tag.fontStyle = FontStyle.Bold;
            tag.text = def.OwnerLabel;
            UiKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(innerLeft, -6f), new Vector2(44f, 26f));

            var name = UiKit.Text("Name", row, 22, TextAnchor.MiddleLeft);
            name.fontStyle = FontStyle.Bold;
            string stackText = def.MaxStacks > 1 ? $"  <color=#FFCC40>×{stack}</color><size=15><color=#999999> / {def.MaxStacks}</color></size>" : "";
            name.text = def.Name + stackText;
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(innerLeft + 44f, -6f), new Vector2(innerWidth - 44f, 26f));

            // 2줄~: 설명 (넘치면 줄바꿈, 칸이 같이 늘어남)
            float cursor = -36f;
            var desc = UiKit.Text("Desc", row, 18, TextAnchor.UpperLeft, outline: false);
            desc.color = new Color(1f, 1f, 1f, 0.82f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.text = def.Description;
            UiKit.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(innerLeft, cursor), new Vector2(innerWidth, 24f));
            float descHeight = Mathf.Max(24f, Mathf.Ceil(desc.preferredHeight));
            desc.rectTransform.sizeDelta = new Vector2(innerWidth, descHeight);
            cursor -= descHeight + 2f;

            // 마지막 줄: 분류 · 구현 여부 (이름 옆에 두면 긴 이름과 가로로 부딪혀서 아래로 뺌)
            var category = UiKit.Text("Category", row, 15, TextAnchor.UpperLeft, outline: false);
            category.color = new Color(1f, 1f, 1f, 0.5f);
            category.text = def.IsImplemented ? def.CategoryLabel : $"{def.CategoryLabel} · <color=#FF8C8C>효과 미구현</color>";
            UiKit.Place(category.rectTransform, new Vector2(0f, 1f), new Vector2(innerLeft, cursor), new Vector2(innerWidth, 20f));
            cursor -= 20f + 8f;

            rowHeight = -cursor;
            row.sizeDelta = new Vector2(Width - Padding * 2f, rowHeight);

            // 왼쪽 색 띠 = 대상 캐릭터 (칸 높이 전체)
            var stripe = UiKit.Image("Stripe", row, ownerColor);
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(6f, 0f);
            stripe.rectTransform.anchoredPosition = Vector2.zero;

            return bg.gameObject;
        }

        private static Color OwnerColor(AugmentOwner owner) => owner switch
        {
            AugmentOwner.Player => new Color(0.95f, 0.38f, 0.32f),
            _ => new Color(0.6f, 0.62f, 0.7f),
        };
    }
}
