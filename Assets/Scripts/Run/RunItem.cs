using System.Collections.Generic;

namespace ParryRL
{
    /// <summary>이번 판 동안만 쓰는 아이템 (엘리트·보상·상점에서 얻는다). 효과는 스킬 트리와 같은 스탯 합산(<see cref="SkillTreeState"/>)에 더해진다. 가안.</summary>
    public class RunItem
    {
        public string Id;
        public string Name;
        public SkillStat Stat;
        public int Amount;
        public string Unit;
        public string StatName;

        public string Describe => $"{StatName} +{Amount}{Unit}";
    }

    public static class RunItemCatalog
    {
        public static readonly RunItem[] All =
        {
            new() { Id = "bell_red", Name = "붉은 방울", Stat = SkillStat.MeleeAttackPercent, Amount = 15, Unit = "%", StatName = "근접 공격력" },
            new() { Id = "bell_ink", Name = "먹 방울", Stat = SkillStat.RangedAttackPercent, Amount = 15, Unit = "%", StatName = "원거리 공격력" },
            new() { Id = "amber", Name = "호박 구슬", Stat = SkillStat.MaxHp, Amount = 20, Unit = "", StatName = "최대 체력" },
            new() { Id = "rosary", Name = "도력 염주", Stat = SkillStat.GaugeGain, Amount = 1, Unit = "", StatName = "패링 성공 도력" },
            new() { Id = "scroll", Name = "배움 두루마리", Stat = SkillStat.XpPercent, Amount = 20, Unit = "%", StatName = "경험치 획득" },
        };

        /// <summary>겹치지 않게 n개를 뽑는다.</summary>
        public static List<RunItem> Roll(int count, System.Random rng = null)
        {
            rng ??= new System.Random();
            var pool = new List<RunItem>(All);
            var result = new List<RunItem>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                result.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return result;
        }
    }
}
