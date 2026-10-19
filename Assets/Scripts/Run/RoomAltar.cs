using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 엘리트·보상·상점방의 제단: 상품(<see cref="RoomOffer"/>) 하나가 올라가 있고 E로 얻는다(상점은 엽전을 낸다).
    /// 엽전이 모자라면 못 산다. 얻은 제단은 "받음/품절"로 어두워진다. 판정은 상호작용 트리거 하나뿐.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class RoomAltar : MonoBehaviour
    {
        public const float Width = 2.2f, Height = 2.4f;

        public RoomOffer Offer { get; private set; }
        public bool Taken { get; private set; }
        /// <summary>다른 제단을 얻어서 못 고르게 된 상태 (하나만 고르는 방).</summary>
        public bool Locked { get; private set; }

        /// <summary>얻었을 때 (RoomController가 문을 여는 데 쓴다).</summary>
        public event Action<RoomAltar> TakenEvent;

        private Interactable _interactable;
        private SpriteRenderer _orb, _pedestal;
        private TextMesh _status;
        private float _time;

        public static RoomAltar Create(Vector3 groundPos, RoomOffer offer)
        {
            var go = new GameObject($"Altar_{offer.Kind}");
            go.transform.position = groundPos + new Vector3(0f, Height * 0.5f, 0f);
            go.transform.localScale = new Vector3(Width, Height, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var altar = go.AddComponent<RoomAltar>();
            altar.Offer = offer;
            altar.Build();
            return altar;
        }

        private void Build()
        {
            _interactable = GetComponent<Interactable>();
            Refresh();
            // 받침(바닥 네모)과 위에 뜬 빛구슬 (제단의 색 = 상품 종류)
            _pedestal = MakeSprite("Pedestal", GameAssets.Square, new Vector3(0f, -0.5f + 0.15f / Height, 0f), new Vector2(1.6f / Width, 0.3f / Height), 9, new Color(0.25f, 0.2f, 0.35f, 1f));
            _orb = MakeSprite("Orb", FxSprites.Glow, new Vector3(0f, 0.05f, 0f), new Vector2(2.2f / Width, 2.2f / Height), 10, Offer.Color);
            MakeLabel(Offer.Title, Offer.Color, new Vector3(0f, 1.0f / Height + 0.5f, 0f) , 0.055f, true);
            MakeLabel(Offer.Desc, new Color(1f, 1f, 1f, 0.85f), new Vector3(0f, 0.5f + 0.6f / Height, 0f), 0.035f, false);
            _status = MakeLabel(Offer.Price > 0 ? $"엽전 {Offer.Price}" : "공짜 (하나만)", Offer.Price > 0 ? new Color(1f, 0.85f, 0.35f) : new Color(0.8f, 0.7f, 1f),
                new Vector3(0f, 0.5f + 0.25f / Height, 0f), 0.04f, true);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 localPos, Vector2 localScale, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private TextMesh MakeLabel(string text, Color color, Vector3 localPos, float worldSize, bool bold)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(1f / Width, 1f / Height, 1f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = UiKit.Font;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = UiKit.Font.material;
            mr.sortingOrder = 24;
            tm.text = text;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = worldSize;
            tm.fontStyle = bold ? UiKit.BoldStyle : FontStyle.Normal;
            return tm;
        }

        private void Refresh()
        {
            string verb = Offer.Price > 0 ? $"사기 (엽전 {Offer.Price})" : "고르기";
            _interactable.SetPrompt(Taken ? "이미 얻었다" : Locked ? "다른 것을 골랐다" : $"{Offer.Title} {verb} — {Offer.Desc}");
        }

        private void OnEnable()
        {
            if (_interactable == null) _interactable = GetComponent<Interactable>();
            _interactable.Interacted += OnInteracted;
        }

        private void OnDisable() => _interactable.Interacted -= OnInteracted;

        private void OnInteracted() => TryTake();

        /// <summary>얻는다 (엽전이 모자라거나 이미 얻었거나 잠겼으면 false). 테스트도 이걸 부른다.</summary>
        public bool TryTake()
        {
            if (Taken || Locked) return false;
            if (Offer.Price > RunState.Gold)
            {
                Hud.Popup($"엽전이 모자란다 ({RunState.Gold} / {Offer.Price})", new Color(1f, 0.85f, 0.35f), false);
                return false;
            }
            RunState.SpendGold(Offer.Price);
            Offer.Apply();
            Taken = true;
            Sfx.Play(SfxId.LevelUp, 0.7f);
            Hud.Popup($"{Offer.Title}  {Offer.Desc}", Offer.Color, false);
            Fx.Ring(transform.position, Offer.Color, 3f, 0.5f, 0.1f, 0.4f);
            Dim(Offer.Price > 0 ? "품절" : "받음");
            Refresh();
            TakenEvent?.Invoke(this);
            return true;
        }

        /// <summary>다른 제단을 골라서 못 쓰게 된다.</summary>
        public void Lock()
        {
            if (Taken) return;
            Locked = true;
            Dim("");
            Refresh();
        }

        private void Dim(string status)
        {
            if (_orb != null) _orb.color = new Color(0.4f, 0.4f, 0.45f, 0.25f);
            if (_status != null) _status.text = status;
        }

        private void Update()
        {
            if (_orb == null || Taken || Locked) return;
            _time += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 2.4f + transform.position.x);
            var c = Offer.Color;
            c.a = 0.55f + 0.3f * pulse;
            _orb.color = c;
        }
    }
}
