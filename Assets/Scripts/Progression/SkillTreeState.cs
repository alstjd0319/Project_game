using System;
using UnityEngine;

namespace ParryRL
{
    public enum SkillBranch { Health, MeleeAttack, RangedAttack, Resource }

    public enum SkillStat { MaxHp, MeleeAttackPercent, RangedAttackPercent, XpPercent, GaugeGain, StartGauge }

    /// <summary>스킬 트리 노드 한 칸의 정의. 같은 줄(가지)에서는 앞 노드를 1랭크 이상 익혀야 다음 노드가 열린다.</summary>
    public class SkillNode
    {
        public string Id;
        public string Name;
        public SkillBranch Branch;
        public SkillStat Stat;
        public int PerRank;
        public int MaxRank;
        public string Unit;
        public string StatName;
        public int Cost = 1;

        public string Effect(int rank) => $"{StatName} +{PerRank * rank}{Unit}";
    }

    /// <summary>
    /// 사당 NPC(만신)의 스킬 트리로 올리는 스탯 (체력 · 근접 공격력 · 원거리 공격력 · 자원). <b>틀만 있는 첫 버전</b> — 노드 표(<see cref="Nodes"/>)에 한 줄 더하면 칸이 는다.
    /// 포인트는 레벨업마다 1점 (총 포인트 = 레벨 − 1, 쓴 포인트 = 익힌 랭크의 비용 합)이라 따로 저장하지 않는다.
    /// 익힌 랭크는 정적 변수에 두고 새 게임·다시 시작·시작 화면(<see cref="PlayerSession.Reset"/>)에서 비운다.
    /// 효과는 읽는 쪽이 보너스 프로퍼티를 곱하거나 더한다 (PlayerParty.MaxHp · DamageCalc · PlayerLevel · PlayerCombat · PlayerPersistence).
    /// </summary>
    public static class SkillTreeState
    {
        public static readonly SkillNode[] Nodes =
        {
            new() { Id = "hp1", Name = "튼튼한 몸", Branch = SkillBranch.Health, Stat = SkillStat.MaxHp, PerRank = 10, MaxRank = 3, StatName = "최대 체력", Unit = "" },
            new() { Id = "hp2", Name = "단단한 뼈대", Branch = SkillBranch.Health, Stat = SkillStat.MaxHp, PerRank = 15, MaxRank = 2, StatName = "최대 체력", Unit = "" },
            new() { Id = "hp3", Name = "불굴의 기운", Branch = SkillBranch.Health, Stat = SkillStat.MaxHp, PerRank = 25, MaxRank = 1, StatName = "최대 체력", Unit = "" },
            new() { Id = "mel1", Name = "날 선 봉", Branch = SkillBranch.MeleeAttack, Stat = SkillStat.MeleeAttackPercent, PerRank = 10, MaxRank = 3, StatName = "근접 공격력", Unit = "%" },
            new() { Id = "mel2", Name = "매서운 손끝", Branch = SkillBranch.MeleeAttack, Stat = SkillStat.MeleeAttackPercent, PerRank = 20, MaxRank = 2, StatName = "근접 공격력", Unit = "%" },
            new() { Id = "mel3", Name = "필살의 일격", Branch = SkillBranch.MeleeAttack, Stat = SkillStat.MeleeAttackPercent, PerRank = 40, MaxRank = 1, StatName = "근접 공격력", Unit = "%" },
            new() { Id = "rng1", Name = "먹 머금은 부적", Branch = SkillBranch.RangedAttack, Stat = SkillStat.RangedAttackPercent, PerRank = 10, MaxRank = 3, StatName = "원거리 공격력", Unit = "%" },
            new() { Id = "rng2", Name = "날랜 손놀림", Branch = SkillBranch.RangedAttack, Stat = SkillStat.RangedAttackPercent, PerRank = 20, MaxRank = 2, StatName = "원거리 공격력", Unit = "%" },
            new() { Id = "rng3", Name = "천벌의 부적", Branch = SkillBranch.RangedAttack, Stat = SkillStat.RangedAttackPercent, PerRank = 40, MaxRank = 1, StatName = "원거리 공격력", Unit = "%" },
            new() { Id = "res1", Name = "배움의 기운", Branch = SkillBranch.Resource, Stat = SkillStat.XpPercent, PerRank = 10, MaxRank = 3, StatName = "경험치 획득", Unit = "%" },
            new() { Id = "res2", Name = "도력 샘", Branch = SkillBranch.Resource, Stat = SkillStat.GaugeGain, PerRank = 1, MaxRank = 2, StatName = "패링 성공 도력", Unit = "" },
            new() { Id = "res3", Name = "채워진 염주", Branch = SkillBranch.Resource, Stat = SkillStat.StartGauge, PerRank = 3, MaxRank = 1, StatName = "판 시작 도력", Unit = "" },
        };

