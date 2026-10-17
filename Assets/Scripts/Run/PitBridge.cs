using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 바닥 구멍(함정) 위에 깔린 보이지 않는 다리. 몬스터는 이 위를 걸어 건너지만(바닥 AI가 구멍에 빠지지 않게) 플레이어는 충돌을 무시해서 구멍에 빠진다.
    /// 플레이어의 "바닥에 서 있나" 검사도 이 다리는 바닥으로 치지 않는다 (PlayerMotor.CheckGround).
    /// </summary>
    public class PitBridge : MonoBehaviour
    {
    }
}
