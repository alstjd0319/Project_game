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
            PlayerSession.Apply(party);

            // 사당(안전지대)에 들어오면 체력이 자동으로 가득 찬다 (도력·레벨·무기는 그대로)
            if (GameManager.InHub && party.Hp < party.MaxHp)
            {
                int healed = party.Heal(party.MaxHp - party.Hp); // "+N" 표시가 함께 뜬다
                if (healed > 0) Sfx.Play(SfxId.UiConfirm, 0.6f);
            }
        }
    }
}
