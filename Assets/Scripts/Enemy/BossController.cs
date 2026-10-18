using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 보스(불가살이). 몸·공격은 <see cref="EnemyController"/>(근접, 슈퍼 아머)를 그대로 쓰고, 여기서는 보스만의 것을 맡는다:
    /// 화면 상단 보스 체력바(퍼센트) · 체력 50% 아래에서 변신(무적·멈춤 + 변신 동작) → 2페이즈(더 큰 모습·판정, 더 빠른 공격 박자, 강공격 조금 더 자주).
    /// 1페이즈에서는 체력이 1 아래로 내려가지 않아(<see cref="EnemyController.HpFloor"/>) 한 방에 2페이즈를 건너뛰고 죽지 않는다.
    /// </summary>
    /// <summary>보스 특수 패턴 (이펙트: make_bossfx.py 그림 → <see cref="AttackFxArt"/>.bossSpine/bossSlam/bossWave/bossDash/bossDashTrail, 판정: <see cref="EnemyAttack"/>).</summary>
    public enum BossPattern { SpineVolley, GroundSlam, Dash }

    [RequireComponent(typeof(EnemyController))]
    public class BossController : MonoBehaviour
    {
        [SerializeField] private string bossName = "불가살이";
        [SerializeField, Range(0.1f, 0.9f)] private float phase2Threshold = 0.5f;
        [SerializeField, Tooltip("2페이즈 그림 묶음")] private EnemyArt phase2Art;
        [SerializeField, Tooltip("변신 동작 (한 번 재생)")] private SpriteClip transformClip = new();
        [SerializeField, Tooltip("변신 연출 최소 길이(초) — 동작이 짧아도 이만큼은 멈춰서 보여 준다")] private float transformMinTime = 1.6f;

        [Header("2페이즈")]
        [SerializeField, Tooltip("2페이즈 몸(판정) 크기 — 그림이 커지는 만큼")] private Vector2 phase2Body = new(4.6f, 3.8f);
        [SerializeField, Tooltip("2페이즈 공격 박자 (예비 모션·회복·공격 간격 배율, 1보다 작을수록 빠름)")] private float phase2Tempo = 0.75f;
        [SerializeField] private float phase2HeavyChance = 0.45f;
        [SerializeField, Tooltip("2페이즈 일반 공격(코 휘두르기 — 한 번) 데미지. 1페이즈 3연타는 타격마다 프리팹 값")] private int phase2NormalDamage = 16;
        [SerializeField, Tooltip("2페이즈 베기: 공격 시작 거리 · 끝 닿는 거리(몸 중심 기준) · 높이 중심 · 일반 높이 · 강공격 높이")]
        private Vector2 phase2RangeReach = new(4.2f, 5.2f);
        [SerializeField] private Vector3 phase2Heights = new(1.3f, 2.6f, 3.4f);

        [Header("연속 패링 → 기절 (보스는 경직이 안 걸리는 대신)")]
        [SerializeField, Tooltip("보스 공격을 맞지 않고 이만큼 연속으로 패링하면 기절")] private int parryStreakToStun = 5;
        [SerializeField, Tooltip("연속 패링 기절 시간(초) — 기절 저항 무시")] private float parryStunTime = 2.5f;

        [Header("특수 패턴 — 일반 공격 사이사이에 (예비 동작 + 느낌표 → 패턴)")]
        [SerializeField, Tooltip("방에 들어오고 첫 특수 패턴까지(초)")] private float firstPatternDelay = 5f;
        [SerializeField, Tooltip("특수 패턴 사이 간격(초, 최소~최대, 2페이즈는 Tempo만큼 짧아짐)")] private Vector2 patternCooldown = new(7f, 10f);
        [SerializeField, Tooltip("특수 패턴 예비 동작 길이(초, Tempo 적용) — 느낌표가 노랑 → 빨강")] private float patternWindup = 1.0f;
        [SerializeField, Tooltip("특수 패턴 뒤 쉬는 시간(초, Tempo 적용) — 반격할 틈")] private float patternRecover = 0.7f;
        [Header("가시 발사 — 등의 가시를 부채꼴로 쏜다 (가시 하나하나 패링 가능)")]
        [SerializeField] private int spineCount = 5;
        [SerializeField, Tooltip("가시 사이 각도(도)")] private float spineSpread = 9f;
        [SerializeField] private float spineSpeed = 11f;
        [SerializeField] private float spineRange = 16f;
        [SerializeField] private int spineDamage = 8;
        [SerializeField, Tooltip("가시 판정 상자 — 그림을 날아가는 방향으로 돌리므로 정사각에 가깝게")] private Vector2 spineSize = new(0.5f, 0.5f);
        [Header("땅 찍기 — 앞뒤로 바닥을 타고 퍼지는 충격파 (점프로 넘거나 패링)")]
        [SerializeField] private int slamDamage = 14;
        [SerializeField] private float waveSpeed = 8f;
        [SerializeField] private float waveRange = 12f;
        [SerializeField] private Vector2 waveSize = new(0.8f, 1.0f);
        [Header("돌진 — 플레이어가 멀리 있으면 몸통 박치기로 달려온다 (패링하면 멈춘다)")]
        [SerializeField] private int dashDamage = 22;
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField, Tooltip("최대 돌진 거리")] private float dashDistance = 16f;
        [SerializeField, Tooltip("플레이어가 있던 자리에서 이만큼 더 달린 뒤 멈춘다")] private float dashOvershoot = 2.5f;
        [SerializeField, Tooltip("이 거리(유닛) 이상 떨어져 있으면 '멀다'")] private float dashTriggerDistance = 8f;
        [SerializeField, Tooltip("이만큼(초) 계속 멀리 있으면 특수 패턴 간격과 상관없이 돌진")] private float dashFarTime = 1.2f;
        [SerializeField, Tooltip("멀어서 하는 돌진 사이 최소 간격(초, Tempo 적용)")] private float dashCooldown = 5f;

        private static readonly Color PatternColor = new(1f, 0.5f, 0.15f, 0.9f); // 용암빛 주황 (빨강은 강공격 경고색)

        public static BossController Instance { get; private set; }
        /// <summary>보스 공격을 맞지 않고 연속으로 패링한 횟수 (맞으면 0으로).</summary>
        public int ParryStreak { get; private set; }
        public int ParryStreakToStun => parryStreakToStun;
        public int ParryStunCount { get; private set; }
        private PlayerCombat _combat;
        private PlayerParty _party;
        public bool IsUsingPattern { get; private set; }
        public int PatternCount { get; private set; }
        public BossPattern LastPattern { get; private set; }
        private float _patternTimer;
        private float _farTime;      // 플레이어가 계속 멀리 있었던 시간
        private float _dashTimer;    // 멀어서 하는 돌진 쿨타임
        private Coroutine _patternRoutine;
        private Transform _player;
        public int Phase { get; private set; } = 1;
        public bool IsTransforming { get; private set; }
        public string BossName => bossName;
        public EnemyController Enemy => _enemy;

        private EnemyController _enemy;
        private EnemySprite _sprite;

        private void Awake()
        {
            Instance = this;
            _enemy = GetComponent<EnemyController>();
            _sprite = GetComponent<EnemySprite>();
            _enemy.HpFloor = 1; // 1페이즈는 죽지 않고 반드시 변신한다
        }

        private void Start()
        {
            _combat = FindAnyObjectByType<PlayerCombat>();
            _party = FindAnyObjectByType<PlayerParty>();
            if (_combat != null) _combat.DefenseSucceeded += OnParried;
            if (_party != null) _party.Damaged += OnPlayerHurt;
            _patternTimer = firstPatternDelay;
            _dashTimer = dashFarTime; // 방에 들어오자마자 달려들지는 않게
            var defense = FindAnyObjectByType<PlayerDefense>();
            if (defense != null) _player = defense.transform;
            _enemy.Died += OnDied;
            _enemy.ComboPreparing += OnComboPreparing;
            if (PlayEntrance) StartCoroutine(EntranceRoutine());
            else ShowBar();
        }

        private void ShowBar()
        {
            Hud.ShowBossBar(bossName, () => _enemy != null ? _enemy.HpRatio : 0f, () => Phase, ParryStreakText);
            Hud.Popup(bossName, new Color(1f, 0.6f, 0.25f), true);
        }

        /// <summary>등장 연출을 할지 (테스트는 끄고 바로 싸운다 — 등장 테스트만 켠다).</summary>
        public static bool PlayEntrance = true;
        public bool IsEntering { get; private set; }

        [Header("등장 — 하늘에서 떨어져 땅을 찍고 포효")]
        [SerializeField, Tooltip("떨어지기 전 땅이 달아오르는 경고(초)")] private float entranceWarn = 0.7f;
        [SerializeField, Tooltip("떨어지는 높이(유닛)")] private float entranceHeight = 10f;
        [SerializeField, Tooltip("떨어지는 시간(초)")] private float entranceFall = 0.35f;
        [SerializeField, Tooltip("착지 뒤 포효(초) — 이 동안 무적·멈춤")] private float entranceRoar = 1.3f;

        /// <summary>
        /// 등장: 내려앉을 자리의 땅이 달아오르며 흔들림 → 그림이 하늘에서 떨어져 → 착지 충격(땅 찍기 이펙트·흔들림·섬광) → 포효(강공격 준비 동작) + 이름·체력바.
        /// 판정 몸은 처음부터 땅에 있고(무적·멈춤) 그림만 위에서 내려온다 — 몸을 옮기면 발 높이 계산이 어긋난다.
        /// </summary>
        private IEnumerator EntranceRoutine()
        {
            IsEntering = true;
            _enemy.Frozen = true;
            _enemy.Invulnerable = true;
            var art = _sprite != null ? _sprite.Art : null;
            var artTf = _sprite != null && _sprite.ArtRenderer != null ? _sprite.ArtRenderer.transform : null;
            Vector3 artBase = artTf != null ? artTf.localPosition : Vector3.zero;
            void Lift(float h)
            {
                if (artTf == null) return;
                artTf.position = transform.TransformPoint(artBase) + Vector3.up * h;
            }
            Lift(entranceHeight + 6f); // 경고 동안은 화면 위 밖에서 기다린다
            var feet = new Vector2(transform.position.x, _enemy.FeetY);

            // 1) 경고: 착지할 땅이 달아오르고 땅이 울린다
            var glow = new GameObject("BossEntranceGlow").AddComponent<SpriteRenderer>();
            glow.sprite = FxSprites.Glow;
            glow.sortingOrder = 3;
            glow.transform.position = feet;
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.15f, entranceWarn);
            Sfx.Play(SfxId.HeavyCue, 0.7f);
            for (float t = 0f; t < entranceWarn; t += Time.deltaTime)
            {
                Lift(entranceHeight + 6f);
                float k = t / entranceWarn;
                glow.color = new Color(1f, 0.5f, 0.12f, 0.45f + 0.45f * k + 0.1f * Mathf.Sin(t * 30f));
                glow.transform.localScale = new Vector3(Half * 4f * (0.6f + 0.4f * k), 1.4f, 1f);
                if (Random.value < 0.35f) // 달아오른 땅에서 튀는 불티
                    Fx.Flash(feet + new Vector2(Random.Range(-Half * 1.6f, Half * 1.6f), Random.Range(0.05f, 0.5f)), 0.18f, 0.02f, new Color(1f, 0.7f, 0.25f), 0.3f);
                yield return null;
            }

            // 2) 하늘에서 떨어진다 (가속)
            if (_sprite != null && art != null) _sprite.PlayLoop(new SpriteClip { frames = art.idle.frames, fps = 1f });
            for (float t = 0f; t < entranceFall; t += Time.deltaTime)
            {
                float k = t / entranceFall;
                Lift(entranceHeight * (1f - k * k));
                yield return null;
            }
            Lift(0f);
            Destroy(glow.gameObject);

            // 3) 착지 충격
            FxClip.PlayAt(GameAssets.AttackFx != null ? GameAssets.AttackFx.bossSlam : null, feet + Vector2.up * (0.8f * FxScale), FxScale * 1.3f);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.55f, 0.5f);
            Hud.ScreenFlash(new Color(1f, 0.55f, 0.2f, 0.4f));
            Sfx.Play(SfxId.BossLand);

            // 4) 포효 + 이름·체력바
            if (_sprite != null && art != null) _sprite.PlayOnce(art.heavyWindup);
            Sfx.Play(SfxId.BossRoar);
            ShowBar();
            yield return new WaitForSeconds(entranceRoar);
            if (_sprite != null) _sprite.StopOverride();

            _enemy.Frozen = false;
            _enemy.Invulnerable = false;
            IsEntering = false;
            _patternTimer = firstPatternDelay;
            _dashTimer = dashFarTime;
        }

        /// <summary>3연타는 드물게 나오고, 나오기 전에 알린다 (그 뒤 웅크림 → 느낌표마다 패링).</summary>
        private void OnComboPreparing()
        {
            Hud.Popup($"{bossName}가 연속 공격을 준비합니다!", new Color(1f, 0.75f, 0.3f), true);
            Sfx.Play(SfxId.HeavyCue, 0.5f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_combat != null) _combat.DefenseSucceeded -= OnParried;
            if (_party != null) _party.Damaged -= OnPlayerHurt;
        }

        // 체력바에 연속 패링 수: "연속 패링 3 / 5"
        private string ParryStreakText()
        {
            if (ParryStreak <= 0) return "";
            return $"<color=#FFFFFF>연속 패링 <b>{ParryStreak}</b> / {parryStreakToStun}</color>";
        }

        /// <summary>보스 공격(일반·3연타 한 타·강공격·가시·충격파·돌진)을 패링할 때마다 +1. 다 차면 기절 (저항 무시).</summary>
        private int _volleyCounter;     // 가시·충격파 한 번 = 묶음 번호 하나
        private int _lastCountedVolley; // 연속 패링에 이미 센 묶음

        private void OnParried(DefenseSuccess success)
        {
            if (success.Attack == null || success.Attack.Source != _enemy || _enemy.IsDead) return;
            // 가시 5개를 한꺼번에 막아도 한 번으로 센다 (안 그러면 가시 패턴 한 번에 5연속이 다 찬다 — 사용자 지적)
            int volley = success.Attack.Volley;
            if (volley != 0)
            {
                if (volley == _lastCountedVolley) return;
                _lastCountedVolley = volley;
            }
            ParryStreak++;
            if (ParryStreak < parryStreakToStun) return;
            ParryStreak = 0;
            if (IsTransforming) return; // 변신 중엔 무적·멈춤이라 기절은 건너뛴다
            StopPattern();
            _enemy.Frozen = false;
            _enemy.Stun(parryStunTime, ignoreResist: true);
            ParryStunCount++;
            StartCoroutine(StunAnnounce()); // 같은 순간 뜨는 "패링 성공!" 팝업에 덮이지 않게 한 박자 뒤에
            Hud.ScreenFlash(new Color(0.6f, 0.85f, 1f, 0.35f));
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.3f, 0.3f);
            Fx.Ring(transform.position, new Color(0.6f, 0.85f, 1f), 1f, 6f, 0.15f, 0.5f);
        }

        private IEnumerator StunAnnounce()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            Hud.Popup($"{bossName} 기절!  ({parryStreakToStun}연속 패링)", new Color(0.6f, 0.85f, 1f), true);
            Hud.WorldText(transform.position + Vector3.up * (transform.localScale.y * 0.5f + 1.2f), "기절!", new Color(0.6f, 0.85f, 1f), 1.6f);
        }

        /// <summary>플레이어가 맞으면(추락 포함) 연속 패링은 처음부터.</summary>
        private void OnPlayerHurt(int damage) => ParryStreak = 0;

        private void OnDied(EnemyController _)
        {
            Hud.HideBossBar();
            Hud.Popup($"{bossName}를 쓰러뜨렸다!", new Color(1f, 0.8f, 0.4f), true);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.5f, 0.6f);
        }

        private void Update()
        {
            if (Phase == 1 && !IsTransforming && !_enemy.IsDead && _enemy.HpRatio <= phase2Threshold)
            {
                StopPattern();
                StartCoroutine(TransformRoutine());
            }

            if (IsEntering) return;
            // 특수 패턴: 쿨타임이 차고 보스가 한가하면(공격·경직·기절 중이 아님) 거리에 맞는 패턴을 고른다
            if (IsTransforming || IsUsingPattern || _enemy.IsDead || _player == null || GameManager.InputBlocked || GameTuning.Current.enemyPassive) return;
            _patternTimer -= Time.deltaTime;
            _dashTimer -= Time.deltaTime;
            float dist = Mathf.Abs(_player.position.x - transform.position.x);
            _farTime = dist >= dashTriggerDistance ? _farTime + Time.deltaTime : 0f;
            // 멀리서 버티면(원거리 견제·도망) 패턴 간격과 상관없이 돌진해 거리를 좁힌다
            if (_farTime >= dashFarTime && _dashTimer <= 0f && !_enemy.IsBusy)
            {
                StartPattern(BossPattern.Dash);
                return;
            }
            if (_patternTimer <= 0f && !_enemy.IsBusy) StartPattern(ChoosePattern());
        }

        private BossPattern ChoosePattern()
        {
            float dist = Mathf.Abs(_player.position.x - transform.position.x);
            if (dist >= 6f && Random.value < 0.5f) return BossPattern.Dash;
            if (dist >= 6f) return BossPattern.SpineVolley;
            return Random.value < 0.7f ? BossPattern.GroundSlam : BossPattern.SpineVolley;
        }

        /// <summary>특수 패턴을 바로 시작한다 (테스트도 부른다). 이미 패턴·변신 중이면 무시.</summary>
        public void StartPattern(BossPattern pattern)
        {
            if (IsUsingPattern || IsTransforming || _enemy.IsDead) return;
            if (_player == null)
            {
                var defense = FindAnyObjectByType<PlayerDefense>();
                if (defense != null) _player = defense.transform;
            }
            _enemy.CancelAttack();
            _patternRoutine = StartCoroutine(PatternRoutine(pattern));
        }

        private void StopPattern()
        {
            if (_patternRoutine != null) StopCoroutine(_patternRoutine);
            _patternRoutine = null;
            if (IsUsingPattern && _sprite != null) _sprite.StopOverride();
            IsUsingPattern = false;
        }

        private IEnumerator PatternRoutine(BossPattern pattern)
        {
            IsUsingPattern = true;
            LastPattern = pattern;
            _enemy.Frozen = true;
            if (_player != null) _enemy.FaceTowardsX(_player.position.x);
            float dir = _enemy.Facing;
            float tempo = Mathf.Max(0.1f, _enemy.Tempo);
            var art = _sprite != null ? _sprite.Art : null;

            // 예비 동작: 강공격 준비 모습 + 노란 느낌표 → 패턴이 나가기 직전 빨강 (패링 판정 창이 덮도록 창의 80% 앞)
            float windup = patternWindup * tempo * (pattern == BossPattern.Dash ? 0.9f : 1f);
            if (_sprite != null && art != null) _sprite.PlayOnce(art.heavyWindup);
            var cue = PressCueIcon.Spawn(_enemy, manual: true);
            Sfx.Play(SfxId.HeavyCue);
            float redAt = windup - GameTuning.Current.defenseActiveTime * 0.8f;
            bool red = false;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                if (!red && t >= redAt) { red = true; _enemy.TurnCueRed(cue); }
                yield return null;
            }
            if (!red) _enemy.TurnCueRed(cue);

            switch (pattern)
            {
                case BossPattern.SpineVolley: FireSpines(dir, art); break;
                case BossPattern.GroundSlam: Slam(dir, art); break;
                case BossPattern.Dash: yield return Dash(dir, art); break;
            }
            cue.FadeOut();

            yield return new WaitForSeconds(patternRecover * tempo);
            if (_sprite != null) _sprite.StopOverride();
            _enemy.Frozen = false;
            IsUsingPattern = false;
            PatternCount++;
            _patternTimer = Random.Range(patternCooldown.x, Mathf.Max(patternCooldown.x, patternCooldown.y)) * tempo;
            _farTime = 0f;
            if (pattern == BossPattern.Dash) _dashTimer = dashCooldown * tempo;
            _patternRoutine = null;
        }

        private float Half => transform.localScale.x * 0.5f;
        private float BodyHeight => transform.localScale.y;
        /// <summary>이펙트 그림 배율: 그림은 1페이즈 몸 높이(3유닛) 기준으로 그렸다 — 2페이즈(4)에선 그만큼 크게.</summary>
        private float FxScale => BodyHeight / 3f;

        /// <summary>가시 발사: 입 앞에서 플레이어 쪽으로 부채꼴 (고정 속도 투사체 — 하나하나 패링할 수 있다).</summary>
        private void FireSpines(float dir, EnemyArt art)
        {
            if (_sprite != null && art != null) _sprite.PlayOnce(art.heavySlash);
            var origin = new Vector2(transform.position.x + dir * Half * 0.9f, _enemy.FeetY + BodyHeight * 0.55f);
            Vector2 aim = _player != null ? ((Vector2)_player.position - origin).normalized : new Vector2(dir, 0f);
            if (Mathf.Sign(aim.x) != Mathf.Sign(dir)) aim = new Vector2(dir, 0f); // 뒤로는 쏘지 않는다
            var fx = GameAssets.AttackFx;
            int volley = ++_volleyCounter;
            for (int i = 0; i < spineCount; i++)
            {
                float angle = (i - (spineCount - 1) * 0.5f) * spineSpread;
                Vector2 d = Quaternion.Euler(0f, 0f, angle) * aim;
                var spine = EnemyAttack.Spawn(_enemy, AttackType.Normal, origin, d, spineSize, spineSpeed, spineRange, PatternColor, spineDamage);
                spine.Volley = volley;
                if (fx != null) spine.AttachProjectileArt(fx.bossSpine, 0, 6, 1f, rotate: true); // 날아가는 가시 그림 (방향으로 돌림)
            }
            Sfx.Play(SfxId.SpineShot);
        }

        /// <summary>땅 찍기: 앞발로 내리쳐 바닥을 타고 앞뒤로 충격파가 퍼진다. 플레이어 쪽 파도는 강공격(패링하면 도력 +3).</summary>
        private void Slam(float dir, EnemyArt art)
        {
            if (_sprite != null && art != null) _sprite.PlayOnce(art.heavySlash);
            var hit = new Vector2(transform.position.x + dir * Half, _enemy.FeetY);
            // 찍은 자리 충격·균열·파편 (그림의 바닥선이 가운데에서 0.8유닛 아래 — 몸 크기에 맞춰 키운다)
            FxClip.PlayAt(GameAssets.AttackFx != null ? GameAssets.AttackFx.bossSlam : null, hit + Vector2.up * (0.8f * FxScale), FxScale);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.35f, 0.35f);
            Sfx.Play(SfxId.BossSlam);
            int volley = ++_volleyCounter;
            foreach (float d in new[] { dir, -dir })
            {
                var start = new Vector2(transform.position.x + d * (Half + 0.4f), _enemy.FeetY + waveSize.y * 0.5f);
                var wave = EnemyAttack.Spawn(_enemy, d == dir ? AttackType.Heavy : AttackType.Normal, start, new Vector2(d, 0f), waveSize, waveSpeed, waveRange, PatternColor, slamDamage);
                wave.Volley = volley;
                // 바닥을 뚫고 솟는 쇠 가시 파도 (그림의 바닥선 = 판정 상자 아랫면) — 그림이 붙으면 판정 네모는 숨겨진다
                if (GameAssets.AttackFx != null) wave.AttachProjectileArt(GameAssets.AttackFx.bossWave, 0, 6, 1f);
            }
        }

        /// <summary>돌진: 몸통 앞에 박치기 판정을 두고 그대로 달려간다. 판정이 부서지면(패링) 그 자리에서 멈춘다. 벽에 닿아도 멈춘다.</summary>
        private IEnumerator Dash(float dir, EnemyArt art)
        {
            if (_sprite != null && art != null && !art.walk.IsEmpty)
                _sprite.PlayLoop(new SpriteClip { frames = art.walk.frames, fps = 16f });
            var size = new Vector2(1.2f, BodyHeight * 0.8f);
            var front = new Vector2(transform.position.x + dir * (Half + size.x * 0.5f), _enemy.FeetY + size.y * 0.5f);
            // 플레이어가 서 있던 자리를 조금 지나서 멈춘다 (피하면 방 끝까지 미끄러지지 않게)
            float reach = dashDistance;
            if (_player != null) reach = Mathf.Clamp(Mathf.Abs(_player.position.x - front.x) + dashOvershoot, 2f, dashDistance);
            var hitbox = EnemyAttack.Spawn(_enemy, AttackType.Heavy, front, new Vector2(dir, 0f), size, dashSpeed, reach, PatternColor, dashDamage);
            if (GameAssets.AttackFx != null) hitbox.AttachProjectileArt(GameAssets.AttackFx.bossDash, 0, 4, FxScale); // 박치기 판정에 앞 압력파 그림
            DashTrail(dir);
            Sfx.Play(SfxId.BossDash);
            float streak = 0f;
            while (hitbox != null && !_enemy.IsDead)
            {
                float want = hitbox.transform.position.x - dir * (Half + size.x * 0.5f);
                float got = _enemy.MoveToX(want);
                if (Mathf.Abs(got - want) > 0.05f) // 벽에 닿았다
                {
                    Destroy(hitbox.gameObject);
                    break;
                }
                streak += Time.deltaTime;
                if (streak >= 0.12f)
                {
                    streak = 0f;
                    DashTrail(dir);
                }
                yield return null;
            }
            if (_sprite != null) _sprite.StopOverride();
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.2f, 0.25f);
        }

        /// <summary>돌진 잔상·속도선: 몸 뒤쪽에 한 장면씩 남긴다 (몸을 따라가지 않고 그 자리에서 사라져 속도감이 난다).</summary>
        private void DashTrail(float dir)
        {
            var at = new Vector2(transform.position.x - dir * Half * 0.4f, _enemy.FeetY + BodyHeight * 0.45f);
            FxClip.PlayAt(GameAssets.AttackFx != null ? GameAssets.AttackFx.bossDashTrail : null, at, FxScale, dir < 0f, 6); // 보스 그림(7) 뒤 — 지나간 자리에 남는 잔상
        }

        /// <summary>2페이즈로 변신 (테스트도 부른다 — 체력과 상관없이).</summary>
        public void ForcePhase2()
        {
            if (Phase == 1 && !IsTransforming) StartCoroutine(TransformRoutine());
        }

        private IEnumerator TransformRoutine()
        {
            StopPattern(); // 변신이 하던 특수 패턴보다 앞선다
            IsTransforming = true;
            _enemy.CancelAttack();
            _enemy.Frozen = true;
            _enemy.Invulnerable = true;
            Hud.Popup($"{bossName}가 깨어난다 — 2페이즈", new Color(1f, 0.55f, 0.2f), true);
            Hud.ScreenFlash(new Color(1f, 0.5f, 0.15f, 0.45f));
            Sfx.Play(SfxId.BossRoar); // 깨어나며 포효
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.35f, 0.8f);
            Fx.Ring(transform.position, new Color(1f, 0.55f, 0.2f), 1f, 7f, 0.15f, 0.6f);

            float length = _sprite != null ? _sprite.PlayOnce(transformClip) : 0f;
            float wait = Mathf.Max(transformMinTime, length);
            // 동작의 절반쯤에서 몸이 커진다 (판정도 함께)
            yield return new WaitForSeconds(wait * 0.5f);
            ApplyPhase2Body();
            Hud.ScreenFlash(new Color(1f, 0.65f, 0.3f, 0.55f));
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.5f, 0.5f);
            Fx.Ring(transform.position, new Color(1f, 0.7f, 0.3f), 1.5f, 9f, 0.15f, 0.5f);
            yield return new WaitForSeconds(wait * 0.5f);

            Phase = 2;
            _enemy.HpFloor = 0;
            _enemy.Tempo = phase2Tempo;
            _enemy.HeavyChanceOverride = phase2HeavyChance;
            _enemy.ComboHits = 1;                    // 2페이즈 일반 공격은 코 휘두르기 한 번 (대신 더 빠르고 강하다)
            _enemy.NormalDamage = phase2NormalDamage;
            _enemy.Invulnerable = false;
            _enemy.Frozen = false;
            IsTransforming = false;
        }

        private void ApplyPhase2Body()
        {
            _enemy.SetBodySize(phase2Body);
            _enemy.SetMeleeGeometry(phase2RangeReach.x, phase2RangeReach.y, phase2Heights.x, phase2Heights.y, phase2Heights.z);
            if (_sprite != null)
            {
                if (phase2Art != null) _sprite.SetArt(phase2Art);
                // 그림은 판정 상자의 자식이라 부모가 커진 만큼 줄여서 픽셀 비율을 유지한다
                if (_sprite.ArtRenderer != null)
                    _sprite.ArtRenderer.transform.localScale = new Vector3(1f / phase2Body.x, 1f / phase2Body.y, 1f);
            }
        }
    }
}
