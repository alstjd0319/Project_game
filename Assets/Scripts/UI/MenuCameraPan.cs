using UnityEngine;

namespace ParryRL
{
    /// <summary>메뉴 배경이 천천히 흐르도록 카메라를 옆으로 민다 (시차 스크롤이 카메라를 따라간다).</summary>
    public class MenuCameraPan : MonoBehaviour
    {
        [SerializeField] private float speed = 0.8f;

        private void Update() => transform.position += Vector3.right * (speed * Time.unscaledDeltaTime);
    }
}
