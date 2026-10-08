using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 플레이어 · 돌진 베기 — 플레이어의 돌진(이동 스킬)이 지나간 적에게 피해 15 (한 번의 돌진에 적마다 1번).
    /// 판정 = 돌진 중인 플레이어 몸 네모 그대로 (적은 트리거라 몸이 통과한다).
    /// 게이지 1칸 · 쿨타임 1초 → 1칸당 기본 15 (기획서 2장 상한).
    /// </summary>
    public class DashSlashAugment : AugmentEffect
    {
        public const int BaseDamage = 15;

        private readonly HashSet<EnemyController> _hitThisDash = new();
        private bool _active;
        private HitInfo _hit;

        private void OnEnable() => Combat.MoveSkillUsed += OnMoveSkill;
        private void OnDisable() => Combat.MoveSkillUsed -= OnMoveSkill;

        private void OnMoveSkill(CharacterKind kind, int dir)
        {
            if (kind != CharacterKind.Player) return;
            _active = true;
            _hitThisDash.Clear();
            _hit = HitInfo.Create(DamageSource.DashSlash, CharacterKind.Player, Pos);
            Sweep(); // 이미 겹쳐 있는 적도 벤다
        }

        private void FixedUpdate()
        {
            if (!_active) return;
            Sweep();
            if (!Motor.IsDashing) _active = false; // 끝난 프레임까지 한 번 더 훑고 종료
        }

        private void Sweep()
        {
            Physics2D.SyncTransforms();
            foreach (var col in Physics2D.OverlapBoxAll(transform.position, transform.localScale, 0f))
            {
                var enemy = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetComponent<EnemyController>()
                    : col.GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead || !_hitThisDash.Add(enemy)) continue;

                float dir = Mathf.Sign(enemy.transform.position.x - transform.position.x);
                Vector2 at = enemy.transform.position;
                DamageCalc.Apply(BaseDamage, _hit, enemy, dir);
                Fx.Streaks(at, new Vector2(Motor.Facing, 0f), new Color(1f, 0.8f, 0.7f, 0.9f), 6, 16f, 0.16f);
                if (HitFeel.Instance != null) HitFeel.Instance.SkillHit();
            }
        }
    }
}
