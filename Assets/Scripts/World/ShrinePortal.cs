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
        // 문 그림(×1.25) 안쪽 문턱: 바닥에서 약 1.9유닛 위가 가운데, 열린 부분 약 1.25 × 2.1유닛 (빛 크기 기준)
        private const float DoorCenterHeight = 1.9f;
        private const float DoorWidth = 1.25f;
        private const float DoorHeight = 2.1f;
        private const float FarRange = 7f;     // 이 거리 밖에서는 평소 밝기
        private const float NearRange = 1.5f;  // 이 안에서는 가장 밝다

        [SerializeField] private ClipPlayer artPlayer;
        [SerializeField, Tooltip("정지 문 그림 (있으면 소용돌이·빛무리를 겹쳐 그린다)")] private SpriteRenderer gateArt;

        private Interactable _interactable;
        private Transform _player;
        private PlayerInteractor _interactor;
        private SpriteRenderer _halo, _glow, _swirl;
        private Vector2 _haloBase, _glowBase;
        private Vector3 _swirlBase;
        private float _near;
        private float _time;

        /// <summary>플레이어가 가까울수록 1에 가까워지는 밝기 계수 (0 = 멀리, 1 = 문 앞). 테스트·연출 확인용.</summary>
        public float Brightness => _near;
        public bool HasDoorEffects => _glow != null && _halo != null && _swirl != null;

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.SetPrompt("귀문으로 들어가기");
        }

        private void Start()
        {
            var fx = GameAssets.AttackFx;
            if (artPlayer != null && fx != null) artPlayer.Play(fx.gatePortal); // 그림이 없을 때의 대체 (소용돌이 이펙트만)
            if (gateArt != null) BuildDoorEffects(fx);
            var party = FindAnyObjectByType<PlayerParty>();
            if (party != null)
            {
                _player = party.transform;
                _interactor = party.GetComponent<PlayerInteractor>();
            }
        }

        private void BuildDoorEffects(AttackFxArt fx)
        {
            float groundY = transform.position.y - transform.localScale.y * 0.5f;
            var center = new Vector3(transform.position.x, groundY + DoorCenterHeight, 0f);

            // 문 뒤로 번지는 빛무리 (문 그림 뒤, 윤곽 밖으로 은은하게)
            _haloBase = new Vector2(5.4f, 5.6f);
            _halo = MakeGlow("PortalHalo", center, _haloBase, 8);
            // 문 안쪽 빛 (소용돌이 위에 살짝)
            _glowBase = new Vector2(DoorWidth * 1.5f, DoorHeight * 1.2f);
            _glow = MakeGlow("PortalGlow", center, _glowBase, 11);

            // 문 안쪽 일렁이는 소용돌이: 칸 192×224 중 내용(138×181)이 문 열린 부분(DoorWidth × DoorHeight)에 꼭 맞게 늘려 반복 재생
            if (fx != null && !fx.portalSwirl.IsEmpty)
            {
                var go = new GameObject("PortalSwirl");
                go.transform.position = center;
                Vector2 cell = fx.portalSwirl.frames[0].bounds.size;                       // 칸 크기(유닛)
                go.transform.localScale = new Vector3(DoorWidth / (cell.x * 138f / 192f), DoorHeight / (cell.y * 181f / 224f), 1f);
                _swirlBase = go.transform.localScale;
                _swirl = go.AddComponent<SpriteRenderer>();
                _swirl.sortingOrder = 10;
                go.AddComponent<ClipPlayer>().Play(fx.portalSwirl);
            }

        }

        private static SpriteRenderer MakeGlow(string name, Vector3 pos, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FxSprites.Glow;
            sr.sortingOrder = order;
            sr.color = new Color(0.65f, 0.42f, 1f, 0f);
            return sr;
        }

        private void Update()
        {
            if (!HasDoorEffects) return;
            _time += Time.deltaTime;

            float target = 0f;
            if (_player != null)
            {
                float dist = Mathf.Abs(_player.position.x - transform.position.x);
                target = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01((dist - NearRange) / (FarRange - NearRange)));
            }
            _near = Mathf.MoveTowards(_near, target, Time.deltaTime * 3f);

            bool canEnter = _interactor != null && _interactor.Current == _interactable;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 2.2f);
            float boost = canEnter ? 0.2f + 0.1f * Mathf.Sin(_time * 7f) : 0f; // 들어갈 수 있는 거리에서는 더 환하게

            SetAlpha(_halo, Mathf.Lerp(0.14f, 0.55f, _near) * (0.8f + 0.2f * pulse) + boost * 0.5f);
            _halo.transform.localScale = new Vector3(_haloBase.x, _haloBase.y, 1f) * (1f + 0.04f * pulse + 0.1f * _near);
            SetAlpha(_glow, Mathf.Lerp(0.06f, 0.4f, _near) * (0.7f + 0.3f * pulse) + boost);
            _glow.transform.localScale = new Vector3(_glowBase.x, _glowBase.y, 1f) * (1f + 0.06f * pulse);
            SetAlpha(_swirl, Mathf.Lerp(0.55f, 0.95f, _near) * (0.88f + 0.12f * Mathf.Sin(_time * 5.3f)));
            // 프레임의 물결에 더해 전체가 부풀었다 가라앉고 좌우로 살랑이게 (가까울수록 조금 더 크게 일렁인다)
            float k = 1f + 0.5f * _near;
            float sx = 1f + 0.07f * k * Mathf.Sin(_time * 3.1f);
            float sy = 1f + 0.05f * k * Mathf.Sin(_time * 2.3f + 1f);
            _swirl.transform.localScale = new Vector3(_swirlBase.x * sx, _swirlBase.y * sy, 1f);
            var p = _swirl.transform.position;
            p.x = transform.position.x + 0.05f * k * Mathf.Sin(_time * 1.7f);
            _swirl.transform.position = p;
        }

        private static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            var c = sr.color;
            c.a = Mathf.Clamp01(alpha);
            sr.color = c;
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
