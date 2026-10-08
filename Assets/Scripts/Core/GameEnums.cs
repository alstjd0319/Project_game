namespace ParryRL
{
    /// <summary>플레이어 캐릭터. 궁수는 폐기(2026-10-08) — 플레이어 하나뿐이지만 HitInfo 등이 참조해 남겨 둔다.</summary>
    public enum CharacterKind { Player }

    public enum AttackType { Normal, Heavy }

    /// <summary>플레이어가 Q로 바꾸는 무기: 근접(칼) ↔ 원거리(화살). 일반공격(A)과 패링 반격이 무기에 따라 달라진다.</summary>
    public enum WeaponMode { Melee, Ranged }

    public enum DefenseState { Ready, Active, Cooldown }

    public enum EnemyKind { Melee, Ranged }

    /// <summary>플레이어 입력 행동 4종 (스킬바 슬롯과 1:1).</summary>
    public enum PlayerAction { Move, Attack, Defend, Weapon }
}
