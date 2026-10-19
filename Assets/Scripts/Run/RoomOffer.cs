using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    public enum OfferKind { Item, Soul, Heal, Gauge }

    /// <summary>제단에 놓인 하나의 보상·상품. 엘리트·보상방은 공짜로 하나만 고르고, 상점은 엽전으로 산다.</summary>
    public class RoomOffer
    {
        public OfferKind Kind;
        public RunItem Item;
        public int Amount;

        /// <summary>상품 그림 이름 (<see cref="RewardArt.OfferIcon"/>).</summary>
        public string IconId => Kind switch
        {
            OfferKind.Item => Item != null ? Item.Id : "",
            OfferKind.Soul => "soul",
            OfferKind.Heal => "heal",
            _ => "gauge",
        };
        public int Price;

        public string Title => Kind switch
        {
            OfferKind.Item => Item.Name,
            OfferKind.Soul => $"영혼석 {Amount}개",
            OfferKind.Heal => Amount >= 100 ? "체력 가득 회복" : $"체력 {Amount}% 회복",
            _ => "도력 가득 채우기",
        };

        public string Desc => Kind switch
        {
            OfferKind.Item => Item.Describe + " (이번 판)",
            OfferKind.Soul => "판이 끝나도 남는다",
            OfferKind.Heal => "지금 바로",
            _ => "지금 바로",
        };

        public Color Color => Kind switch
        {
            OfferKind.Soul => new Color(0.75f, 0.6f, 1f),
            OfferKind.Heal => new Color(0.5f, 1f, 0.6f),
            OfferKind.Gauge => new Color(0.45f, 0.9f, 1f),
            _ => new Color(1f, 0.85f, 0.35f),
        };

        /// <summary>얻는다 (돈은 이미 냈다). 효과는 체력·도력 컴포넌트에 바로 적용.</summary>
        public void Apply()
        {
            switch (Kind)
            {
                case OfferKind.Item:
                    RunState.AddItem(Item);
                    var party = Object.FindAnyObjectByType<PlayerParty>();
                    if (party != null) party.SetMaxHpBonus(SkillTreeState.MaxHpBonus, true); // 최대 체력 아이템은 늘어난 만큼 체력도 찬다
                    break;
                case OfferKind.Soul:
                    RunState.AddSoulStones(Amount);
                    break;
                case OfferKind.Heal:
                    var p = Object.FindAnyObjectByType<PlayerParty>();
                    if (p != null) p.Heal(Mathf.CeilToInt(p.MaxHp * Amount / 100f));
                    break;
                case OfferKind.Gauge:
                    var g = Object.FindAnyObjectByType<SkillGauge>();
                    if (g != null) g.Add(g.Max);
                    break;
            }
        }

        // ── 방 종류별 구성 (가안) ──

        public const int EliteSoulStones = 3;

        /// <summary>엘리트방: 영혼석 또는 아이템 (하나만 고른다, 공짜).</summary>
        public static List<RoomOffer> ForElite(System.Random rng = null) => new()
        {
            new RoomOffer { Kind = OfferKind.Soul, Amount = EliteSoulStones },
            new RoomOffer { Kind = OfferKind.Item, Item = RunItemCatalog.Roll(1, rng)[0] },
        };

        /// <summary>보상방: 아이템 · 체력 가득 회복 · 영혼석 중 하나 (공짜).</summary>
        public static List<RoomOffer> ForReward(System.Random rng = null) => new()
        {
            new RoomOffer { Kind = OfferKind.Item, Item = RunItemCatalog.Roll(1, rng)[0] },
            new RoomOffer { Kind = OfferKind.Heal, Amount = 100 },
            new RoomOffer { Kind = OfferKind.Soul, Amount = 2 },
        };

        /// <summary>상점방: 엽전으로 산다 (여러 개 살 수 있다).</summary>
        public static List<RoomOffer> ForShop(System.Random rng = null)
        {
            var items = RunItemCatalog.Roll(2, rng);
            return new List<RoomOffer>
            {
                new RoomOffer { Kind = OfferKind.Item, Item = items[0], Price = 45 },
                new RoomOffer { Kind = OfferKind.Item, Item = items[1], Price = 45 },
                new RoomOffer { Kind = OfferKind.Heal, Amount = 50, Price = 30 },
                new RoomOffer { Kind = OfferKind.Gauge, Price = 20 },
            };
        }

        public static List<RoomOffer> For(RoomType type, System.Random rng = null) => type switch
        {
            RoomType.Elite => ForElite(rng),
            RoomType.Reward => ForReward(rng),
            RoomType.Shop => ForShop(rng),
            _ => new List<RoomOffer>(),
        };
    }
}
