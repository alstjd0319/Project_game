using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 씬을 넘어 이어지는 플레이어 데이터 (사당 ↔ 플레이 씬). 씬이 바뀌면 씬 안의 오브젝트는 사라지므로,
    /// 떠나기 직전에 <see cref="Capture"/>로 정적 변수에 담아 두고 새 씬의 <see cref="PlayerPersistence"/>가 되돌려 준다.
    /// 이어지는 것: 체력 · 레벨/경험치 · 도력 · 든 무기. (쿨타임·진행 중인 판 상태는 이어지지 않는다.)
    /// 새 게임(시작 화면 → 게임 시작)·다시 시작(R)·시작 화면으로 가면 <see cref="Reset"/>으로 비운다.
    /// </summary>
    public static class PlayerSession
    {
        public struct Data
        {
            public int Hp;
            public int Level;
            public int Xp;
            public int Gauge;
            public WeaponMode Weapon;
        }

        /// <summary>담아 둔 데이터가 있는가 (없으면 새 씬은 기본값으로 시작).</summary>
        public static bool HasData { get; private set; }
        public static Data Current { get; private set; }

        public static void Reset()
        {
            HasData = false;
            SkillTreeState.Reset(); // 포인트는 레벨에서 나오므로 익힌 랭크도 같이 비운다
        }

        /// <summary>지금 씬의 플레이어 상태를 담는다. 플레이어가 없는 씬(시작 화면 등)에서는 아무것도 안 한다.</summary>
        public static void Capture()
        {
            var party = Object.FindAnyObjectByType<PlayerParty>();
            if (party == null) return;
            var level = party.GetComponent<PlayerLevel>();
            var gauge = party.GetComponent<SkillGauge>();
            var combat = party.GetComponent<PlayerCombat>();
            Current = new Data
            {
                Hp = party.Hp,
                Level = level != null ? level.Level : 1,
                Xp = level != null ? level.Xp : 0,
                Gauge = gauge != null ? gauge.Value : 0,
                Weapon = combat != null ? combat.Weapon : WeaponMode.Melee,
            };
            HasData = true;
        }

        /// <summary>담아 둔 상태를 새 씬의 플레이어에 되돌린다. 쓰러진 채(체력 0) 떠났다면 체력은 가득 채운다.</summary>
        public static void Apply(PlayerParty party)
        {
            if (!HasData || party == null) return;
            var d = Current;
            party.SetHp(d.Hp <= 0 ? party.MaxHp : d.Hp);
            var level = party.GetComponent<PlayerLevel>();
            if (level != null) level.Restore(d.Level, d.Xp);
            var gauge = party.GetComponent<SkillGauge>();
            if (gauge != null) gauge.SetValue(d.Gauge);
            var combat = party.GetComponent<PlayerCombat>();
            if (combat != null) combat.SetWeapon(d.Weapon);
        }
    }
}
