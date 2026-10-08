using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>대상 머리 위에 따라다니는 체력바 (콜라이더 없음).</summary>
    public class WorldBar : MonoBehaviour
    {
        private Transform _follow;
        private Func<float> _ratio;
        private Transform _fill;
        private float _width;

        public static WorldBar Create(Transform follow, float width, Color fillColor, Func<float> ratio)
        {
            var root = new GameObject("WorldBar");
            var bar = root.AddComponent<WorldBar>();
            bar._follow = follow;
            bar._ratio = ratio;
            bar._width = width;

            var bg = Box.Create("Bg", Vector2.zero, new Vector2(width + 0.06f, 0.16f), new Color(0f, 0f, 0f, 0.7f), 40);
            bg.transform.SetParent(root.transform, false);
            var fill = Box.Create("Fill", Vector2.zero, new Vector2(width, 0.1f), fillColor, 41);
            fill.transform.SetParent(root.transform, false);
            bar._fill = fill.transform;
            bar.LateUpdate();
            return bar;
        }

        private void LateUpdate()
        {
            if (_follow == null)
            {
                Destroy(gameObject);
                return;
            }

            float top = _follow.position.y + _follow.lossyScale.y * 0.5f;
            transform.position = new Vector3(_follow.position.x, top + 0.3f, 0f);

            float r = Mathf.Clamp01(_ratio());
            _fill.localScale = new Vector3(_width * r, _fill.localScale.y, 1f);
            _fill.localPosition = new Vector3(-_width * (1f - r) * 0.5f, 0f, 0f);
        }
    }
}
