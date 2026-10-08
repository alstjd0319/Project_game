using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 몬스터 한 종의 도트 애니메이션 묶음. 프레임은 Tools/sprites/process_sheets.ps1이 아틀라스에서 잘라 만들고,
    /// 이 에셋은 PrototypeSceneBuilder가 생성한다. 그림이 없는 몬스터는 네모로 표시된다.
    /// </summary>
    public class EnemyArt : ScriptableObject
    {
        public SpriteClip idle = new();
        public SpriteClip walk = new();
        [Tooltip("일반 예비 모션 — 예비 모션 길이(반응시간)에 맞춰 한 번 재생 (fps 무시)")] public SpriteClip windup = new();
        [Tooltip("일반 베기 (한 번 재생, 마지막 장면에서 회복 끝까지 멈춤)")] public SpriteClip slash = new();
        [Tooltip("강공격 예비 모션 — 예비 모션 길이에 맞춰 한 번 재생 (fps 무시). 눈이 빨갛게 빛남")] public SpriteClip heavyWindup = new();
        [Tooltip("강공격 베기 (한 번 재생)")] public SpriteClip heavySlash = new();
        [Tooltip("기절 중 반복")] public SpriteClip stun = new();
        [Tooltip("경직 동안 한 번 재생")] public SpriteClip hit = new();
        [Tooltip("죽으면 그 자리에 남아 한 번 재생하고 사라짐")] public SpriteClip death = new();

        [Header("판정이 생기는 장면 (판정-시각 일치)")]
        [Tooltip("베기 박스는 예비 모션이 끝나는 순간 한 번에 뻗으므로, 칼을 뻗은 이 장면부터 재생한다")]
        public int slashHitFrame = 0;
        [Tooltip("강공격 베기 시트의 앞 장면(칼을 뒤로 젖힘)은 준비 자세라 건너뛰고 내려찍는 장면부터")]
        public int heavySlashHitFrame = 3;
    }
}
