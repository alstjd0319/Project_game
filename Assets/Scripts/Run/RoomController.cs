using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 플레이 씬에 하나 있는 방 진행 관리. <see cref="RunState.Active"/>가 아니면 아무것도 하지 않고(예전 연습 모드 그대로),
    /// 켜져 있으면 방 종류에 맞는 몬스터 웨이브를 세우고, 다 깨면 다음 방으로 가는 문을 연다 (스컬 방식).
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        public enum Phase { Intro, Fighting, BetweenWaves, Cleared }

        [SerializeField, Tooltip("문 그림 (바닥 가운데 피벗). 포탈 틀(portalArt)이 없을 때만, 그것도 없으면 색 네모")] private Sprite gateSprite;
        [SerializeField, Tooltip("방 종류별 포탈 틀 7종 (1016 에셋)")] private PortalArt portalArt;
        [SerializeField] private float introDelay = 1.2f;
        [SerializeField] private float betweenWaves = 1.6f;
        [SerializeField, Tooltip("다음 방 문은 오른쪽 끝에 나란히 선다: 가장 바깥 문이 바닥 오른쪽 끝에서 안쪽으로 떨어진 거리")] private float gateInset = 1.8f;
        [SerializeField, Tooltip("문들 사이 간격 (문 글자가 겹치지 않게)")] private float gateSpacing = 4.2f;
        [SerializeField, Tooltip("방에 들어오면 플레이어는 바닥 왼쪽 끝에서 이만큼 안쪽에서 시작한다 (문은 오른쪽 끝)")] private float startInset = 2f;

        public static RoomController Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Intro;
        public int WaveIndex { get; private set; }
        public int WaveCount => _waves.Count;
        /// <summary>이 방의 전투 장면 (보스·보상·상점·연습 모드는 null).</summary>
        public Encounter Encounter { get; private set; }
        /// <summary>지금 웨이브에서 아직 나오지 않은(늦게 나오는) 몬스터 수.</summary>
        public int PendingSpawns => _pending.Count;
        public IReadOnlyList<RoomGate> Gates => _gates;
        public PortalArt PortalArt => portalArt;
        public IReadOnlyList<RoomAltar> Altars => _altars;
        public bool IsRunRoom { get; private set; }

        private EnemySpawner _spawner;
        private List<SpawnSpec[]> _waves = new();
        private readonly List<SpawnSpec> _pending = new();
        private float _waveTime;
        private readonly List<EnemyController> _alive = new();
        private readonly List<RoomGate> _gates = new();
        private readonly List<RoomAltar> _altars = new();
        private bool _gatesOpen;
        private const int GoldMelee = 5, GoldRanged = 7, GoldElite = 25, GoldBoss = 60;
        private float _timer;

        public int LayoutIndex { get; private set; }
        /// <summary>이 방의 지형 (방 길이 배율을 적용한 것).</summary>
        public RoomLayout Layout { get; private set; }
        /// <summary>방 길이 배율: 싸우는 방은 구간이 깊을수록 길다 (1구간 1 · 2구간 1.3 · 3구간 1.6), 그 밖은 1.</summary>
        public float ArenaScale { get; private set; } = 1f;

        private void Awake()
        {
            Instance = this;
            IsRunRoom = RunState.Active;
            // 싸우는 방(일반·엘리트)은 전투 장면을 먼저 고르고, 지형은 그 장면에 어울리는 것 중에서 (예: 높이 뜬 도깨비불 → 발판 있는 지형)
            bool fightRoom = IsRunRoom && !RunState.IsFinalRoom && (RunState.Current == RoomType.Normal || RunState.Current == RoomType.Elite);
            if (fightRoom)
            {
                Encounter = EncounterCatalog.Pick(RunState.Current, RunState.RoomNumber, RunState.Zone, RunState.Seed, RunState.LastEncounter);
                RunState.LastEncounter = Encounter.Id;
                LayoutIndex = EncounterCatalog.PickLayout(Encounter, RunState.Seed, RunState.RoomNumber, RunState.LastLayout);
            }
            // 그 밖의 방(지형): 연습 모드는 기본, 보상·상점·보스는 평지 (바닥 그림 데코레이터가 첫 프레임에 그 배치대로 깐다)
            else LayoutIndex = RoomLayouts.Pick(IsRunRoom, RunState.Current, RunState.RoomNumber, RunState.Seed, RunState.LastLayout);
            if (IsRunRoom) RunState.LastLayout = LayoutIndex;
            ArenaScale = fightRoom ? RoomLayouts.ArenaScaleFor(RunState.Zone) : 1f;
            Layout = RoomLayouts.Scaled(RoomLayouts.All[LayoutIndex], ArenaScale);
            float arenaHalf = RoomLayouts.BaseArenaHalf * ArenaScale;
            var deco = FindAnyObjectByType<FloorDecorator>();
            if (deco != null)
            {
                deco.SetPlatformLayout(Layout.Platforms);
                deco.SetGroundIntervals(RoomLayouts.GroundIntervals(Layout, RoomLayouts.GroundHalfExtent + arenaHalf - RoomLayouts.BaseArenaHalf));
                if (ArenaScale != 1f) deco.SetWalls(arenaHalf + 1f);
            }
            if (ArenaScale != 1f)
            {
                // 길어진 방: 몬스터 범위 · 카메라 끝(= 벽 안쪽 면)을 함께 넓힌다 (문·시작 자리는 스포너 범위를 따른다)
                var spawner = FindAnyObjectByType<EnemySpawner>();
                if (spawner != null) spawner.SetArena(-arenaHalf, arenaHalf);
                var rig = FindAnyObjectByType<CameraRig>();
                if (rig != null) rig.SetArena(-arenaHalf - 1f, arenaHalf + 1f);
            }
            BuildPits();
            if (!IsRunRoom) return;
            _spawner = FindAnyObjectByType<EnemySpawner>();
            if (_spawner != null) _spawner.RoomMode = true; // 스포너 Start보다 먼저 — 몬스터를 계속 유지하는 연습 모드를 끈다
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EnemyController.AnyDied -= OnEnemyDied;
            RunState.Changed -= UpdateInfo;
        }

        /// <summary>몬스터를 잡으면 엽전을 얻는다 (엘리트가 훨씬 많다).</summary>
        private void OnEnemyDied(EnemyController enemy)
        {
            int gold = enemy.GetComponent<BossController>() != null ? GoldBoss : enemy.IsElite ? GoldElite : enemy.Kind == EnemyKind.Ranged ? GoldRanged : GoldMelee;
            RunState.AddGold(gold);
            Hud.WorldText(enemy.transform.position + Vector3.up * 0.8f, $"+{gold} 엽전", new Color(1f, 0.85f, 0.35f), 1f);
            RewardFx.Burst(RewardFx.Kind.Coin, enemy.transform.position, gold >= GoldElite ? 4 : gold >= GoldRanged ? 2 : 1); // 엽전이 톡 튀어 반짝 사라진다
        }

        private void Start()
        {
            if (!IsRunRoom) return;
            _waves = Encounter != null ? new List<SpawnSpec[]>(Encounter.Waves) : RoomPlan.Specs(RunState.Current, RunState.RoomNumber);
            _timer = introDelay;
            EnemyController.AnyDied += OnEnemyDied;
            RunState.Changed += UpdateInfo;
            if (RunState.Current == RoomType.Boss)
            {
                var bg = FindAnyObjectByType<ParallaxBackground>();
                if (bg != null) bg.UseBossBackground(); // 쇠사슬 감긴 거대한 문 — 불가살이 보스방 전용 배경
            }
            else if (RunState.Current == RoomType.Reward)
            {
                var bg = FindAnyObjectByType<ParallaxBackground>();
                if (bg != null) bg.UseRewardBackground(); // 달빛 드는 낡은 사당 기둥 — 보상방 전용 배경 (1016 에셋)
            }
            else if (RunState.Current == RoomType.Shop)
            {
                var bg = FindAnyObjectByType<ParallaxBackground>();
                if (bg != null) bg.UseShopBackground(); // 소라 껍데기 동굴 장터 — 상점방 전용 배경 (1016 에셋)
            }
            if (RunState.Current == RoomType.Boss && CameraRig.Instance != null)
                CameraRig.Instance.SetView(BossViewSize, BossViewSize - 2.5f); // 보스방은 넓게 — 큰 보스(2페이즈)가 화면에 다 들어오게. 바닥은 같은 높이(아래 끝 y=−2.5)
            PlacePlayerAtStart();
            StartCoroutine(PortalTransition.Arrive()); // 포탈에서 나오는 연출 (빛이 감겨 몸이 된다)
            UpdateInfo();
            string scene = Encounter != null ? $" — {Encounter.Name}" : "";
            string zone = RunState.Current == RoomType.Boss ? "" : $"{RunState.Zone}구간 · ";
            Hud.Popup($"{zone}방 {RunState.RoomNumber} · {RoomInfo.Label(RunState.Current)}{scene}", RoomInfo.Color(RunState.Current), true);
        }

        /// <summary>방에 들어오면 플레이어는 가장 왼쪽에서 시작한다 (문은 오른쪽 끝 — 왼쪽에서 오른쪽으로 나아가는 흐름). 카메라도 바로 따라간다.</summary>
        private void PlacePlayerAtStart()
        {
            var party = FindAnyObjectByType<PlayerParty>();
            if (party == null) return;
            float left = _spawner != null ? _spawner.ArenaMinX : -22f;
            var pos = new Vector3(left + startInset, party.transform.position.y, 0f);
            party.transform.position = pos;
            var rb = party.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = pos;
                rb.linearVelocity = Vector2.zero;
            }
            Physics2D.SyncTransforms();
            if (CameraRig.Instance != null) CameraRig.Instance.SnapToTarget();
        }

        private void UpdateInfo()
        {
            string text = $"{RunState.Zone}구간 · 방 {RunState.RoomNumber}/{RunState.TotalRooms} · {RoomInfo.Label(RunState.Current)}";
            Hud.SetCurrency(RunState.Gold, RunState.SoulStones);
            if (RunState.Items.Count > 0)
            {
                var counts = new List<string>();
                var seen = new HashSet<string>();
                foreach (var item in RunState.Items)
                {
                    if (!seen.Add(item.Id)) continue;
                    int n = 0;
                    foreach (var other in RunState.Items) if (other.Id == item.Id) n++;
                    counts.Add(n > 1 ? $"{item.Name} ×{n}" : item.Name);
                }
                text += $"\n<size=22><color=#E8D9A0>아이템  {string.Join(" · ", counts)}</color></size>";
            }
            Hud.SetRunInfo(text);
        }

        // ── 바닥 구멍: 빠지면 피해를 입고 떨어지기 전 자리로 돌아온다 ──
        public const int FallDamage = 15;
        /// <summary>보스방 카메라 크기 (평소 6). 화면 아래 끝은 y=−2.5로 같게 둔다.</summary>
        public const float BossViewSize = 7.2f;

        /// <summary>보스방에서 플레이어가 시작 자리보다 이만큼 앞으로 나오면 보스가 등장한다.</summary>
        public const float BossTriggerAdvance = 4f;
        /// <summary>보스는 플레이어 앞 이만큼에 내려앉는다 (보스방 카메라 반폭 약 12.8 — 몸까지 화면 안에).</summary>
        public const float BossAppearAhead = 8.5f;
        /// <summary>테스트용: false면 보스가 플레이어를 기다리지 않고 바로 나온다.</summary>
        public static bool BossWaitsForPlayer = true;

        private float StartX => (_spawner != null ? _spawner.ArenaMinX : -22f) + startInset;

        private bool WaitingForBossTrigger
        {
            get
            {
                if (!BossWaitsForPlayer || _waves.Count == 0 || _waves[0].Length == 0 || !_waves[0][0].Boss) return false;
                var party = FindAnyObjectByType<PlayerParty>();
                return party != null && party.transform.position.x < StartX + BossTriggerAdvance;
            }
        }

        private float? BossAppearX()
        {
            var party = FindAnyObjectByType<PlayerParty>();
            if (party == null || !BossWaitsForPlayer) return null;
            return party.transform.position.x + BossAppearAhead;
        }
        private const float FallKillY = -4.5f;    // 이보다 아래로 떨어지면 추락
        private const float SafeEdgeMargin = 1.2f; // 구멍 가장자리에서 이만큼 안쪽에서 선 자리만 "안전한 자리"로 기억한다

        public int FallCount { get; private set; }
        /// <summary>마지막으로 안전하게 서 있던 자리 (추락하면 여기로 돌아온다).</summary>
        public Vector2 SafePosition => _safePosition;
        private Vector2 _safePosition;
        private PlayerParty _fallParty;
        private PlayerMotor _fallMotor;

        private void BuildPits()
        {
            _fallParty = FindAnyObjectByType<PlayerParty>();
            _fallMotor = _fallParty != null ? _fallParty.GetComponent<PlayerMotor>() : null;
            if (_fallParty != null) _safePosition = _fallParty.transform.position;
            // 걷는 몬스터(창귀)는 구멍 앞에서 멈추고(EnemyController가 발밑 땅을 검사), 떠다니는 도깨비불은 그냥 건넌다 — 몬스터는 중력을 받지 않아 빠지지 않는다
        }

        private bool NearPit(float x)
        {
            foreach (var pit in Layout.Pits)
                if (x > pit.Left - SafeEdgeMargin && x < pit.Right + SafeEdgeMargin) return true;
            return false;
        }

        private void TrackFall()
        {
            if (_fallParty == null || _fallParty.IsDead || Layout.Pits.Length == 0) return;
            var pos = (Vector2)_fallParty.transform.position;
            float feet = pos.y - _fallParty.transform.localScale.y * 0.5f;
            // 땅(발판이 아닌 바닥 높이)에 서 있고 구멍에서 떨어져 있을 때만 안전한 자리로 기억한다
            if (_fallMotor != null && _fallMotor.IsGrounded && feet < 0.3f && !NearPit(pos.x)) _safePosition = pos;
            if (pos.y < FallKillY) Fall();
        }

        /// <summary>구멍에 빠졌다: 피해(무적·방어 무시)를 입고 안전한 자리로 돌아와 잠깐 무적.</summary>
        public void Fall()
        {
            if (_fallParty == null) return;
            FallCount++;
            _fallParty.TakeDamage(FallDamage, ignoreInvulnerability: true, ignoreReduction: true);
            Hud.Popup($"추락!  -{FallDamage}", new Color(1f, 0.55f, 0.55f), false);
            if (_fallParty.IsDead) return;
            var pos = new Vector3(_safePosition.x, _safePosition.y + 0.2f, 0f);
            _fallParty.transform.position = pos;
            var rb = _fallParty.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = pos;
                rb.linearVelocity = Vector2.zero;
            }
            Physics2D.SyncTransforms();
            _fallParty.GrantInvulnerability(1.5f); // 돌아온 직후 바로 맞지 않게 조금 더 길게
            Hud.ScreenFlash(new Color(0f, 0f, 0f, 0.5f));
            Fx.Ring(pos, new Color(0.65f, 0.45f, 1f), 2.5f, 0.5f, 0.1f, 0.35f);
            if (CameraRig.Instance != null) CameraRig.Instance.SnapToTarget();
        }

        private void Update()
        {
            if (!GameManager.InputBlocked) TrackFall();
            if (!IsRunRoom || GameManager.InputBlocked) return;
            switch (CurrentPhase)
            {
                case Phase.Intro:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    if (WaitingForBossTrigger) break; // 보스방: 플레이어가 앞으로 걸어 나와야 등장
                    if (_waves.Count == 0) Clear();
                    else SpawnWave(0);
                    break;
                case Phase.Fighting:
                    // 혹시 바닥 밖으로 밀려난 몬스터가 있으면 치운다 (웨이브가 안 끝나는 일이 없게)
                    foreach (var e in _alive) if (e != null && !e.IsDead && e.transform.position.y < FallKillY) e.Despawn();
                    _alive.RemoveAll(e => e == null || e.IsDead);
                    SpawnDue();
                    if (_alive.Count > 0) break;
                    // 다 잡았는데 늦게 나올 몬스터가 남았으면 기다리지 않고 바로 다음을 부른다
                    if (_pending.Count > 0)
                    {
                        Spawn(_pending[0]);
                        _pending.RemoveAt(0);
                        break;
                    }
                    if (WaveIndex + 1 < _waves.Count)
                    {
                        CurrentPhase = Phase.BetweenWaves;
                        _timer = betweenWaves;
                    }
                    else Clear();
                    break;
                case Phase.BetweenWaves:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) SpawnWave(WaveIndex + 1);
                    break;
            }
        }

        private void SpawnWave(int index)
        {
            WaveIndex = index;
            CurrentPhase = Phase.Fighting;
            if (_waves.Count > 1) Hud.Popup($"{index + 1} / {_waves.Count} 파도", new Color(1f, 1f, 1f), false);
            _pending.Clear();
            _pending.AddRange(_waves[index]);
            _pending.Sort((a, b) => a.Delay.CompareTo(b.Delay));
            _waveTime = 0f;
            SpawnDue();
        }

        /// <summary>웨이브 시작 뒤 Delay가 지난 몬스터를 세운다 (늦게 들어오는 몬스터 — 시간차·협공).</summary>
        private void SpawnDue()
        {
            _waveTime += Time.deltaTime;
            while (_pending.Count > 0 && _pending[0].Delay <= _waveTime)
            {
                Spawn(_pending[0]);
                _pending.RemoveAt(0);
            }
        }

        /// <summary>발판 위 도깨비불이 발판 끝에서 이만큼 안쪽까지만 움직인다 (몸이 발판 밖 허공에 걸치지 않게).</summary>
        public const float PlatformEdgeInset = 0.4f;

        private void Spawn(SpawnSpec spec)
        {
            if (spec.Boss)
            {
                Track(_spawner.SpawnBoss(BossAppearX()));
                return;
            }
            var party = FindAnyObjectByType<PlayerParty>();
            float px = party != null ? party.transform.position.x : 0f;
            float x = spec.Anchor switch
            {
                SpawnAnchor.AheadOfPlayer => px + spec.X,
                SpawnAnchor.BehindPlayer => px - spec.X,
                _ => spec.X * ArenaScale, // 방 좌표는 기본 길이 기준으로 짰다 — 늘어난 방에선 같은 비율 자리
            };
            float height = spec.Height;
            float? confineMin = null, confineMax = null;
            if (spec.Kind == EnemyKind.Ranged && spec.Height > 0f)
            {
                // 떠서 나오는 도깨비불은 허공이 아니라 원하는 층에 가장 가까운 발판 위에서 나오고, 그 발판 안에서만 움직인다
                var platform = RoomLayouts.NearestPlatform(Layout, x, spec.Height);
                if (platform.HasValue)
                {
                    var p = platform.Value;
                    height = RoomLayouts.Top(p);
                    confineMin = p.X - p.Width * 0.5f + PlatformEdgeInset;
                    confineMax = p.X + p.Width * 0.5f - PlatformEdgeInset;
                }
                else height = 0f;
            }
            var enemy = _spawner.SpawnEnemyAt(spec.Kind, x, height, spec.Elite, confineMin, confineMax);
            if (enemy != null && spec.Delay > 0f)
                Hud.WorldText(enemy.transform.position + Vector3.up * 1.4f, "!", new Color(1f, 0.85f, 0.3f), 1.2f); // 늦게 들어온 몬스터 — 어디서 나왔는지 보이게
            Track(enemy);
        }

        private void Track(EnemyController enemy)
        {
            if (enemy != null) _alive.Add(enemy);
        }

        /// <summary>방을 깼다. 엘리트·보상은 제단에서 하나를 고르면 문이 열리고, 상점은 제단과 문이 함께 열리고, 일반방은 바로 문이 열린다. 마지막 방은 사당 출구.</summary>
        private void Clear()
        {
            CurrentPhase = Phase.Cleared;
            Sfx.Play(SfxId.LevelUp, 0.7f);
            var type = RunState.Current;
            GrantRoomXp(type);
            if (RunState.IsFinalRoom || !(type == RoomType.Elite || type == RoomType.Reward || type == RoomType.Shop))
            {
                OpenGates();
                return;
            }

            var offers = RoomOffer.For(type);
            bool shop = type == RoomType.Shop;
            for (int i = 0; i < offers.Count; i++)
            {
                float x = (i - (offers.Count - 1) * 0.5f) * 6.5f;
                var altar = RoomAltar.Create(new Vector3(x, 0f, 0f), offers[i]);
                if (!shop) altar.TakenEvent += OnChosen;
                _altars.Add(altar);
            }
            if (shop)
            {
                Hud.Popup("상점 — 엽전으로 산다", RoomInfo.Color(type), true);
                OpenGates(announce: false);
            }
            else Hud.Popup(type == RoomType.Elite ? "엘리트를 쓰러뜨렸다 — 하나를 고른다" : "보상 — 하나를 고른다", RoomInfo.Color(type), true);
        }

        /// <summary>방을 깬 경험치 (스킬 트리·아이템의 경험치 배율 적용). 런 중엔 몬스터 처치 경험치가 없다.</summary>
        private void GrantRoomXp(RoomType type)
        {
            int baseXp = RoomPlan.XpReward(type, RunState.IsFinalRoom);
            if (baseXp <= 0) return;
            var level = FindAnyObjectByType<PlayerLevel>();
            if (level == null) return;
            int xp = Mathf.Max(1, Mathf.RoundToInt(baseXp * SkillTreeState.XpMultiplier));
            Hud.WorldText(level.transform.position + Vector3.up * 1.6f, $"방 클리어 +{xp} EXP", new Color(0.45f, 0.9f, 1f), 1.2f);
            level.AddXp(xp);
            LastRoomXp = xp;
            RewardFx.Burst(RewardFx.Kind.Exp, level.transform.position + Vector3.up * 0.5f, 3); // 청록 혼불
        }

        /// <summary>마지막으로 방을 깨서 얻은 경험치 (테스트·디버그).</summary>
        public int LastRoomXp { get; private set; }

        /// <summary>하나만 고르는 방: 하나를 얻으면 나머지는 잠기고 문이 열린다.</summary>
        private void OnChosen(RoomAltar chosen)
        {
            foreach (var altar in _altars) if (altar != chosen) altar.Lock();
            OpenGates();
        }

        private void OpenGates(bool announce = true)
        {
            if (_gatesOpen) return;
            _gatesOpen = true;
            float left = _spawner != null ? _spawner.ArenaMinX : -22f;
            float right = _spawner != null ? _spawner.ArenaMaxX : 22f;
            if (RunState.IsFinalRoom)
            {
                Hud.Popup("귀문을 닫았다!", new Color(0.8f, 0.7f, 1f), true);
                _gates.Add(RoomGate.Create(new Vector3((left + right) * 0.5f + 6f, 0f, 0f), RoomType.Normal, true, gateSprite, portalArt));
            }
            else
            {
                if (announce) Hud.Popup("문을 고른다", new Color(0.8f, 0.7f, 1f), true);
                var options = RunState.RollOptions();
                // 다음 방 문은 전부 오른쪽 끝에 나란히 (둘이면 바깥 · 안쪽, 하나면 두 자리 사이)
                for (int i = 0; i < options.Length; i++)
                {
                    float x = right - gateInset - (options.Length == 1 ? gateSpacing * 0.5f : i * gateSpacing);
                    _gates.Add(RoomGate.Create(new Vector3(x, 0f, 0f), options[i], false, gateSprite, portalArt));
                }
            }
        }
    }
}
