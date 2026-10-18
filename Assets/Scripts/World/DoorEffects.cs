using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 문(포탈)의 일렁이는 빛 연출: 문 안쪽 소용돌이(<see cref="AttackFxArt.portalSwirl"/> 반복) + 문 뒤 빛무리 + 안쪽 빛.
    /// 플레이어가 가까이 올수록 밝아지고, 상호작용이 가능한 거리에서는 더 환하게 숨 쉰다. 연출만 있고 판정은 없다.
    /// 사당 포탈(<see cref="ShrinePortal"/>)과 방 문(<see cref="RoomGate"/>)이 같이 쓴다. 문 그림(×1.25)의 안쪽 문턱 크기에 맞춰져 있고,
    /// 문 오브젝트 중심에서 아래로 높이/2 내려간 곳이 바닥이라고 본다.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class DoorEffects : MonoBehaviour
    {
        // 문 그림(×1.25) 안쪽 문턱: 바닥에서 약 1.9유닛 위가 가운데, 열린 부분 약 1.25 × 2.1유닛 (빛 크기 기준)
        private const float DoorCenterHeight = 1.9f;
        private const float DoorWidth = 1.25f;
        private const float DoorHeight = 2.1f;
        private const float FarRange = 7f;     // 이 거리 밖에서는 평소 밝기
        private const float NearRange = 1.5f;  // 이 안에서는 가장 밝다

        /// <summary>문 안쪽 열린 부분: 바닥에서 가운데까지 높이 · 크기(유닛). 기본은 사당 문 그림(×1.25). 방 포탈 틀은 틀마다 다르다 (Start 전에 정할 것).</summary>
        public float OpeningCenterHeight { get; set; } = DoorCenterHeight;
        public Vector2 OpeningSize { get; set; } = new(DoorWidth, DoorHeight);
        /// <summary>
        /// true면 소용돌이로 에셋의 귀문 소용돌이(<see cref="AttackFxArt.gatePortal"/>, 타원형 6장: 열림 → 휘몰아침 → 닫힘)를 쓴다 —
        /// 문이 생길 때 열리는 장면(0·1)을 한 번, 그 뒤 휘몰아치는 장면(2·3·4·3)을 반복. 문 틀 뒤에 그려 구름 테두리가 틀 안쪽에 묻힌다.
        /// 에셋 안내(1016 안내.md)의 "방마다 색만 바꿔 쓰는" 계획대로 방 포탈 틀에서 쓴다. false면 사당 문의 네모 소용돌이(portalSwirl).
        /// </summary>
        public bool UseGatePortal { get; set; }
        /// <summary>문 틀 구멍 모양 가림막 (틀 그림과 같은 크기·아래 가운데 피벗). 있으면 소용돌이를 구멍보다 크게 그리고 구멍 모양대로만 보이게 한다.</summary>
        public Sprite HoleMask { get; set; }
        // 귀문 소용돌이 칸 256×256 중 타원 내용(구름 테두리 포함) 약 150×256
        private const float GateContentW = 150f, GateContentH = 256f, GateCell = 256f;

        /// <summary>빛무리 크기 배율 (사당 문 기준 1).</summary>
        public float HaloScale { get; set; } = 1f;

        /// <summary>빛 색 (보라 = 귀문 기본). 방 문은 방 종류 색을 섞는다.</summary>
        public Color Tint { get; set; } = new Color(0.65f, 0.42f, 1f);
        /// <summary>소용돌이에 섞는 색 비율 (0 = 그림 그대로).</summary>
        public float SwirlTintAmount { get; set; }

        private Interactable _interactable;
        private Transform _player;
        private PlayerInteractor _interactor;
        private SpriteRenderer _halo, _glow, _swirl;
        private Vector2 _haloBase, _glowBase;
        private Vector3 _swirlBase;
        private float _near;
        private float _time;

        /// <summary>플레이어가 가까울수록 1에 가까워지는 밝기 계수 (0 = 멀리, 1 = 문 앞).</summary>
        public float Brightness => _near;
        public bool HasEffects => _glow != null && _halo != null && _swirl != null;

        private void Awake() => _interactable = GetComponent<Interactable>();

        private void Start()
        {
            var party = FindAnyObjectByType<PlayerParty>();
            if (party != null)
            {
                _player = party.transform;
                _interactor = party.GetComponent<PlayerInteractor>();
            }
            Build(GameAssets.AttackFx);
        }

        private void Build(AttackFxArt fx)
        {
            float groundY = transform.position.y - transform.localScale.y * 0.5f;
            var center = new Vector3(transform.position.x, groundY + OpeningCenterHeight, 0f);
            float openW = OpeningSize.x, openH = OpeningSize.y;

            // 문 뒤로 번지는 빛무리 (문 그림 뒤, 윤곽 밖으로 은은하게)
            _haloBase = new Vector2(5.4f, 5.6f) * HaloScale;
            _halo = MakeGlow("PortalHalo", center, _haloBase, 8);
            // 문 안쪽 빛 (소용돌이 위에 살짝)
            _glowBase = new Vector2(openW * 1.5f, openH * 1.2f);
            _glow = MakeGlow("PortalGlow", center, _glowBase, 11);

            if (UseGatePortal && fx != null && fx.gatePortal.frames.Length >= 5)
            {
                // 회색판이 있으면 그걸 써서 방 종류 색이 그대로 나오게 (보라 그림에 색을 곱하면 탁해진다)
                var clip = fx.gatePortalGray != null && fx.gatePortalGray.frames.Length >= 5 ? fx.gatePortalGray : fx.gatePortal;
                BuildGatePortal(clip, center, openW, openH, groundY);
                return;
            }
            // 문 안쪽 일렁이는 소용돌이: 칸 192×224 중 내용(138×181)이 문 열린 부분(DoorWidth × DoorHeight)에 꼭 맞게 늘려 반복 재생
            if (fx != null && !fx.portalSwirl.IsEmpty)
            {
                var go = new GameObject("PortalSwirl");
                go.transform.SetParent(transform, true); // 문이 사라지면 같이 사라지게
                go.transform.position = center;
                Vector2 cell = fx.portalSwirl.frames[0].bounds.size;                       // 칸 크기(유닛)
                go.transform.localScale = new Vector3(openW / (cell.x * 138f / 192f), openH / (cell.y * 181f / 224f), 1f);
                // 부모(문 판정 상자)의 늘림을 상쇄해 월드 크기가 위 값이 되게 한다
                var parentScale = transform.localScale;
                go.transform.localScale = new Vector3(go.transform.localScale.x / parentScale.x, go.transform.localScale.y / parentScale.y, 1f);
                _swirlBase = go.transform.localScale;
                _swirl = go.AddComponent<SpriteRenderer>();
                _swirl.sortingOrder = 10;
                go.AddComponent<ClipPlayer>().Play(fx.portalSwirl);
            }
        }

        private void BuildGatePortal(SpriteClip clip, Vector3 center, float openW, float openH, float groundY)
        {
            var parentScale = transform.localScale;
            bool masked = HoleMask != null;
            if (masked)
            {
                // 구멍 모양 가림막: 틀 그림과 같은 자리(바닥 가운데)·같은 크기
                var maskGo = new GameObject("PortalHoleMask");
                maskGo.transform.SetParent(transform, true);
                maskGo.transform.position = new Vector3(transform.position.x, groundY, 0f);
                maskGo.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
                var mask = maskGo.AddComponent<SpriteMask>();
                mask.sprite = HoleMask;
                mask.alphaCutoff = 0.5f;
            }
            var go = new GameObject("PortalSwirl");
            go.transform.SetParent(transform, true);
            go.transform.position = center;
            Vector2 cell = clip.frames[0].bounds.size;
            // 가림막이 있으면 타원이 구멍(네모난 문도)을 빈틈없이 덮게 크게 그리고 구멍 밖은 잘라낸다.
            // 없으면 타원이 구멍보다 조금 크게 (구름 테두리가 틀 돌에 덮이게)
            float kx = masked ? 1.45f : 1.12f, ky = masked ? 1.25f : 1.08f;
            float sx = openW * kx / (cell.x * GateContentW / GateCell);
            float sy = openH * ky / (cell.y * GateContentH / GateCell);
            go.transform.localScale = new Vector3(sx / parentScale.x, sy / parentScale.y, 1f);
            _swirlBase = go.transform.localScale;
            _swirl = go.AddComponent<SpriteRenderer>();
            _swirl.sortingOrder = 8;      // 틀(9) 뒤 — 구멍으로 보인다
            if (masked) _swirl.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            _halo.sortingOrder = 7;
            var f = clip.frames;
            var open = new SpriteClip { frames = new[] { f[0], f[1] }, fps = clip.fps };
            var loop = new SpriteClip { frames = new[] { f[2], f[3], f[4], f[3] }, fps = clip.fps * 0.8f };
            StartCoroutine(PlayGatePortal(go.AddComponent<ClipPlayer>(), open, loop));
        }

        private System.Collections.IEnumerator PlayGatePortal(ClipPlayer player, SpriteClip open, SpriteClip loop)
        {
            player.Play(open);
            yield return new WaitForSeconds(open.frames.Length / Mathf.Max(0.1f, open.fps));
            player.Play(loop);
        }

        private SpriteRenderer MakeGlow(string name, Vector3 pos, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            var parentScale = transform.localScale;
            go.transform.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FxSprites.Glow;
            sr.sortingOrder = order;
            sr.color = new Color(Tint.r, Tint.g, Tint.b, 0f);
            return sr;
        }

        private void Update()
        {
            if (!HasEffects) return;
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

            SetColor(_halo, Mathf.Lerp(0.14f, 0.55f, _near) * (0.8f + 0.2f * pulse) + boost * 0.5f, Tint);
            _halo.transform.localScale = Scaled(_haloBase, 1f + 0.04f * pulse + 0.1f * _near);
            SetColor(_glow, Mathf.Lerp(0.06f, 0.4f, _near) * (0.7f + 0.3f * pulse) + boost, Tint);
            _glow.transform.localScale = Scaled(_glowBase, 1f + 0.06f * pulse);
            SetColor(_swirl, Mathf.Lerp(HoleMask != null ? 0.8f : 0.55f, 0.95f, _near) * (0.88f + 0.12f * Mathf.Sin(_time * 5.3f)), Color.Lerp(Color.white, Tint, SwirlTintAmount));
            // 프레임의 물결에 더해 전체가 부풀었다 가라앉고 좌우로 살랑이게 (가까울수록 조금 더 크게 일렁인다)
            float k = 1f + 0.5f * _near;
            float sx = 1f + 0.07f * k * Mathf.Sin(_time * 3.1f);
            float sy = 1f + 0.05f * k * Mathf.Sin(_time * 2.3f + 1f);
            _swirl.transform.localScale = new Vector3(_swirlBase.x * sx, _swirlBase.y * sy, 1f);
            var p = _swirl.transform.position;
            p.x = transform.position.x + 0.05f * k * Mathf.Sin(_time * 1.7f);
            _swirl.transform.position = p;
        }

        private Vector3 Scaled(Vector2 baseSize, float k)
        {
            var parent = transform.localScale;
            return new Vector3(baseSize.x * k / parent.x, baseSize.y * k / parent.y, 1f);
        }

        private static void SetColor(SpriteRenderer sr, float alpha, Color rgb)
        {
            sr.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(alpha));
        }
    }
}
