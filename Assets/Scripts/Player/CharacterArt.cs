using System;
using UnityEngine;

namespace ParryRL
{
    [Serializable]
    public class SpriteClip
    {
        public Sprite[] frames = Array.Empty<Sprite>();
        public float fps = 10f;

        public bool IsEmpty => frames == null || frames.Length == 0;
    }

    /// <summary>
    /// 캐릭터 한 명의 도트 애니메이션 묶음. 프레임은 Tools/sprites/process_sheets.ps1이 만들고,
    /// 이 에셋은 PrototypeSceneBuilder가 생성한다. 그림이 없는 캐릭터는 네모로 표시된다.
    /// </summary>
    public class CharacterArt : ScriptableObject
    {
        public CharacterKind kind;
        public SpriteClip idle = new();
        public SpriteClip run = new();
        [Tooltip("방어 활성 시간 동안 한 번 재생 (fps는 무시하고 활성 길이에 맞춘다)")] public SpriteClip parry = new();
        [Tooltip("대시 시간 동안 한 번 재생")] public SpriteClip dash = new();
        [Tooltip("방어 성공 직후 반격 (한 번 재생)")] public SpriteClip counter = new();
        [Tooltip("공격 스킬 (한 번 재생)")] public SpriteClip attack = new();
        [Tooltip("도약·상승·정점·낙하·착지 5장. 공중에서는 세로 속도로 1~3번을 고르고, 착지 순간 4번을 잠깐 보여준다")] public SpriteClip jump = new();
        public SpriteClip hit = new();
        [Tooltip("한 번 재생하고 마지막 장면에서 멈춤")] public SpriteClip death = new();
        [Tooltip("교대로 들어올 때 (한 번 재생)")] public SpriteClip enter = new();
        [Tooltip("점프 그림이 없을 때 공중에서 쓰는 달리기 프레임 번호 (두 발이 뜬 장면)")] public int airRunFrame = 3;
        [Header("판정이 생기는 장면 (판정-시각 일치)")]
        [Tooltip("반격·공격 스킬은 누르는 순간 판정이 생기므로 칼을 뻗은 이 장면부터 재생한다. 앞 장면(준비 자세)은 선딜이 생기면 쓴다")]
        public int counterHitFrame = 1;
        public int attackHitFrame = 2;
        [Tooltip("패링 성공 순간 잠깐 멈춰 보여줄 패링 프레임 번호 (막아서 밀리는 장면)")] public int parryImpactFrame = 2;
    }
}
