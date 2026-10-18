using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 맞은 몬스터가 얼마나 움찔하는가. Full = 밀리고 잠깐 경직 + 하던 공격(예비 모션)이 끊긴다 (반격·스킬·봉 3타) /
    /// Light = 살짝 밀리기만 하고 공격은 끊기지 않는다 (부적 평타) / None = 피해만, 밀리지도 끊기지도 않는다 (봉 1·2타).
    /// 평타만으로 몬스터 공격을 계속 끊으면 패링할 이유가 없어서 나눴다.
    /// </summary>
    public enum HitStagger { Full, Light, None }

    /// <summary>플레이어 딜의 출처.</summary>
    public enum DamageSource { Counter, AttackSkill, DashSlash }

    /// <summary>
    /// 플레이어 공격 한 번의 정보. 발동 순간에 만들어 투사체·근접 박스가 들고 다니다가, 명중 순간 데미지를 계산한다.
    /// </summary>
    public struct HitInfo
    {
        public DamageSource Source;
        public CharacterKind Kind;
        /// <summary>강공격 반격 (연출이 한 층 큼).</summary>
        public bool Heavy;
        /// <summary>발동 시각 (Time.time) — "스왑 직후 3초" 같은 시간 조건은 이 시각 기준.</summary>
        public float FiredTime;
        /// <summary>발동 순간 플레이어 위치 — 거리 조건(지척의 일격) 기준.</summary>
        public Vector2 FiredFrom;
        /// <summary>적을 뚫고 지나감 (증강 "관통 화살").</summary>
        public bool Pierce;
        /// <summary>원거리 무기로 쏜 공격 (공격력 배율이 근접과 다름).</summary>
        public bool Ranged;
        /// <summary>관통 순번 — 첫 적 0, 두 번째 적 1, …</summary>
        public int PierceIndex;
        /// <summary>맞은 몬스터의 경직 정도 (기본 Full).</summary>
        public HitStagger Stagger;

        public static HitInfo Create(DamageSource source, CharacterKind kind, Vector2 from, bool heavy = false) => new()
        {
            Source = source,
            Kind = kind,
            Heavy = heavy,
            FiredTime = Time.time,
            FiredFrom = from,
        };
    }

    /// <summary>
    /// 플레이어 딜 계산은 전부 여기 한 곳 (기획서 5.3).
    /// 최종 = 기본 × 캐릭터 패시브(공격력 배율). (증강은 폐지됐다 — 보너스 층이 필요하면 여기에 다시 붙인다.)
    /// </summary>
    public static class DamageCalc
    {
        private static readonly List<string> Notes = new();

        /// <param name="note">데미지 숫자 옆에 붙일 설명 (없으면 null)</param>
        public static int Compute(int baseDamage, in HitInfo hit, EnemyController target, out string note)
        {
            var t = GameTuning.Current;
            Notes.Clear();

            // 초반 배율(약하게 시작) × 무기 배율 × 스킬 트리·아이템 성장
            float passive = t.startDamageScale * (hit.Ranged ? t.rangedAttackMultiplier : t.AttackMultiplier(hit.Kind)) * SkillTreeState.AttackMultiplier(hit.Ranged);

            note = Notes.Count > 0 ? string.Join(" · ", Notes) : null;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * passive));
        }

        public static int Compute(int baseDamage, in HitInfo hit, EnemyController target) => Compute(baseDamage, hit, target, out _);

        /// <summary>적에게 계산된 데미지를 넣는다.</summary>
        public static int Apply(int baseDamage, in HitInfo hit, EnemyController target, float fromDirection)
        {
            int damage = Compute(baseDamage, hit, target, out string note);
            target.TakeDamage(damage, fromDirection, hit.Heavy, note, hit.Stagger);
            return damage;
        }
    }
}
