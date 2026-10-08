using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 플레이어 근접 판정 박스 (플레이어 반격 / 플레이어 공격 스킬 / 등장기 충격파).
    /// 생성 순간 한 번 OverlapBox로 판정하고, 판정한 네모를 그대로 잠깐 보여준다 → 보이는 범위 = 판정 범위.
    /// 데미지는 맞은 적마다 <see cref="DamageCalc"/>로 따로 계산한다 (거리 조건 증강이 적마다 다르게 붙으므로).
    /// </summary>
    public static class MeleeStrike
    {
        private static readonly HashSet<EnemyController> HitBuffer = new();
        private static readonly List<EnemyController> HitList = new();

        private const int Order = 30;
        // 판정 박스가 플레이어 몸 가운데부터 시작하므로, 그림은 플레이어 그림(12) 뒤·몬스터(8) 앞에 — 몸에 겹친 부분은 캐릭터에 가려진다
        private const int ArtOrder = 11;

        /// <param name="onHit">데미지를 넣은 뒤 적마다 호출 (기절 등 추가 효과)</param>
        /// <param name="art">판정 네모 대신 보여줄 그림 (박스 크기로 늘려 그림). 없으면 네모</param>
        /// <param name="facing">그림 방향 (+1 오른쪽)</param>
        /// <returns>맞은 적 수</returns>
        public static int Spawn(Vector2 center, Vector2 size, Color color, int baseDamage, HitInfo hit,
            Action<EnemyController> onHit = null, SpriteClip art = null, float facing = 1f)
        {
            bool hasArt = art != null && !art.IsEmpty;
            if (hasArt)
            {
                // 그림은 흰 먹물 — 캐릭터 색을 살짝만 섞는다
                var tint = Color.Lerp(Color.white, new Color(color.r, color.g, color.b, 1f), 0.25f);
                FxClip.Play(art, 0, center, size, facing < 0f, tint, ArtOrder);
            }
            if (!hasArt || GameTuning.Current.showHitboxes)
            {
                var boxColor = color;
                if (hasArt) boxColor.a *= 0.45f; // 판정 상자 보기: 그림 위에 반투명하게
                var go = Box.Create("MeleeStrike", center, size, boxColor, Order + 1);
                go.AddComponent<FxPiece>().Init(Vector2.zero, 0.14f, 1f, 1f, 0f, 0f);
            }

            Physics2D.SyncTransforms();
            HitBuffer.Clear();
            HitList.Clear();
            foreach (var col in Physics2D.OverlapBoxAll(center, size, 0f))
            {
                var enemy = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetComponent<EnemyController>()
                    : col.GetComponent<EnemyController>();
                if (enemy != null && !enemy.IsDead && HitBuffer.Add(enemy)) HitList.Add(enemy);
            }

            // 콜백 안에서 또 MeleeStrike가 불려도 안전하도록 복사본으로 돈다
            var targets = HitList.ToArray();
            foreach (var enemy in targets)
            {
                float dir = Mathf.Sign(enemy.transform.position.x - center.x);
                DamageCalc.Apply(baseDamage, hit, enemy, dir);
                if (!enemy.IsDead) onHit?.Invoke(enemy);
            }
            return targets.Length;
        }
    }
}
