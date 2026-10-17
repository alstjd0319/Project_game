using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 테스트용 몬스터를 유지한다. 죽으면 일정 시간 뒤 플레이어에게서 먼 쪽에 다시 생성.
    /// 연습 스위치(F1 연습 탭 "근접/원거리 몬스터 끄기")가 켜지면 그 종류를 치우고 다시 만들지 않는다.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemyController meleePrefab;
        [SerializeField] private EnemyController rangedPrefab;
        [SerializeField, Tooltip("보스(불가살이) — 런의 마지막 방에서만 나온다")] private EnemyController bossPrefab;
        [SerializeField] private bool spawnMelee = true;
        [SerializeField] private bool spawnRanged = true;
        [SerializeField] private float respawnDelay = 2.5f;
        [SerializeField] private float spawnDistanceFromPlayer = 8f;
        [SerializeField, Tooltip("같은 종류가 여러 마리일 때 출현 위치 간격")] private float spawnSpacing = 2.2f;
        [SerializeField] private float groundY = 0f;
        [SerializeField] private float arenaMinX = -21f;
        [SerializeField] private float arenaMaxX = 21f;

        /// <summary>종류마다 GameTuning.enemiesPerKind 마리를 유지하는 자리.</summary>
        private sealed class Slot
        {
            public EnemyController prefab;
            public float side;
            public bool enabledInScene;
            public readonly List<EnemyController> live = new();
            public int respawning;
            public int spawned;
        }

        private Transform _player;
        private Slot _melee;
        private Slot _ranged;

        /// <summary>true면 몬스터를 계속 유지하지 않는다 — 방 흐름(<see cref="RoomController"/>)이 직접 <see cref="SpawnEnemy"/>로 세운다.</summary>
        public bool RoomMode { get; set; }
        public float ArenaMinX => arenaMinX;
        public float ArenaMaxX => arenaMaxX;

        private void Start()
        {
            var defense = FindAnyObjectByType<PlayerDefense>();
            if (defense != null) _player = defense.transform;

            _melee = new Slot { prefab = meleePrefab, side = 1f, enabledInScene = spawnMelee && meleePrefab != null };
            _ranged = new Slot { prefab = rangedPrefab, side = -1f, enabledInScene = spawnRanged && rangedPrefab != null };
            if (RoomMode) return;
            Maintain(_melee, GameTuning.Current.hideMelee);
            Maintain(_ranged, GameTuning.Current.hideRanged);
        }

        private void Update()
        {
            if (RoomMode) return;
            Maintain(_melee, GameTuning.Current.hideMelee);
            Maintain(_ranged, GameTuning.Current.hideRanged);
        }

        /// <summary>연습 스위치에 맞춰 치우거나(경험치 없이) 목표 수까지 세운다. 목표를 줄이면 남는 몬스터는 조용히 치운다.</summary>
        private void Maintain(Slot slot, bool hidden)
        {
            if (slot == null || !slot.enabledInScene) return;

            slot.live.RemoveAll(e => e == null || e.IsDead);
            int target = hidden ? 0 : Mathf.Max(1, GameTuning.Current.enemiesPerKind);

            while (slot.live.Count > target)
            {
                var extra = slot.live[^1];
                slot.live.RemoveAt(slot.live.Count - 1);
                extra.Despawn();
            }

            while (slot.live.Count + slot.respawning < target) Spawn(slot);
        }

        private void Spawn(Slot slot)
        {
            var enemy = SpawnAt(slot.prefab, slot.side, slot.live.Count, new Color(0.95f, 0.3f, 0.35f));
            enemy.Died += _ => StartCoroutine(Respawn(slot));
            slot.live.Add(enemy);
        }

        /// <summary>방 흐름용: 종류·엘리트 여부를 정해 하나 세운다. index는 같은 웨이브 안의 순번(겹치지 않게 간격을 둔다).</summary>
        public EnemyController SpawnEnemy(EnemyKind kind, int index, bool elite)
        {
            var prefab = kind == EnemyKind.Melee ? meleePrefab : rangedPrefab;
            if (prefab == null) return null;
            float side = kind == EnemyKind.Melee ? 1f : -1f;
            var enemy = SpawnAt(prefab, side, index, elite ? new Color(0.8f, 0.45f, 1f) : new Color(0.95f, 0.3f, 0.35f));
            if (elite) enemy.MakeElite(RoomPlan.EliteHpMultiplier, 3);
            return enemy;
        }

        /// <summary>보스를 세운다: 방 가운데에서 오른쪽(플레이어는 왼쪽 끝에서 시작). 머리 위 체력바 대신 화면 상단 보스 체력바(BossController).</summary>
        public EnemyController SpawnBoss()
        {
            if (bossPrefab == null) return null;
            float half = bossPrefab.transform.localScale.x * 0.5f;
            float x = Mathf.Clamp((arenaMinX + arenaMaxX) * 0.5f + 6f, arenaMinX + half, arenaMaxX - half);
            var pos = new Vector3(x, groundY + bossPrefab.transform.localScale.y * 0.5f, 0f);
            var boss = Instantiate(bossPrefab, pos, Quaternion.identity);
            boss.name = bossPrefab.name;
            boss.SetArena(arenaMinX, arenaMaxX);
            Fx.Ring(pos, new Color(1f, 0.55f, 0.2f), 3f, 6f, 0.15f, 0.6f);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.3f, 0.5f);
            return boss;
        }

        private EnemyController SpawnAt(EnemyController prefab, float side, int count, Color barColor)
        {
            float px = _player != null ? _player.position.x : 0f;
            float half = prefab.transform.localScale.x * 0.5f;
            // 여러 마리면 한 줄로 겹치지 않게 간격을 두고 바깥쪽으로 늘어세운다
            float offset = count * spawnSpacing + Random.Range(0f, 1f);
            float x = px + side * (spawnDistanceFromPlayer + offset);
            if (x < arenaMinX + half || x > arenaMaxX - half) x = px - side * (spawnDistanceFromPlayer + offset + 3f); // 반대쪽으로 넘어갈 땐 같은 쪽 몬스터와 겹치지 않게 조금 더 멀리
            if (x < arenaMinX + half || x > arenaMaxX - half) x = Random.Range(arenaMinX + half, arenaMaxX - half);
            x = Mathf.Clamp(x, arenaMinX + half, arenaMaxX - half);
            // 걷는 몬스터가 바닥 구멍 위에 생기지 않게 가까운 땅으로 옮긴다 (도깨비불은 떠다녀서 상관없다)
            if (prefab.Kind == EnemyKind.Melee) x = GroundProbe.NudgeOntoGround(x, half, groundY, arenaMinX, arenaMaxX);

            var pos = new Vector3(x, groundY + prefab.transform.localScale.y * 0.5f, 0f);
            var enemy = Instantiate(prefab, pos, Quaternion.identity);
            enemy.name = prefab.name;
            enemy.SetArena(arenaMinX, arenaMaxX);
            WorldBar.Create(enemy.transform, 1.2f, barColor, () => enemy != null ? enemy.HpRatio : 0f);
            Fx.Ring(pos, Color.white, 2.5f, 0.5f, 0.1f, 0.3f);
            return enemy;
        }

        private IEnumerator Respawn(Slot slot)
        {
            slot.respawning++;
            yield return new WaitForSeconds(respawnDelay);
            slot.respawning--;
            // 기다리는 동안 연습 스위치로 꺼졌으면 Maintain이 알아서 안 만든다
        }
    }
}
