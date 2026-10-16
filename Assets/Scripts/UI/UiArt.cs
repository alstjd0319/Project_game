using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 귀문 UI 그림 묶음 (체력 틀·채움 바·도력 구슬·스킬 아이콘 …). 시트를 오린 조각(Assets/Art/Ui, Tools/gwimun_sprites/cut_ui.py)을
    /// PrototypeSceneBuilder가 이 에셋으로 묶어 GameAssets에 연결한다. 비어 있으면 UI는 예전 네모·글자로 그려진다.
    /// </summary>
    public class UiArt : ScriptableObject
    {
        [Header("체력·경험치 바")]
        [Tooltip("옥 장식 모서리 틀 (가운데가 어두운 홈)")] public Sprite hpFrame;
        [Tooltip("초록 채움 바")] public Sprite hpFill;
        [Tooltip("구리 테두리 빈 바 (경험치 바 배경)")] public Sprite barEmpty;
        [Tooltip("보라 채움 바 (경험치)")] public Sprite purpleFill;
        [Tooltip("용 장식 큰 틀 — 보스 체력 등 큰 바용 (아직 안 씀)")] public Sprite dragonFrame;

        [Header("도력 구슬")]
        public Sprite orbEmpty;
        public Sprite orbFull;
        [Tooltip("방금 찬 구슬 (반짝)")] public Sprite orbSpark;
        [Tooltip("구슬을 꿰는 매듭 줄 (아직 안 씀)")] public Sprite orbString;

        [Header("스킬 칸 틀·메달")]
        public Sprite slotParchment;
        public Sprite slotActive;
        public Sprite slotLocked;
        public Sprite cooldownDisc;
        [Tooltip("무기 전환 칸에 보이는 메달 (봉 / 부적)")] public Sprite weaponStaff;
        public Sprite weaponTalisman;

        [Header("스킬 트리 창")]
        [Tooltip("두루마리 창")] public Sprite skillScroll;
        [Tooltip("노드 틀: 잠김 · 익힐 수 있음 · 다 익힘 · 선택 표시")] public Sprite nodeLocked;
        public Sprite nodeOpen;
        public Sprite nodeOwned;
        public Sprite nodeSelected;
        [Tooltip("노드 사이 이음 줄 (점선 / 빛나는 줄)")] public Sprite lineDim;
        public Sprite lineGlow;
        public Sprite iconStaff;
        public Sprite iconTalisman;

        [Header("아이콘")]
        public Sprite iconHeart;
        public Sprite iconLantern;
        [Tooltip("스킬 아이콘 12종 (틀 포함). 0 봉 휩쓸기 · 1 봉 찌르기 · 2 내려찍기 · 3 회오리 · 4 부적 바람 · 5 부적 부채꼴 · 6 봉인진 · 7 결계 · 8 분신 · 9 사슬 여우 · 10 도력 상승 · 11 잎 부적")]
        public Sprite[] skillIcons = new Sprite[12];

        public Sprite Skill(int index) => skillIcons != null && index >= 0 && index < skillIcons.Length ? skillIcons[index] : null;
    }
}
