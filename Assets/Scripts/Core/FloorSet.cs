using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 한 구간(사당·1~3구간)의 바닥 타일 묶음. Tools/gwimun_sprites/cut_floor.py가 오린 조각(Assets/Art/Floor)을
    /// PrototypeSceneBuilder가 이 에셋으로 묶는다. 스프라이트 피벗: 윗면·발판은 "땅 표면 줄"(가운데), 속 채움은 위 가운데.
    /// 크기는 <see cref="unitScale"/>(= 90 ÷ 윗면 몸통 높이 px)을 곱해 "바닥 판정 두께 1유닛 = 윗면 타일 몸통 높이"가 되게 맞춘다.
    /// </summary>
    public class FloorSet : ScriptableObject
    {
        public string zoneName;
        [Tooltip("땅 윗면 타일 (이어 붙임) — 판정 윗면에 표면이 닿는다")] public Sprite[] top = new Sprite[0];
        [Tooltip("윗면 아래로 깔리는 속 채움 타일 (없으면 윗면만)")] public Sprite[] fill = new Sprite[0];
        [Tooltip("떠 있는 발판 왼쪽 끝 / 가운데 / 오른쪽 끝 (없으면 발판은 예전 네모)")] public Sprite[] platL = new Sprite[0];
        public Sprite[] platM = new Sprite[0];
        public Sprite[] platR = new Sprite[0];
        [Tooltip("스프라이트(90px/유닛)에 곱할 배율")] public float unitScale = 1f;
        [Tooltip("속 채움이 깊어질수록 어두워지는 정도 (0 = 그대로, 1 = 맨 아래가 거의 검게). 사당처럼 채움 그림이 한 줄뿐일 때 아래가 자연스럽게 가라앉게 한다")] [Range(0f, 1f)] public float fillDarken = 0f;

        public bool HasPlatforms => platL.Length > 0 && platM.Length > 0 && platR.Length > 0;
    }
}
