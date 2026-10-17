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

        [SerializeField, Tooltip("문 그림 (바닥 가운데 피벗). 없으면 색 네모")] private Sprite gateSprite;
        [SerializeField] private float introDelay = 1.2f;
        [SerializeField] private float betweenWaves = 1.6f;
        [SerializeField, Tooltip("다음 방 문은 왼쪽 끝에 나란히 선다: 가장 바깥 문이 바닥 왼쪽 끝에서 안쪽으로 떨어진 거리")] private float gateInset = 1.8f;
        [SerializeField, Tooltip("왼쪽 문들 사이 간격 (문 글자가 겹치지 않게)")] private float gateSpacing = 4.2f;

        public static RoomController Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Intro;
        public int WaveIndex { get; private set; }
        public int WaveCount => _waves.Count;
        public IReadOnlyList<RoomGate> Gates => _gates;
        public IReadOnlyList<RoomAltar> Altars => _altars;
        public bool IsRunRoom { get; private set; }

        private EnemySpawner _spawner;
        private List<RoomPlan.Wave> _waves = new();
        private readonly List<EnemyController> _alive = new();
        private readonly List<RoomGate> _gates = new();
        private readonly List<RoomAltar> _altars = new();
        private bool _gatesOpen;
        private const int GoldMelee = 5, GoldRanged = 7, GoldElite = 25;
        private float _timer;

        public int LayoutIndex { get; private set; }
        public RoomLayout Layout => RoomLayouts.All[LayoutIndex];

        private void Awake()
        {
            Instance = this;
            IsRunRoom = RunState.Active;
            // 방 지형(발판 배치): 연습 모드는 기본, 런은 방마다 다르게 (바닥 그림 데코레이터가 첫 프레임에 그 배치대로 깐다)
            LayoutIndex = RoomLayouts.Pick(IsRunRoom, RunState.Current, RunState.RoomNumber, RunState.Seed, RunState.LastLayout);
            if (IsRunRoom) RunState.LastLayout = LayoutIndex;
            var deco = FindAnyObjectByType<FloorDecorator>();
            if (deco != null) deco.SetPlatformLayout(Layout.Platforms);
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
            int gold = enemy.IsElite ? GoldElite : enemy.Kind == EnemyKind.Ranged ? GoldRanged : GoldMelee;
            RunState.AddGold(gold);
            Hud.WorldText(enemy.transform.position + Vector3.up * 0.8f, $"+{gold} 엽전", new Color(1f, 0.85f, 0.35f), 1f);
        }

        private void Start()
        {
            if (!IsRunRoom) return;
            _waves = RoomPlan.For(RunState.Current, RunState.RoomNumber);
            _timer = introDelay;
            EnemyController.AnyDied += OnEnemyDied;
            RunState.Changed += UpdateInfo;
            UpdateInfo();
            Hud.Popup($"방 {RunState.RoomNumber} · {RoomInfo.Label(RunState.Current)}  ({Layout.Name})", RoomInfo.Color(RunState.Current), true);
        }

        private void UpdateInfo()
        {
            string text = $"방 {RunState.RoomNumber} / {RunState.TotalRooms}  ·  {RoomInfo.Label(RunState.Current)}" +
                          $"      <color=#FFD95A>엽전 {RunState.Gold}</color>   <color=#C9A6FF>영혼석 {RunState.SoulStones}</color>";
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

        private void Update()
        {
            if (!IsRunRoom || GameManager.InputBlocked) return;
            switch (CurrentPhase)
            {
                case Phase.Intro:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    if (_waves.Count == 0) Clear();
                    else SpawnWave(0);
                    break;
                case Phase.Fighting:
                    _alive.RemoveAll(e => e == null || e.IsDead);
                    if (_alive.Count > 0) break;
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
            var w = _waves[index];
            if (_waves.Count > 1) Hud.Popup($"{index + 1} / {_waves.Count} 파도", new Color(1f, 1f, 1f), false);
            int i = 0;
            for (int m = 0; m < w.Melee; m++) Track(_spawner.SpawnEnemy(EnemyKind.Melee, i++, m < w.EliteMelee));
            i = 0;
            for (int r = 0; r < w.Ranged; r++) Track(_spawner.SpawnEnemy(EnemyKind.Ranged, i++, r < w.EliteRanged));
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
                _gates.Add(RoomGate.Create(new Vector3((left + right) * 0.5f + 6f, 0f, 0f), RoomType.Normal, true, gateSprite));
            }
            else
            {
                if (announce) Hud.Popup("문을 고른다", new Color(0.8f, 0.7f, 1f), true);
                var options = RunState.RollOptions();
                // 다음 방 문은 전부 왼쪽 끝에 나란히 (둘이면 바깥 · 안쪽, 하나면 두 자리 사이)
                for (int i = 0; i < options.Length; i++)
                {
                    float x = left + gateInset + (options.Length == 1 ? gateSpacing * 0.5f : i * gateSpacing);
                    _gates.Add(RoomGate.Create(new Vector3(x, 0f, 0f), options[i], false, gateSprite));
                }
            }
        }
    }
}
