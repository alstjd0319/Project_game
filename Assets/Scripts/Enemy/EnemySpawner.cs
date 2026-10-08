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

        private void Start()
        {
            var defense = FindAnyObjectByType<PlayerDefense>();
            if (defense != null) _player = defense.transform;

            _melee = new Slot { prefab = meleePrefab, side = 1f, enabledInScene = spawnMelee && meleePrefab != null };
            _ranged = new Slot { prefab = rangedPrefab, side = -1f, enabledInScene = spawnRanged && rangedPrefab != null };
            Maintain(_melee, GameTuning.Current.hideMelee);
            Maintain(_ranged, GameTuning.Current.hideRanged);
        }

        private void Update()
        {
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
            var prefab = slot.prefab;
            float px = _player != null ? _player.position.x : 0f;
            float half = prefab.transform.localScale.x * 0.5f;
            // 여러 마리면 한 줄로 겹치지 않게 간격을 두고 바깥쪽으로 늘어세운다
            float offset = slot.live.Count * spawnSpacing + Random.Range(0f, 1f);
            float x = px + slot.side * (spawnDistanceFromPlayer + offset);
            if (x < arenaMinX + half || x > arenaMaxX - half) x = px - slot.side * (spawnDistanceFromPlayer + offset);
            if (x < arenaMinX + half || x > arenaMaxX - half) x = Random.Range(arenaMinX + half, arenaMaxX - half);
            x = Mathf.Clamp(x, arenaMinX + half, arenaMaxX - half);

            var pos = new Vector3(x, groundY + prefab.transform.localScale.y * 0.5f, 0f);
            var enemy = Instantiate(prefab, pos, Quaternion.identity);
            enemy.name = prefab.name;
            enemy.SetArena(arenaMinX, arenaMaxX);
            enemy.Died += _ => StartCoroutine(Respawn(slot));
            slot.live.Add(enemy);
            WorldBar.Create(enemy.transform, 1.2f, new Color(0.95f, 0.3f, 0.35f), () => enemy != null ? enemy.HpRatio : 0f);
            Fx.Ring(pos, Color.white, 2.5f, 0.5f, 0.1f, 0.3f);
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
