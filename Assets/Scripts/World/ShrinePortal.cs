using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 사당 오른쪽 끝의 귀문 포탈(문). 상호작용하면 플레이(게임) 씬으로 넘어간다.
    /// 문 그림은 정지 그림이라 문 안쪽에 일렁이는 소용돌이(<see cref="AttackFxArt.portalSwirl"/> 8장 반복)와 은은한 빛무리를 겹쳐 그린다.
    /// 플레이어가 가까이 올수록 빛이 밝아지고, 상호작용이 가능한 거리에서는 더 환하게 숨 쉰다. 연출만 있고 판정은 없다.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class ShrinePortal : MonoBehaviour
    {
        [SerializeField] private ClipPlayer artPlayer;
        [SerializeField, Tooltip("정지 문 그림 (있으면 소용돌이·빛무리를 겹쳐 그린다 — DoorEffects)")] private SpriteRenderer gateArt;

        private Interactable _interactable;
        private DoorEffects _effects;

        /// <summary>플레이어가 가까울수록 1에 가까워지는 밝기 계수 (0 = 멀리, 1 = 문 앞). 테스트·연출 확인용.</summary>
        public float Brightness => _effects != null ? _effects.Brightness : 0f;
        public bool HasDoorEffects => _effects != null && _effects.HasEffects;

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.SetPrompt("귀문으로 들어가기");
        }

        private void Start()
        {
            var fx = GameAssets.AttackFx;
            if (artPlayer != null && fx != null) artPlayer.Play(fx.gatePortal); // 그림이 없을 때의 대체 (소용돌이 이펙트만)
            if (gateArt != null) _effects = gameObject.AddComponent<DoorEffects>(); // 문 안쪽 일렁이는 소용돌이·빛무리
        }

        private void OnEnable() => GetComponent<Interactable>().Interacted += Enter;
        private void OnDisable() => GetComponent<Interactable>().Interacted -= Enter;

        private void Enter()
        {
            Hud.ScreenFlash(new Color(0.65f, 0.45f, 1f, 0.8f));
            RunState.Begin(); // 한 판 시작 (첫 방은 일반방)
            GameManager.LoadRun();
        }
    }
}
