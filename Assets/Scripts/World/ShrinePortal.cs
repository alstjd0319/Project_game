using UnityEngine;

namespace ParryRL
{
    /// <summary>사당 오른쪽 끝의 귀문 포탈. 상호작용하면 플레이(게임) 씬으로 넘어간다.</summary>
    [RequireComponent(typeof(Interactable))]
    public class ShrinePortal : MonoBehaviour
    {
        [SerializeField] private ClipPlayer artPlayer;

        private Interactable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.SetPrompt("귀문으로 들어가기");
        }

        private void Start()
        {
            var fx = GameAssets.AttackFx;
            if (artPlayer != null && fx != null) artPlayer.Play(fx.gatePortal); // 귀문 소용돌이 반복
        }

        private void OnEnable() => _interactable.Interacted += Enter;
        private void OnDisable() => _interactable.Interacted -= Enter;

        private void Enter()
        {
            Hud.ScreenFlash(new Color(0.65f, 0.45f, 1f, 0.8f));
            GameManager.LoadRun();
        }
    }
}
