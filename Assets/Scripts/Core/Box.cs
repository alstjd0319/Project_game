using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 흰 정사각형 오브젝트 생성 헬퍼.
    /// 판정-시각 일치 원칙: 스프라이트 1×1 + BoxCollider2D 기본 size(1,1), 크기는 localScale로만 조절.
    /// </summary>
    public static class Box
    {
        public static GameObject Create(string name, Vector2 center, Vector2 size, Color color,
            int sortingOrder = 0, bool withCollider = false)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            if (withCollider) go.AddComponent<BoxCollider2D>();
            return go;
        }
    }
}
