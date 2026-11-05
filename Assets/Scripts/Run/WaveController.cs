using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 웨이브 맵의 진행 (<see cref="RoomController"/>가 붙인다). 웨이브의 몬스터를 전부 잡으면 숨 돌릴 틈 뒤 다음 웨이브, 10웨이브 보스를 잡으면 사당 출구.
    /// 규칙은 docs/기획_웨이브모드.md. 보스 소환·결계와 보상은 아직 정하지 않아 임시로 광장으로 옮겨 싸운다.
    /// </summary>
    public class WaveController : MonoBehaviour
    {
        public enum Phase { Rest, Fighting, Cleared }

        /// <summary>웨이브 사이 숨 돌릴 틈 (테스트가 줄인다).</summary>
        public static float RestTime = 3f;
        private const float MarkerTime = 0.5f, HoldAfterSpawn = 1f;

        public Phase CurrentPhase { get; private set; } = Phase.Rest;
        /// <summary>지금 웨이브 번호 (1부터).</summary>
        public int Wave { get; private set; }
        public int Killed { get; private set; }
        public int Spawned { get; private set; }
        public WaveDef Def => Wave >= 1 ? WavePlan.Waves[Wave - 1] : null;
        public int Remaining => Def != null ? Def.Total - Killed : 0;
        public IReadOnlyList<EnemyController> Alive => _alive;
        public static WaveController Instance { get; private set; }

        private readonly List<EnemyController> _alive = new();
        private EnemySpawner _spawner;
        private Transform _player;
        private float _timer;
        private float _spawnTimer;
        private int _incoming;      // 등장 표시 중인 몬스터
        private bool _eliteDone;

        private void Awake() => Instance = this;

        private void Start()
        {
            _spawner = FindAnyObjectByType<EnemySpawner>();
            var party = FindAnyObjectByType<PlayerParty>();
            if (party != null) _player = party.transform;
            EnemyController.AnyDied += OnDied;
            gameObject.AddComponent<WaveIndicators>();
            _timer = 1.2f;
            Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EnemyController.AnyDied -= OnDied;
            AttackTokens.ClearOverride();
        }

        /// <summary>그 웨이브부터 시작한다 (테스트·캡처).</summary>
        public void JumpTo(int wave)
        {
            foreach (var e in _alive) if (e != null) e.Despawn();
            _alive.Clear();
            StartWave(wave);
        }

        private void OnDied(EnemyController enemy)
        {
            if (!_alive.Remove(enemy)) return;
            Killed++;
            Refresh();
        }

        private void Update()
        {
            if (GameManager.InputBlocked || CurrentPhase == Phase.Cleared) return;
            if (CurrentPhase == Phase.Rest)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) StartWave(Wave + 1);
                return;
            }
            _alive.RemoveAll(e => e == null);
            if (Killed >= Def.Total)
            {
                FinishWave();
                return;
            }
            if (Def.Boss) return;
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f && Spawned < Def.Total && _alive.Count + _incoming < Def.Concurrent)
            {
                _spawnTimer = WavePlan.SpawnInterval;
                SpawnNext();
            }
        }

        private void StartWave(int wave)
        {
            Wave = wave;
            Killed = 0;
            Spawned = 0;
            _eliteDone = false;
            _spawnTimer = 0f;
            CurrentPhase = Phase.Fighting;
            var def = Def;
            AttackTokens.SetOverride(def.Tokens, def.Stagger);
            Hud.Popup(def.Boss ? "불가살이가 깨어난다" : $"웨이브 {wave} — {def.ZoneName}에 귀문이 열렸다", new Color(0.8f, 0.7f, 1f), true);
            if (def.Boss) StartBoss();
            Refresh();
        }

        private void FinishWave()
        {
            if (Wave >= WavePlan.Waves.Length)
            {
                CurrentPhase = Phase.Cleared;
                Hud.Popup("귀문을 닫았다!", new Color(0.8f, 0.7f, 1f), true);
                if (RoomController.Instance != null) RoomController.Instance.OpenWaveExit();
                return;
            }
            CurrentPhase = Phase.Rest;
            _timer = RestTime;
            Hud.Popup($"웨이브 {Wave} 돌파", Color.white, false);
        }

        // 보스는 평지가 필요하다. 소환·결계 규칙을 정하기 전까지는 플레이어를 광장으로 옮겨 그대로 싸운다 (임시)
        private void StartBoss()
        {
            if (_player != null)
            {
                var pos = new Vector3(-6f, _player.localScale.y * 0.5f + 0.05f, 0f);
                _player.position = pos;
                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
                Physics2D.SyncTransforms();
            }
            if (CameraRig.Instance != null)
            {
                CameraRig.Instance.SetView(RoomController.BossViewSize, RoomController.BossViewSize - 2.5f);
                CameraRig.Instance.SnapToTarget();
            }
            Hud.ScreenFlash(new Color(0.55f, 0.35f, 0.9f, 0.6f));
            var boss = _spawner != null ? _spawner.SpawnBoss(2.5f) : null;
            if (boss != null) { _alive.Add(boss); Spawned = 1; }
        }

        private void SpawnNext()
        {
            var def = Def;
            Spawned++;
            if (def.Elite && !_eliteDone)
            {
                // 엘리트는 플레이어 앞 평평한 땅에 (구멍 건너편에 서 있지 않게)
                _eliteDone = true;
                float dir = _player != null && _player.position.x > 0f ? -1f : 1f;
                var elite = _spawner.SpawnEnemyAt(EnemyKind.Melee, (_player != null ? _player.position.x : 0f) + dir * 8f, 0f, true);
                Register(elite, def, false);
                return;
            }
            var kind = Random.Range(0, def.MeleeWeight + def.RangedWeight) < def.MeleeWeight ? EnemyKind.Melee : EnemyKind.Ranged;
            var point = PickPoint(def, kind);
            StartCoroutine(Arrive(kind, point, def));
        }

        private WaveSpawnPoint PickPoint(WaveDef def, EnemyKind kind)
        {
            bool active = Random.value < WavePlan.ActiveZoneChance;
            var points = new List<WaveSpawnPoint>();
            for (int z = 0; z < WavePlan.ZoneList.Length; z++)
                if (System.Array.IndexOf(def.Zones, z) >= 0 == active) points.AddRange(WavePlan.Points(z, def.MaxLevel));
            if (points.Count == 0) foreach (int z in def.Zones) points.AddRange(WavePlan.Points(z, def.MaxLevel));

            // 플레이어 바로 옆 · 이미 몬스터가 서 있는 자리는 뺀다
            var free = points.FindAll(IsFree);
            if (free.Count > 0) points = free;
            // 창귀는 바닥을, 도깨비불은 발판을 더 자주 쓴다
            bool wantPlatform = Random.value < (kind == EnemyKind.Ranged ? 0.7f : 0.25f);
            var fit = points.FindAll(p => p.Platform.HasValue == wantPlatform);
            if (fit.Count > 0) points = fit;
            return points[Random.Range(0, points.Count)];
        }

        private bool IsFree(WaveSpawnPoint p)
        {
            var at = new Vector2(p.X, p.Height + 0.7f);
            if (_player != null && Vector2.Distance(_player.position, at) < WavePlan.MinPlayerDistance) return false;
            foreach (var e in _alive)
                if (e != null && Vector2.Distance(e.transform.position, at) < 1.5f) return false;
            return true;
        }

        private IEnumerator Arrive(EnemyKind kind, WaveSpawnPoint point, WaveDef def)
        {
            _incoming++;
            int wave = Wave;
            // 화면 안에서 나올 때만 바닥에 표시 (멀리서 나오면 볼 수 없고, 억울할 일도 없다)
            if (OnScreen(point))
            {
                var marker = new GameObject("WaveSpawnMarker").AddComponent<SpriteRenderer>();
                marker.sprite = FxSprites.Glow;
                marker.color = new Color(0.7f, 0.4f, 1f, 0.85f);
                marker.sortingOrder = 15;
                marker.transform.position = new Vector3(point.X, point.Height + 0.05f, 0f);
                marker.transform.localScale = new Vector3(2.2f, 0.55f, 1f);
                yield return new WaitForSeconds(MarkerTime);
                Destroy(marker.gameObject);
            }
            _incoming--;
            if (wave != Wave || CurrentPhase != Phase.Fighting) yield break;

            float? min = null, max = null;
            if (point.Platform.HasValue)
            {
                var p = point.Platform.Value;
                min = p.X - p.Width * 0.5f + RoomController.PlatformEdgeInset;
                max = p.X + p.Width * 0.5f - RoomController.PlatformEdgeInset;
            }
            Register(_spawner.SpawnEnemyAt(kind, point.X, point.Height, false, min, max, exact: true), def, true);
        }

        private void Register(EnemyController enemy, WaveDef def, bool minion)
        {
            if (enemy == null) return;
            if (minion) enemy.ScaleMaxHp(WavePlan.MinionHpScale);
            enemy.SetHeavyChance(def.HeavyChance);
            enemy.HoldAttack(HoldAfterSpawn);
            _alive.Add(enemy);
        }

        private static bool OnScreen(WaveSpawnPoint p)
        {
            var cam = Camera.main;
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(new Vector3(p.X, p.Height + 0.7f, 0f));
            return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        private void Refresh()
        {
            Hud.SetRunInfo(Def == null ? "곧 귀문이 열린다" : Def.Boss ? $"웨이브 {Wave}/{WavePlan.Waves.Length} · 보스" : $"웨이브 {Wave}/{WavePlan.Waves.Length} · 남은 {Remaining}/{Def.Total}"); // 길면 왼쪽 체력 틀에 닿는다
        }
    }
}
