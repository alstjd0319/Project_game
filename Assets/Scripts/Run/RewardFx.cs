using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 재화를 얻는 연출 (1016 재화 그림): 그 자리에서 엽전·영혼석·혼불이 톡 튀어 오르며 돌다가 줍기 반짝임과 함께 사라진다.
    /// 판정 없음 · 연출만. 여러 개면 조금씩 흩어 뿌린다. 그림이 없으면 아무것도 안 한다(글자 알림은 따로).
    /// </summary>
    public static class RewardFx
    {
        public enum Kind { Coin, Soul, Exp }

        /// <summary>테스트용: 지금까지 뿌린 개수.</summary>
        public static int SpawnCount { get; private set; }

        public static void Burst(Kind kind, Vector2 at, int count = 1)
        {
            var art = GameAssets.Reward;
            if (art == null) return;
            var (loop, pickup, scale) = kind switch
            {
                Kind.Coin => (art.coinSpin, art.pickupCoin, 0.38f),
                Kind.Soul => (art.soulFloat, art.pickupSoul, 0.55f),
                _ => (art.expOrb, art.pickupExp, 0.5f),
            };
            if (loop == null || loop.IsEmpty) return;
            count = Mathf.Clamp(count, 1, 6);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Reward_{kind}");
                go.transform.position = at;
                go.transform.localScale = Vector3.one * scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 47;
                go.AddComponent<ClipPlayer>().Play(loop);
                var runner = go.AddComponent<RewardFxRunner>();
                float spread = count == 1 ? 0f : (i - (count - 1) * 0.5f) * 0.45f;
                runner.Begin(new Vector2(spread + Random.Range(-0.15f, 0.15f), 3.2f + Random.Range(0f, 0.8f)), pickup, i * 0.06f);
                SpawnCount++;
            }
        }
    }
}
