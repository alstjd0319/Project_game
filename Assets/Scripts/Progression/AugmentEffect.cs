using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 증강 효과 하나 = 이 클래스를 상속한 컴포넌트 하나 (기획서 5.2). 플레이어 오브젝트에 붙고,
    /// 필요한 이벤트만 구독하거나 아래 보정값 중 필요한 것만 덮어쓴다. 같은 증강을 또 고르면 <see cref="Stacks"/>만 오른다.
    /// 튜닝 값(GameTuning)은 직접 바꾸지 않는다.
    /// </summary>
    public abstract class AugmentEffect : MonoBehaviour
    {
        public int Stacks { get; private set; }

        protected PlayerParty Party { get; private set; }
        protected PlayerDefense Defense { get; private set; }
        protected PlayerCombat Combat { get; private set; }
        protected PlayerMotor Motor { get; private set; }
        protected SkillGauge Gauge { get; private set; }

        protected virtual void Awake()
        {
            Party = GetComponent<PlayerParty>();
            Defense = GetComponent<PlayerDefense>();
            Combat = GetComponent<PlayerCombat>();
            Motor = GetComponent<PlayerMotor>();
            Gauge = GetComponent<SkillGauge>();
        }

        public void AddStack() => Stacks++;

        // ───────────── 보정값 (필요한 것만 덮어쓴다) ─────────────

        public virtual float DefenseWindowBonus => 0f;
        public virtual float DashDistanceBonus(CharacterKind kind) => 0f;
        public virtual void PrepareHit(ref HitInfo hit) { }
        /// <summary>데미지 보너스 (0.3 = +30%). 다른 증강 보너스와 더해진다.</summary>
        public virtual float DamageBonus(in HitInfo hit, EnemyController target) => 0f;
        /// <summary>데미지 숫자 옆 설명에 쓰는 짧은 이름.</summary>
        public virtual string BonusLabel => GetType().Name;

        // ───────────── 공용 헬퍼 ─────────────

        protected Vector2 Pos => transform.position;

        protected void Say(string text, Color color, float height = 1.5f, float scale = 0.85f) =>
            Hud.WorldText(transform.position + Vector3.up * height, text, color, scale);
    }
}
