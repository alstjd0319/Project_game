using UnityEngine;

namespace ParryRL
{
    /// <summary>NPC 한 명의 도트 애니메이션 묶음 (만신: 대기 · 대화 · 축복). PrototypeSceneBuilder가 생성한다.</summary>
    public class NpcArt : ScriptableObject
    {
        public SpriteClip idle = new();
        [Tooltip("대화 중 반복")] public SpriteClip talk = new();
        [Tooltip("스킬을 익혔을 때 등 (한 번 재생)")] public SpriteClip bless = new();
    }
}
