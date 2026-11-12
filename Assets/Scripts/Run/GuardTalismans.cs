using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 강화 "수호 부적": 몸 주위를 부적 n장이 돌며 닿은 적에게 약한 피해 (자동 공격 — 잔챙이 정리용, 몬스터 공격은 끊지 않는다).
    /// 판정은 부적 그림과 같은 크기의 네모 겹침. 같은 적은 <see cref="HitInterval"/>초마다 한 번만.
    /// </summary>
    public class GuardTalismans : MonoBehaviour
    {
        public const float Radius = 1.7f;
        public const float DegreesPerSecond = 200f;
        public const float HitInterval = 0.5f;
        public static readonly Vector2 Size = new(0.5f, 0.5f);

        private readonly List<Transform> _talismans = new();
        private readonly Dictionary<EnemyController, float> _nextHit = new();
        private float _angle;

        public int Count => _talismans.Count;
        public int HitCount { get; private set; }
        public IReadOnlyList<Transform> Talismans => _talismans;

        public void SetCount(int n)
        {
            while (_talismans.Count < n) _talismans.Add(Create());
            while (_talismans.Count > n)
            {
                var t = _talismans[^1];
                _talismans.RemoveAt(_talismans.Count - 1);
                if (t != null) Destroy(t.gameObject);
            }
        }

        private Transform Create()
        {
            var go = new GameObject("GuardTalisman");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 26;
            var clip = GameAssets.AttackFx != null ? GameAssets.AttackFx.talismanProjectile : null;
            if (clip != null && !clip.IsEmpty)
            {
                sr.sprite = clip.frames[0];
                go.transform.localScale = Vector3.one * 0.42f;
            }
            else
            {
                sr.sprite = FxSprites.Glow;
                sr.color = new Color(0.55f, 1f, 0.6f, 0.9f);
                go.transform.localScale = (Vector3)Size;
            }
            // 빛무리
            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(go.transform, false);
            glow.sprite = FxSprites.Glow;
            glow.color = new Color(0.55f, 1f, 0.6f, 0.45f);
            glow.sortingOrder = 25;
            glow.transform.localScale = Vector3.one * 2.2f;
            return go.transform;
        }

        private void OnDestroy()
        {
            foreach (var t in _talismans) if (t != null) Destroy(t.gameObject);
        }

        private void Update()
        {
            if (_talismans.Count == 0) return;
            _angle = (_angle + DegreesPerSecond * Time.deltaTime) % 360f;
            Vector2 center = (Vector2)transform.position + Vector2.up * 0.2f;
            Physics2D.SyncTransforms();
            for (int i = 0; i < _talismans.Count; i++)
            {
                float a = (_angle + 360f * i / _talismans.Count) * Mathf.Deg2Rad;
                Vector2 pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * Radius;
                var t = _talismans[i];
                t.position = pos;
                t.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                HitAt(pos);
            }
        }

        private void HitAt(Vector2 pos)
        {
            foreach (var col in Physics2D.OverlapBoxAll(pos, Size, 0f))
            {
                var enemy = col.attachedRigidbody != null ? col.attachedRigidbody.GetComponent<EnemyController>() : col.GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead) continue;
                if (_nextHit.TryGetValue(enemy, out float next) && Time.time < next) continue;
                _nextHit[enemy] = Time.time + HitInterval;
                var hit = HitInfo.Create(DamageSource.Upgrade, CharacterKind.Player, pos);
                hit.Ranged = true;
                hit.Stagger = HitStagger.None;
                DamageCalc.Apply(WaveUpgrades.GuardDamage, hit, enemy, Mathf.Sign(enemy.transform.position.x - transform.position.x));
                Fx.Sparks(pos, new Color(0.6f, 1f, 0.65f), 4, 4f);
                HitCount++;
            }
        }
    }
}
