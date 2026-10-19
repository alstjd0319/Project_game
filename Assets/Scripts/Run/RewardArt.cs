using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 보상 관련 그림 (1016 에셋, <c>Tools/gwimun_sprites/cut_reward.py</c>): 보상 상자 열기 · 재화(엽전·영혼석·혼불) 둥실 그림과 줍기 반짝임 · HUD 재화 아이콘.
    /// 빌더(CreateRewardArt)가 만들어 <see cref="GameAssets.Reward"/>로 읽는다. 없으면 예전 모양(빛구슬 제단·글자만).
    /// </summary>
    public class RewardArt : ScriptableObject
    {
        [Tooltip("보상 상자 · 열기 (6장, 0 = 닫힘, 마지막 = 열린 채, 발끝 피벗)")] public SpriteClip chest = new();
        [Tooltip("엽전 · 회전 (반복)")] public SpriteClip coinSpin = new();
        [Tooltip("영혼석 · 둥실 (반복)")] public SpriteClip soulFloat = new();
        [Tooltip("경험치 혼불 (반복)")] public SpriteClip expOrb = new();
        [Tooltip("줍기 반짝임 · 엽전 / 영혼석 / 경험치 (한 번)")] public SpriteClip pickupCoin = new();
        public SpriteClip pickupSoul = new();
        public SpriteClip pickupExp = new();
        [Tooltip("HUD 재화 아이콘")] public Sprite iconCoin;
        public Sprite iconSoul;
        public Sprite iconExp;
        [Header("상품 그림 (cut_offer_icons.py)")]
        [Tooltip("상품 아이콘 이름 (RoomOffer.IconId: 아이템 Id · heal · gauge)")] public string[] offerIds = new string[0];
        public Sprite[] offerIcons = new Sprite[0];
        [Tooltip("상점 진열대 (천 덮인 나무 상, 아래 가운데 피벗)")] public Sprite shopTable;

        /// <summary>상품 아이콘 (영혼석은 재화 아이콘, 경험치 두루마리는 혼불). 없으면 null.</summary>
        public Sprite OfferIcon(string id)
        {
            if (id == "soul") return iconSoul;
            if (id == "scroll") return iconExp;
            for (int i = 0; i < offerIds.Length && i < offerIcons.Length; i++)
                if (offerIds[i] == id) return offerIcons[i];
            return null;
        }
    }
}
