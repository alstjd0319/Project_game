using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 한 장소의 배경 묶음 (귀문 에셋 안내.md 4절): 먼 배경(꽉 찬 그림) · 중간(투명 바탕) · 가까운(투명 바탕).
    /// 셋 다 좌우로 이어 붙여 반복할 수 있게 이음새 처리가 돼 있다. PrototypeSceneBuilder가 만든다.
    /// </summary>
    public class BackgroundSet : ScriptableObject
    {
        public string zoneName;
        public Sprite far;
        public Sprite mid;
        public Sprite near;
        [Tooltip("가까운 배경을 주인공 앞(전경)에 둔다. 사당·1구간은 땅 높이 건물이 있어 뒤, 2·3구간은 가장자리 풀뿐이라 앞.")]
        public bool nearInFront;
    }
}
