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

        // 부적 그림 (판정 네모 대신 보인다 — 네모는 "판정 상자 보기"에서만)
        private const float ArtScale = 0.55f;
        private SpriteRenderer _art;
        private SpriteClip _artClip;
        private float _artTime;

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
            p.AttachArt(size);
            return p;
        }

        private void AttachArt(Vector2 size)
        {
            var clip = GameAssets.AttackFx != null ? GameAssets.AttackFx.talismanProjectile : null;
            if (clip == null || clip.IsEmpty) return;
            var go = new GameObject("Art");
            go.transform.SetParent(transform, false);
            // 부모 스케일 = 박스 크기 → 상쇄해서 그림을 원본 비율(× ArtScale)로
            go.transform.localScale = new Vector3(ArtScale / size.x, ArtScale / size.y, 1f);
            _art = go.AddComponent<SpriteRenderer>();
            _art.sortingOrder = 26;
            _artClip = clip;
            _art.sprite = clip.frames[0];
            GetComponent<SpriteRenderer>().enabled = GameTuning.Current.showHitboxes;
        }

        private void Update()
        {
            if (_art != null)
            {
                _artTime += Time.deltaTime;
                _art.sprite = _artClip.frames[(int)(_artTime * _artClip.fps) % _artClip.frames.Length];
                _art.flipY = _direction.x < 0f; // 왼쪽으로 날아갈 때 뒤집힌 채 돌지 않게
            }

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
                if (GameAssets.AttackFx != null) FxClip.PlayFit(GameAssets.AttackFx.talismanBurst, transform.position, new Vector2(1.5f, 1.5f));
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
