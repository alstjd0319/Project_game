namespace ParryRL
{
    /// <summary>플레이어 캐릭터. 궁수는 폐기(2026-10-08) — 전사 하나뿐이지만 HitInfo 등이 참조해 남겨 둔다.</summary>
    public enum CharacterKind { Warrior }

    public enum AttackType { Normal, Heavy }

    public enum DefenseState { Ready, Active, Cooldown }

    public enum EnemyKind { Melee, Ranged }

    /// <summary>플레이어 입력 행동 3종 (스킬바 슬롯과 1:1).</summary>
    public enum PlayerAction { Move, Attack, Defend }
}
