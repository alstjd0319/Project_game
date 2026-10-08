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

        public SpriteClip EnemySlash(AttackType type) => type == AttackType.Heavy ? enemyHeavySlash : enemySlash;
    }
}
