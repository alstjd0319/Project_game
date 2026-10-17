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
        [SerializeField, Tooltip("문이 서는 자리: 바닥 끝에서 안쪽으로 이 거리")] private float gateInset = 4f;

        public static RoomController Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Intro;
        public int WaveIndex { get; private set; }
        public int WaveCount => _waves.Count;
        public IReadOnlyList<RoomGate> Gates => _gates;
        public bool IsRunRoom { get; private set; }

        private EnemySpawner _spawner;
        private List<RoomPlan.Wave> _waves = new();
        private readonly List<EnemyController> _alive = new();
        private readonly List<RoomGate> _gates = new();
        private float _timer;

        private void Awake()
        {
            Instance = this;
            IsRunRoom = RunState.Active;
            if (!IsRunRoom) return;
            _spawner = FindAnyObjectByType<EnemySpawner>();
            if (_spawner != null) _spawner.RoomMode = true; // 스포너 Start보다 먼저 — 몬스터를 계속 유지하는 연습 모드를 끈다
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (!IsRunRoom) return;
            _waves = RoomPlan.For(RunState.Current, RunState.RoomNumber);
            _timer = introDelay;
            UpdateInfo();
            Hud.Popup($"방 {RunState.RoomNumber} · {RoomInfo.Label(RunState.Current)}", RoomInfo.Color(RunState.Current), true);
        }

        private void UpdateInfo() =>
            Hud.SetRunInfo($"방 {RunState.RoomNumber} / {RunState.TotalRooms}  ·  {RoomInfo.Label(RunState.Current)}");

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

        /// <summary>방을 깼다: 문을 연다 (마지막 방이면 사당으로 나가는 문).</summary>
        private void Clear()
        {
            CurrentPhase = Phase.Cleared;
            Sfx.Play(SfxId.LevelUp, 0.7f);
            float left = _spawner != null ? _spawner.ArenaMinX : -22f;
            float right = _spawner != null ? _spawner.ArenaMaxX : 22f;
            if (RunState.IsFinalRoom)
            {
                Hud.Popup("귀문을 닫았다!", new Color(0.8f, 0.7f, 1f), true);
                _gates.Add(RoomGate.Create(new Vector3((left + right) * 0.5f + 6f, 0f, 0f), RoomType.Normal, true, gateSprite));
            }
            else
            {
                Hud.Popup("방을 깼다 — 문을 고른다", new Color(0.8f, 0.7f, 1f), true);
                var options = RunState.RollOptions();
                // 문이 둘이면 왼쪽 끝·오른쪽 끝에 하나씩, 하나면 오른쪽
                for (int i = 0; i < options.Length; i++)
                {
                    float x = options.Length == 1 ? right - gateInset : (i == 0 ? left + gateInset : right - gateInset);
                    _gates.Add(RoomGate.Create(new Vector3(x, 0f, 0f), options[i], false, gateSprite));
                }
            }
        }
    }
}
