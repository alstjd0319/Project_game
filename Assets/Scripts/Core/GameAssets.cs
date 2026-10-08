using UnityEngine;

namespace ParryRL
{
    /// <summary>화이트박스 단계에서 모든 오브젝트가 공유하는 1×1 유닛 흰색 정사각형 스프라이트.</summary>
    public class GameAssets : MonoBehaviour
    {
        [SerializeField] private Sprite square;
        [SerializeField, Tooltip("공격 이펙트 그림 (없으면 네모로 표시)")] private AttackFxArt attackFx;

        private static Sprite _square;
        private static AttackFxArt _attackFx;
        private static bool _attackFxLooked;

        /// <summary>공격 이펙트 그림. 씬에 없으면 null → 공격은 네모로 보인다.</summary>
        public static AttackFxArt AttackFx
        {
            get
            {
                if (_attackFx == null && !_attackFxLooked)
                {
                    _attackFxLooked = true;
                    var assets = FindAnyObjectByType<GameAssets>();
                    if (assets != null) _attackFx = assets.attackFx;
                }
                return _attackFx;
            }
        }

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    var assets = FindAnyObjectByType<GameAssets>();
                    _square = assets != null && assets.square != null ? assets.square : CreateRuntimeSquare();
                }
                return _square;
            }
        }

        private void Awake()
        {
            if (square != null) _square = square;
            _attackFx = attackFx;
            _attackFxLooked = true;
        }

        private static Sprite CreateRuntimeSquare()
        {
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
    }
}
