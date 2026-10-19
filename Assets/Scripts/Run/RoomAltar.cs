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
        /// <summary>보상 상자 그림 배율 (원본 그대로면 너비 약 1.1유닛이라 제단 글자에 비해 작다).</summary>
        private const float ChestScale = 1.6f;
        /// <summary>상품 그림 높이(유닛).</summary>
        private const float IconSize = 0.85f;

        public RoomOffer Offer { get; private set; }
        public bool Taken { get; private set; }
        /// <summary>다른 제단을 얻어서 못 고르게 된 상태 (하나만 고르는 방).</summary>
        public bool Locked { get; private set; }

        /// <summary>얻었을 때 (RoomController가 문을 여는 데 쓴다).</summary>
        public event Action<RoomAltar> TakenEvent;

        private Interactable _interactable;
        private SpriteRenderer _orb, _pedestal, _chest, _soul, _icon;
        private float _iconBaseY;

        /// <summary>상품 그림 (메달 아이콘, 없으면 null).</summary>
        public SpriteRenderer IconRenderer => _icon;
        private SpriteClip _chestClip;
        private float _chestOpenTime = -1f;

        /// <summary>보상 상자 그림을 쓰는가 (공짜로 고르는 엘리트·보상방 제단).</summary>
        public bool HasChest => _chest != null;
        /// <summary>상자가 열렸는가 (얻은 뒤).</summary>
        public bool ChestOpened => _chestOpenTime >= 0f;
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
            var reward = GameAssets.Reward;
            if (Offer.Price == 0 && reward != null && !reward.chest.IsEmpty)
            {
                // 공짜로 고르는 보상: 보물 상자(1016 에셋) — 고르면 열리며 빛이 터진다. 위에 상품 색 빛이 은은하게 뜬다
                _chestClip = reward.chest;
                _chest = MakeSprite("Chest", _chestClip.frames[0], new Vector3(0f, -0.5f, 0f), new Vector2(ChestScale / Width, ChestScale / Height), 10, Color.white);
                _orb = MakeSprite("Orb", FxSprites.Glow, new Vector3(0f, 0.12f, 0f), new Vector2(1.8f / Width, 1.8f / Height), 9, Offer.Color);
                if (Offer.Kind == OfferKind.Soul && !reward.soulFloat.IsEmpty)
                {
                    _soul = MakeSprite("Soul", reward.soulFloat.frames[0], new Vector3(0f, 0.18f, 0f), new Vector2(0.5f / Width, 0.5f / Height), 11, Color.white);
                    _soul.gameObject.AddComponent<ClipPlayer>().Play(reward.soulFloat);
                }
            }
            else if (reward != null && reward.shopTable != null)
            {
                // 상점: 천 덮인 나무 진열대(1016 상점 소품) 위에 상품
                _pedestal = MakeSprite("Table", reward.shopTable, new Vector3(0f, -0.5f, 0f), new Vector2(1f / Width, 1f / Height), 9, Color.white);
                _orb = MakeSprite("Orb", FxSprites.Glow, new Vector3(0f, (reward.shopTable.bounds.size.y + 0.45f) / Height - 0.5f, 0f), new Vector2(1.6f / Width, 1.6f / Height), 8, Offer.Color);
            }
            else
            {
                // 받침(바닥 네모)과 위에 뜬 빛구슬 (제단의 색 = 상품 종류)
                _pedestal = MakeSprite("Pedestal", GameAssets.Square, new Vector3(0f, -0.5f + 0.15f / Height, 0f), new Vector2(1.6f / Width, 0.3f / Height), 9, new Color(0.25f, 0.2f, 0.35f, 1f));
                _orb = MakeSprite("Orb", FxSprites.Glow, new Vector3(0f, 0.05f, 0f), new Vector2(2.2f / Width, 2.2f / Height), 10, Offer.Color);
            }
            // 상품 그림: 상품마다 어울리는 메달(1017 상태 아이콘·1016 업경 아이콘) — 진열대 위 / 상자 위에 둥실
            var iconSprite = reward != null ? reward.OfferIcon(Offer.IconId) : null;
            if (iconSprite != null && _soul == null)
            {
                float baseY = _chest != null ? 1.55f // 상자(그림 칸 아래쪽 절반이 상자) 바로 위
                    : reward.shopTable != null ? reward.shopTable.bounds.size.y + 0.45f : 1.2f;
                float s = IconSize / iconSprite.bounds.size.y;
                _icon = MakeSprite("Icon", iconSprite, new Vector3(0f, baseY / Height - 0.5f, 0f), new Vector2(s / Width, s / Height), 11, Color.white);
                _iconBaseY = _icon.transform.localPosition.y;
                if (_orb != null) _orb.transform.localPosition = _icon.transform.localPosition;
            }
            MakeLabel(Offer.Title, Offer.Color, new Vector3(0f, 1.0f / Height + 0.5f, 0f) , 0.055f, true);
            MakeLabel(Offer.Desc, new Color(1f, 1f, 1f, 0.85f), new Vector3(0f, 0.5f + 0.6f / Height, 0f), 0.035f, false);
            _status = MakeLabel(Offer.Price > 0 ? $"엽전 {Offer.Price}" : "공짜 (하나만)", Offer.Price > 0 ? new Color(1f, 0.85f, 0.35f) : new Color(0.8f, 0.7f, 1f),
                new Vector3(0f, 0.5f + 0.25f / Height, 0f), 0.04f, true);
            if (Offer.Price > 0 && reward != null && reward.iconCoin != null)
            {
                // 가격 앞 엽전 그림 (글자 폭을 재서 왼쪽에 붙인다)
                var bounds = _status.GetComponent<MeshRenderer>().bounds;
                float left = bounds.size.x > 0.01f ? bounds.min.x - transform.position.x : -0.6f;
                var icon = MakeSprite("CoinIcon", reward.iconCoin, new Vector3((left - 0.25f) / Width, 0.5f + 0.25f / Height, 0f), new Vector2(0.4f / Width, 0.4f / Height), 24, Color.white);
                icon.transform.localScale = new Vector3(0.4f / (reward.iconCoin.bounds.size.x * Width), 0.4f / (reward.iconCoin.bounds.size.x * Height), 1f);
            }
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
            if (_chest != null)
            {
                _chestOpenTime = 0f; // Update가 여는 장면을 재생한다
                if (_soul != null) _soul.enabled = false;
            }
            if (Offer.Kind == OfferKind.Soul) RewardFx.Burst(RewardFx.Kind.Soul, transform.position, Mathf.Min(Offer.Amount, 4));
            if (Offer.Price > 0) RewardFx.Burst(RewardFx.Kind.Coin, transform.position + Vector3.up * 0.3f, 2);
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
            if (_chest != null && !Taken) _chest.color = new Color(0.45f, 0.45f, 0.5f, 0.8f); // 다른 걸 골라 잠긴 상자는 어둡게 닫힌 채
            if (_soul != null && !Taken) _soul.color = new Color(1f, 1f, 1f, 0.3f);
            if (_icon != null) _icon.color = Taken ? new Color(1f, 1f, 1f, 0f) : new Color(0.5f, 0.5f, 0.55f, 0.6f); // 받은 상품은 사라지고, 못 고른 상품은 흐리게
            if (_status != null) _status.text = status;
        }

        private void Update()
        {
            if (_icon != null)
            {
                var ip = _icon.transform.localPosition;
                ip.y = _iconBaseY + Mathf.Sin(Time.time * 2f + transform.position.x) * 0.06f / Height; // 둥실
                _icon.transform.localPosition = ip;
            }
            if (_chest != null && _chestOpenTime >= 0f)
            {
                // 상자 열기: 6장을 한 번 재생하고 마지막(열린 채) 장면에 머문다. 빛이 터지는 장면에 상품 색 빛무리
                _chestOpenTime += Time.deltaTime;
                int n = _chestClip.frames.Length;
                int f = Mathf.Min(n - 1, (int)(_chestOpenTime * _chestClip.fps));
                _chest.sprite = _chestClip.frames[f];
                if (_orb != null)
                {
                    float k = Mathf.Clamp01(_chestOpenTime / (n / _chestClip.fps));
                    var glow = Offer.Color;
                    glow.a = Mathf.Sin(k * Mathf.PI) * 0.9f;
                    _orb.color = glow;
                }
                return;
            }
            if (_orb == null || Taken || Locked) return;
            _time += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 2.4f + transform.position.x);
            var c = Offer.Color;
            c.a = 0.55f + 0.3f * pulse;
            _orb.color = c;
        }
    }
}
