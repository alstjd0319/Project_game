using System;

namespace ParryRL
{
    /// <summary>증강을 쓸 수 있는 캐릭터. 두 캐릭터의 증강 풀은 방향이 겹치지 않게 설계한다 (기획서 2장·5장).</summary>
    public enum AugmentOwner { Common, Warrior }

    public enum AugmentCategory { Defense, Counter, Gauge, Skill, Mobility }

    /// <summary>
    /// 증강 하나의 정의 (이름·설명·중첩). 실제 효과는 <see cref="Effect"/> 타입의 컴포넌트가 담당한다.
    /// </summary>
    public sealed class AugmentDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public AugmentOwner Owner;
        public AugmentCategory Category;
        public int MaxStacks = 1;

        /// <summary>효과 컴포넌트 (<see cref="AugmentEffect"/> 상속). null이면 효과 미구현.</summary>
        public Type Effect;

        public bool IsImplemented => Effect != null;

        public string OwnerLabel => Owner switch
        {
            AugmentOwner.Warrior => "전사",
            _ => "공용",
        };

        public string CategoryLabel => Category switch
        {
            AugmentCategory.Defense => "방어 판정",
            AugmentCategory.Counter => "반격 강화",
            AugmentCategory.Gauge => "게이지",
            AugmentCategory.Skill => "스킬 강화",
            _ => "이동 스킬",
        };
    }
}
