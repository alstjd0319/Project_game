using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    public enum UpgradeId { CounterBurst, ChainCounter, Reflect, SoulDrain, GaugeOverflow, Mastery, PiercingTalisman, GuardTalismans }

    public enum UpgradeCategory { Parry, Gauge, Weapon, Auto }

    /// <summary>강화 하나의 정의. 같은 강화를 다시 고르면 단계(1~3)가 오른다.</summary>
    public class UpgradeDef
    {
        public UpgradeId Id;
        public string Name;
        public UpgradeCategory Category;
        public int MaxLevel = 3;
        /// <summary>그 단계의 효과 설명 (1부터).</summary>
        public Func<int, string> Describe;
        /// <summary>UiArt.skillIcons 번호 (그림이 없으면 글자만).</summary>
        public int Icon;
    }

    /// <summary>
    /// 웨이브 모드 전용 "판 안 강화" (docs/기획_웨이브모드.md 7-2장). 웨이브를 깨면 3장 중 하나를 고른다.
    /// 스탯만 올리는 건 스킬 트리 몫이라, 여기엔 노는 방식을 바꾸는 효과만 둔다. 수치는 전부 가안.
    /// 판이 시작될 때(<see cref="WaveController"/> 시작 · <see cref="PlayerSession.Reset"/>) 비운다.
    /// 효과는 <see cref="WaveUpgradeEffects"/>(이벤트 구독)와 읽는 쪽(PlayerCombat 쿨타임·관통)이 적용한다.
    /// </summary>
    public static class WaveUpgrades
    {
        // ── 단계별 수치 (인덱스 = 단계 − 1) ──
        public static readonly float[] BurstRadius = { 2f, 2.5f, 3f };
        public static readonly int[] BurstDamage = { 12, 18, 25 };
        public static readonly int[] ChainTargets = { 1, 2, 3 };
        public const int ChainDamage = 12;
        public const float ChainRange = 7f;
        public static readonly float[] ReflectMultiplier = { 1f, 1.5f, 2f };
        public const int ReflectBaseDamage = 15;
        public static readonly int[] DrainHeal = { 1, 2, 3 };
        public static readonly int[] GaugeBonusByLevel = { 1, 2, 3 };
        public static readonly float[] CooldownCut = { 0.15f, 0.3f, 0.45f };
        public static readonly int[] PierceByLevel = { 1, 2, 3 };
        public static readonly int[] GuardCount = { 2, 3, 4 };
        public const int GuardDamage = 5;

        public static readonly UpgradeDef[] All =
        {
            new() { Id = UpgradeId.CounterBurst, Name = "반격 파동", Category = UpgradeCategory.Parry, Icon = 2,
                Describe = l => $"퍼펙트 패링·강공격 패링 때 주변에 충격파\n범위 {BurstRadius[l - 1]:0.#} · 피해 {BurstDamage[l - 1]}" },
            new() { Id = UpgradeId.ChainCounter, Name = "연쇄 반격", Category = UpgradeCategory.Parry, Icon = 9,
                Describe = l => $"반격이 가까운 다른 적 {ChainTargets[l - 1]}마리에게\n부적으로 튄다 (피해 {ChainDamage})" },
            new() { Id = UpgradeId.Reflect, Name = "되받아치기", Category = UpgradeCategory.Parry, Icon = 4,
                Describe = l => $"막은 불덩이·가시가 쏜 몬스터에게\n되돌아간다 (피해 {Mathf.RoundToInt(ReflectBaseDamage * ReflectMultiplier[l - 1])})" },
            new() { Id = UpgradeId.SoulDrain, Name = "흡혼", Category = UpgradeCategory.Parry, Icon = 7,
                Describe = l => $"패링에 성공할 때마다\n체력 +{DrainHeal[l - 1]}" },
            new() { Id = UpgradeId.GaugeOverflow, Name = "도력 넘침", Category = UpgradeCategory.Gauge, Icon = 10,
                Describe = l => $"패링으로 얻는 도력\n+{GaugeBonusByLevel[l - 1]}" },
            new() { Id = UpgradeId.Mastery, Name = "숙련", Category = UpgradeCategory.Gauge, Icon = 8,
                Describe = l => $"D · F 스킬 쿨타임\n−{Mathf.RoundToInt(CooldownCut[l - 1] * 100)}%" },
            new() { Id = UpgradeId.PiercingTalisman, Name = "꿰뚫는 부적", Category = UpgradeCategory.Weapon, Icon = 5,
                Describe = l => $"부적 평타가 적을\n{PierceByLevel[l - 1]}마리 꿰뚫는다" },
            new() { Id = UpgradeId.GuardTalismans, Name = "수호 부적", Category = UpgradeCategory.Auto, Icon = 11,
                Describe = l => $"몸 주위를 부적 {GuardCount[l - 1]}장이 돌며\n닿은 적에게 피해 {GuardDamage}" },
        };

        public static readonly string[] CategoryNames = { "패링", "도력", "무기", "자동" };
        public static readonly Color[] CategoryColors =
        {
            new(0.55f, 0.9f, 1f),     // 패링: 옥빛
            new(0.72f, 0.5f, 1f),     // 도력: 귀문 보라
            new(1f, 0.78f, 0.4f),     // 무기: 금빛
            new(0.55f, 1f, 0.6f),     // 자동: 초록 (빨강은 적 공격 경고 전용)
        };

        private static readonly int[] Levels = new int[All.Length];

        public static event Action Changed;

        public static UpgradeDef Def(UpgradeId id) => All[(int)id];
        public static int Level(UpgradeId id) => Levels[(int)id];
        public static bool Has(UpgradeId id) => Levels[(int)id] > 0;
        public static bool Maxed(UpgradeId id) => Levels[(int)id] >= All[(int)id].MaxLevel;

        public static int TotalPicks
        {
            get
            {
                int n = 0;
                foreach (int l in Levels) n += l;
                return n;
            }
        }

        public static void Reset()
        {
            Array.Clear(Levels, 0, Levels.Length);
            Changed?.Invoke();
        }

        /// <summary>한 단계 올린다 (최대면 false).</summary>
        public static bool Add(UpgradeId id)
        {
            if (Maxed(id)) return false;
            Levels[(int)id]++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 고를 수 있는 강화 count개 (최대 단계는 빼고, 서로 다름). 패링 강화가 남아 있으면 적어도 하나는 패링 강화.
        /// </summary>
        public static List<UpgradeId> Roll(int count, System.Random rng = null)
        {
            rng ??= new System.Random();
            var pool = new List<UpgradeId>();
            foreach (var d in All) if (!Maxed(d.Id)) pool.Add(d.Id);
            var picks = new List<UpgradeId>();
            var parry = pool.FindAll(id => Def(id).Category == UpgradeCategory.Parry);
            if (parry.Count > 0 && count > 0)
            {
                var first = parry[rng.Next(parry.Count)];
                picks.Add(first);
                pool.Remove(first);
            }
            while (picks.Count < count && pool.Count > 0)
            {
                var id = pool[rng.Next(pool.Count)];
                picks.Add(id);
                pool.Remove(id);
            }
            // 패링이 항상 첫 칸이면 티가 나서 섞는다
            for (int i = picks.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (picks[i], picks[j]) = (picks[j], picks[i]);
            }
            return picks;
        }

        // ── 읽는 쪽에서 쓰는 값 ──
        private static int At(int[] table, UpgradeId id) => Has(id) ? table[Level(id) - 1] : 0;

        /// <summary>D · F 쿨타임 배율 (숙련).</summary>
        public static float SkillCooldownScale => Has(UpgradeId.Mastery) ? 1f - CooldownCut[Level(UpgradeId.Mastery) - 1] : 1f;
        /// <summary>부적 평타가 꿰뚫는 적 수.</summary>
        public static int PierceCount => At(PierceByLevel, UpgradeId.PiercingTalisman);
        public static int GaugeBonus => At(GaugeBonusByLevel, UpgradeId.GaugeOverflow);
        public static int Drain => At(DrainHeal, UpgradeId.SoulDrain);
        public static int Guards => At(GuardCount, UpgradeId.GuardTalismans);
        public static int Chains => At(ChainTargets, UpgradeId.ChainCounter);

        /// <summary>단계 표기 (글꼴에 로마 숫자가 없을 수 있어 글자로).</summary>
        public static string LevelText(int level) => $"{level}단계";
    }
}
