using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 궁수 · 거리 유지 — 궁수 이동 스킬(백스텝) 거리 +1.5. (사거리별 데미지 패시브가 없어져 "조준" 효과는 뺐다)
    /// </summary>
    public class KeepDistanceAugment : AugmentEffect
    {
        public const float ExtraDistance = 1.5f;

        public override float DashDistanceBonus(CharacterKind kind) => kind == CharacterKind.Archer ? ExtraDistance : 0f;
    }
}
