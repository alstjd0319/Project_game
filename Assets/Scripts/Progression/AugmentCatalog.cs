using System.Collections.Generic;
using System.Linq;

namespace ParryRL
{
    /// <summary>
    /// 증강 목록 (기획서 5.1 재설계안). 수치는 전부 가안 — 각 효과 클래스 맨 위 상수에서 바꾼다.
    /// 증강 추가 = 효과 클래스 1개 + 여기 한 줄. 삭제 = 반대로.
    /// </summary>
    public static class AugmentCatalog
    {
        public static readonly IReadOnlyList<AugmentDefinition> All = new List<AugmentDefinition>
        {
            // ── 공용: 방어 루프 자체 강화 ──
            new() { Id = "wide_window", Name = "넓은 판정", Owner = AugmentOwner.Common, Category = AugmentCategory.Defense, MaxStacks = 3,
                Effect = typeof(WideWindowAugment),
                Description = "패링 활성 시간이 0.05초 늘어난다." },
            new() { Id = "chain_defense", Name = "연쇄 방어", Owner = AugmentOwner.Common, Category = AugmentCategory.Gauge,
                Effect = typeof(ChainDefenseAugment),
                Description = "헛스윙·피격 없이 방어를 3번 연속 성공할 때마다 게이지 +2칸." },
            new() { Id = "heavy_hunter", Name = "강공격 사냥꾼", Owner = AugmentOwner.Common, Category = AugmentCategory.Gauge,
                Effect = typeof(HeavyHunterAugment),
                Description = "강공격을 막으면 게이지가 2칸 더 찬다 (3 → 5칸)." },

            // ── 플레이어: 가까이 · 세게 · 단단하게 ──
            new() { Id = "w_close_strike", Name = "지척의 일격", Owner = AugmentOwner.Player, Category = AugmentCategory.Counter,
                Effect = typeof(CloseStrikeAugment),
                Description = "바짝 붙은 적(1.2 이내)에게 반격하면 데미지 +50%." },
            new() { Id = "w_blood_parry", Name = "피의 패링", Owner = AugmentOwner.Player, Category = AugmentCategory.Defense, MaxStacks = 2,
                Effect = typeof(BloodParryAugment),
                Description = "패링에 성공하면 체력 +2 (강공격 +6)." },
            new() { Id = "w_dash_slash", Name = "돌진 베기", Owner = AugmentOwner.Player, Category = AugmentCategory.Mobility,
                Effect = typeof(DashSlashAugment),
                Description = "돌진(이동 스킬)으로 지나간 적에게 피해 15 (적마다 1번)." },
        };

        public static AugmentDefinition Get(string id) => All.First(a => a.Id == id);
    }
}
