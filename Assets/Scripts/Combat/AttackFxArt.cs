using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 공격 이펙트 그림 묶음 (판정 네모 대신 보이는 그림). 프레임은 Tools/sprites/process_sheets.ps1이
    /// 판정 박스 크기로 맞춰 만들고, 게임에서는 박스 크기로 늘려 그린다 → 보이는 그림 = 판정 범위.
    /// 회색 그림이라 공격색(일반 주황·강공격 빨강)은 게임에서 곱한다. 비어 있는 칸은 네모로 표시.
    /// </summary>
    public class AttackFxArt : ScriptableObject
    {
        [Tooltip("근접 몬스터 일반 베기 — 판정이 살아 있는 동안 첫 장, 꺼진 뒤 나머지(부서짐)")] public SpriteClip enemySlash = new();
        public SpriteClip enemyHeavySlash = new();
        [Tooltip("플레이어 반격 — 판정은 순간이라 첫 장을 보여주고 바로 부서짐")] public SpriteClip meleeCounter = new();
        public SpriteClip meleeAttack = new();

        [Header("연출 (원본 크기 × 배율로 그림 — 판정과 무관)")]
        [Tooltip("패링 성공 불꽃 (근접 / 원거리 무기)")] public SpriteClip parryMelee = new();
        public SpriteClip parryRanged = new();
        [Tooltip("원거리 무기 화살(부적) 날아가기 — 반복 / 맞는 순간 폭발")] public SpriteClip talismanProjectile = new();
        public SpriteClip talismanBurst = new();
        [Tooltip("몬스터가 맞는 순간 타격 / 죽을 때 소멸 / 강공격 경고 / 플레이어 착지 먼지")] public SpriteClip hitSpark = new();
        public SpriteClip yokaiDissolve = new();
        public SpriteClip heavyWarning = new();
        public SpriteClip dustLand = new();
        [Tooltip("도깨비불 불덩이 — 앞 4장 청록(일반) · 뒤 4장 빨강(강공격), 반복 재생")] public SpriteClip dokkaebiProjectile = new();

        public SpriteClip EnemySlash(AttackType type) => type == AttackType.Heavy ? enemyHeavySlash : enemySlash;
    }
}
