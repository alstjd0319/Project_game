using UnityEngine;

namespace ParryRL
{
    /// <summary>한 판(런)을 이루는 방의 종류. 문(게이트) 앞에 이 종류가 표시돼서 다음 방을 고른다.</summary>
    public enum RoomType { Normal, Elite, Reward, Shop, Boss }

    public static class RoomInfo
    {
        public static string Label(RoomType type) => type switch
        {
            RoomType.Elite => "엘리트",
            RoomType.Reward => "보상",
            RoomType.Shop => "상점",
            RoomType.Boss => "보스",
            _ => "일반",
        };

        /// <summary>문 앞에 붙는 한 줄 힌트 (무엇이 기다리는지).</summary>
        public static string Hint(RoomType type) => type switch
        {
            RoomType.Elite => "강한 몬스터 · 영혼석 또는 아이템",
            RoomType.Reward => "싸움 없음 · 보상 고르기",
            RoomType.Shop => "싸움 없음 · 엽전으로 구매",
            RoomType.Boss => "불가살이 · 마지막 관문",
            _ => "몬스터 웨이브",
        };

        /// <summary>문·표시 색. 빨강은 적 공격 경고 전용이라 쓰지 않는다 (엘리트 = 귀문 보라).</summary>
        public static Color Color(RoomType type) => type switch
        {
            RoomType.Elite => new Color(0.8f, 0.45f, 1f),
            RoomType.Reward => new Color(1f, 0.85f, 0.35f),
            RoomType.Shop => new Color(0.5f, 1f, 0.6f),
            RoomType.Boss => new Color(1f, 0.55f, 0.2f), // 불가살이의 용암빛 주황 (빨강은 적 공격 경고 전용)
            _ => new Color(0.65f, 0.9f, 1f),
        };

        /// <summary>싸움이 있는 방인가 (보상·상점은 몬스터가 없다).</summary>
        public static bool HasFight(RoomType type) => type == RoomType.Normal || type == RoomType.Elite || type == RoomType.Boss;
    }
}
