using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>플레이어 딜의 출처. 증강 보너스가 어디에 붙는지 가르는 기준.</summary>
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
        /// <summary>관통 순번 — 첫 적 0, 두 번째 적 1, …</summary>
        public int PierceIndex;

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
    /// 최종 = 기본 × 캐릭터 패시브(공격력 배율) × (1 + 증강 보너스 합).
    /// 증강끼리는 더하고 캐릭터 패시브에만 곱한다 — 증강을 전부 곱하면 빌드 간 편차가 폭발하기 때문.
    /// </summary>
    public static class DamageCalc
    {
        private static readonly List<string> Notes = new();

        /// <param name="note">데미지 숫자 옆에 붙일 설명 (없으면 null)</param>
        public static int Compute(int baseDamage, in HitInfo hit, EnemyController target, out string note)
        {
            var t = GameTuning.Current;
            Notes.Clear();

            float passive = t.AttackMultiplier(hit.Kind);

            float bonus = RunModifiers.Instance != null ? RunModifiers.Instance.DamageBonus(hit, target, Notes) : 0f;
            note = Notes.Count > 0 ? string.Join(" · ", Notes) : null;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * passive * (1f + bonus)));
        }

        public static int Compute(int baseDamage, in HitInfo hit, EnemyController target) => Compute(baseDamage, hit, target, out _);

        /// <summary>적에게 계산된 데미지를 넣는다.</summary>
        public static int Apply(int baseDamage, in HitInfo hit, EnemyController target, float fromDirection)
        {
            int damage = Compute(baseDamage, hit, target, out string note);
            target.TakeDamage(damage, fromDirection, hit.Heavy, note);
            return damage;
        }
    }
}