        public static readonly string[] BranchNames = { "체력", "근접", "원거리", "자원" };

        private static readonly int[] Ranks = new int[Nodes.Length];

        public static event Action Changed;

        public static void Reset()
        {
            Array.Clear(Ranks, 0, Ranks.Length);
            Changed?.Invoke();
        }

        public static int Rank(int index) => Ranks[index];

        /// <summary>줄(가지) 안의 순서 (0~2)에서 노드 번호를 찾는다.</summary>
        public static int IndexOf(SkillBranch branch, int slot)
        {
            int n = 0;
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i].Branch == branch && n++ == slot) return i;
            return -1;
        }

        public static int Slot(int index)
        {
            int n = 0;
            for (int i = 0; i < index; i++) if (Nodes[i].Branch == Nodes[index].Branch) n++;
            return n;
        }

        public static int TotalPoints(int level) => Mathf.Max(0, level - 1);

        public static int Spent
        {
            get
            {
                int s = 0;
                for (int i = 0; i < Nodes.Length; i++) s += Ranks[i] * Nodes[i].Cost;
                return s;
            }
        }

        /// <summary>연습 스위치 "스킬 포인트 무제한"이 켜져 있는가 (설정창).</summary>
        public static bool PointsUnlimited => GameTuning.Current.infiniteSkillPoints;

        public static int Available(int level) => PointsUnlimited ? 99 : TotalPoints(level) - Spent;

        /// <summary>앞 노드를 1랭크 이상 익혔는가 (첫 노드는 항상 열려 있다).</summary>
        public static bool Unlocked(int index)
        {
            int slot = Slot(index);
            return slot == 0 || Ranks[IndexOf(Nodes[index].Branch, slot - 1)] >= 1;
        }

        public static bool Maxed(int index) => Ranks[index] >= Nodes[index].MaxRank;

        public static bool CanBuy(int index, int level) =>
            index >= 0 && index < Nodes.Length && Unlocked(index) && !Maxed(index) && Available(level) >= Nodes[index].Cost;

        public static bool TryBuy(int index, int level)
        {
            if (!CanBuy(index, level)) return false;
            Ranks[index]++;
            Changed?.Invoke();
            return true;
        }

        private static int Sum(SkillStat stat)
        {
            int s = 0;
            for (int i = 0; i < Nodes.Length; i++) if (Nodes[i].Stat == stat) s += Ranks[i] * Nodes[i].PerRank;
            return s + RunState.ItemSum(stat); // 이번 판 아이템 효과도 같은 합산에 들어간다
        }

        public static int MaxHpBonus => Sum(SkillStat.MaxHp);
        public static float MeleeAttackMultiplier => 1f + Sum(SkillStat.MeleeAttackPercent) / 100f;
        public static float RangedAttackMultiplier => 1f + Sum(SkillStat.RangedAttackPercent) / 100f;
        public static float AttackMultiplier(bool ranged) => ranged ? RangedAttackMultiplier : MeleeAttackMultiplier;
        public static float XpMultiplier => 1f + Sum(SkillStat.XpPercent) / 100f;
        public static int GaugeGainBonus => Sum(SkillStat.GaugeGain);
        public static int StartGauge => Sum(SkillStat.StartGauge);
    }
}
