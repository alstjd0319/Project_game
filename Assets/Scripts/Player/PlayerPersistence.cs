using UnityEngine;

namespace ParryRL
{
    /// <summary>플레이어에 붙어, 씬이 시작될 때 <see cref="PlayerSession"/>에 담긴 상태를 되돌린다. 사당이면 체력을 자동으로 채운다.</summary>
    [RequireComponent(typeof(PlayerParty))]
    public class PlayerPersistence : MonoBehaviour
    {
        // 다른 컴포넌트의 Awake(기본 체력 등)가 끝난 뒤에 덮어써야 해서 Start에서 한다
        private void Start()
        {
            var party = GetComponent<PlayerParty>();
            party.SetMaxHpBonus(SkillTreeState.MaxHpBonus, false); // 스킬 트리 보너스를 먼저 — 아래 체력 복원이 늘어난 최대치 안에서 이뤄진다
            PlayerSession.Apply(party);
            if (!PlayerSession.HasData) party.SetHp(party.MaxHp);

            // 판 시작 도력 (스킬 트리): 플레이 씬에 들어올 때 그만큼은 채워 준다
            var gauge = GetComponent<SkillGauge>();
            if (!GameManager.InHub && gauge != null && SkillTreeState.StartGauge > gauge.Value) gauge.SetValue(SkillTreeState.StartGauge);

            // 사당(안전지대)에 들어오면 체력이 자동으로 가득 찬다 (도력·레벨·무기는 그대로)
            if (GameManager.InHub && party.Hp < party.MaxHp)
            {
                int healed = party.Heal(party.MaxHp - party.Hp); // "+N" 표시가 함께 뜬다
                if (healed > 0) Sfx.Play(SfxId.UiConfirm, 0.6f);
            }
        }
    }
}
