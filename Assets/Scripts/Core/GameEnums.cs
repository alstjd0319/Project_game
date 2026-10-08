namespace ParryRL
{
    public enum CharacterKind { Warrior, Archer }

    public enum AttackType { Normal, Heavy }

    /// <summary>HeavyParry: 강공격 방어 성공 / Defense: 일반 공격 방어 성공 (둘 다 자동 스왑) / Manual: 수동 스왑.</summary>
    public enum SwapCause { HeavyParry, Defense, Manual }

    public enum DefenseState { Ready, Active, Cooldown }

    public enum EnemyKind { Melee, Ranged }

    /// <summary>플레이어 입력 행동 4종 (스킬바 슬롯과 1:1).</summary>
    public enum PlayerAction { Move, Attack, Defend, Swap }
}
