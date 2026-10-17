using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// "여기 아래에 땅이 있나" 검사 — 바닥 구멍 앞에서 걷는 몬스터가 멈추고, 구멍 위에 생기지 않게 하는 데 쓴다.
    /// 땅 = 움직이지 않는(리지드바디 없는) 판정 콜라이더. 몬스터·플레이어·트리거는 치지 않는다.
    /// </summary>
    public static class GroundProbe
    {
        private static readonly Collider2D[] Hits = new Collider2D[8];

        /// <summary>(x, 발 높이) 바로 아래(0.2유닛)에 땅이 있는가.</summary>
        public static bool HasGround(float x, float feetY)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int n = Physics2D.OverlapPoint(new Vector2(x, feetY - 0.2f), filter, Hits);
            for (int i = 0; i < n; i++)
                if (Hits[i] != null && Hits[i].attachedRigidbody == null && Hits[i].GetComponent<PlatformEffector2D>() == null) return true;
            return false;
        }

        /// <summary>몸 폭(half) 전체가 땅 위에 서도록 x를 가장 가까운 땅 쪽으로 옮긴다 (이미 땅 위면 그대로). 못 찾으면 원래 값.</summary>
        public static float NudgeOntoGround(float x, float half, float feetY, float minX, float maxX)
        {
            bool Ok(float cx) => HasGround(cx - half, feetY) && HasGround(cx + half, feetY);
            if (Ok(x)) return x;
            for (float d = 0.25f; d < 12f; d += 0.25f)
            {
                if (x + d <= maxX - half && Ok(x + d)) return x + d;
                if (x - d >= minX + half && Ok(x - d)) return x - d;
            }
            return x;
        }
    }
}
