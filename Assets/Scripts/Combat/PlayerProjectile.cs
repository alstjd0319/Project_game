using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 궁수 투사체 (반격 / 공격 스킬 / 증강 화살). 속도는 거리와 무관하게 고정,
    /// 데미지는 투사체 네모가 적 네모와 실제로 겹치는 순간 <see cref="DamageCalc"/>로 계산해 넣는다 (기획서 4.2·5.3).
    /// </summary>
    public class PlayerProjectile : MonoBehaviour
    {
        private EnemyController _target;
        private Vector2 _direction;
        private float _speed;
        private float _maxDistance;
        private float _traveled;
        private int _baseDamage;
        private HitInfo _hit;
        private bool _homing;
        private Color _color;
        private readonly HashSet<EnemyController> _pierced = new();

        public static PlayerProjectile Spawn(Vector2 origin, Vector2 direction, EnemyController target, bool homing,
            float speed, float maxDistance, int baseDamage, HitInfo hit, Color color, Vector2 size)
        {
            var go = Box.Create("PlayerProjectile", origin, size, color, 25);
            var p = go.AddComponent<PlayerProjectile>();
            p._target = target;
            p._homing = homing && target != null;
            p._direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            p._speed = speed;
            p._maxDistance = maxDistance;
            p._baseDamage = baseDamage;
            p._hit = hit;
            p._color = color;
            p.ApplyRotation();
            return p;
        }

        private void Update()
        {
            if (_homing && _target != null && !_target.IsDead)
                _direction = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;

            float step = _speed * Time.deltaTime;
            transform.position += (Vector3)(_direction * step);
            _traveled += step;
            ApplyRotation();

            if (CheckHit()) return;
            if (_traveled >= _maxDistance) Destroy(gameObject);
        }

        private void ApplyRotation()
        {
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        /// <returns>투사체가 사라졌으면 true</returns>
        private bool CheckHit()
        {
            Physics2D.SyncTransforms();
            Vector2 size = transform.localScale;
            foreach (var col in Physics2D.OverlapBoxAll(transform.position, size, transform.eulerAngles.z))
            {
                var enemy = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetComponent<EnemyController>()
                    : col.GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead || _pierced.Contains(enemy)) continue;

                DamageCalc.Apply(_baseDamage, _hit, enemy, Mathf.Sign(_direction.x));
                Fx.Sparks(transform.position, _color, _hit.Heavy ? 10 : 6, 6f);
                if (!_hit.Pierce)
                {
                    Destroy(gameObject);
                    return true;
                }

                // 관통: 뚫고 계속 간다. 유도 화살이면 목표를 뚫은 뒤엔 직진.
                _pierced.Add(enemy);
                _hit.PierceIndex++;
                if (enemy == _target) _homing = false;
            }
            return false;
        }
    }
}
