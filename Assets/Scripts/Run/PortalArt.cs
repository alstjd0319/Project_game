using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 방 포탈 틀 그림 7종 (1016 에셋 `bg/rooms/portal_frames.png`, 안내.md 2절 순서: 일반 · 엘리트 · 상점 · 보상 · 휴식 · 이벤트 · 보스)과
    /// 틀마다 안쪽 뚫린 부분(소용돌이 자리). <c>Tools/gwimun_sprites/cut_portal.py</c>가 자르고 빌더가 만든다.
    /// </summary>
    public class PortalArt : ScriptableObject
    {
        public const int Normal = 0, Elite = 1, Shop = 2, Reward = 3, Rest = 4, Event = 5, Boss = 6;
        private const float Ppu = 90f;

        [Tooltip("틀 그림 7장 (아래 가운데 피벗)")] public Sprite[] frames = new Sprite[0];
        [Tooltip("틀마다 안쪽 구멍 (그림 픽셀, 왼쪽 위 기준: x0, y0, x1, y1)")] public Vector4[] holes = new Vector4[0];
        [Tooltip("틀마다 구멍 모양 가림막 (틀과 같은 크기·아래 가운데 피벗) — 소용돌이를 구멍 모양대로만 보이게")] public Sprite[] holeMasks = new Sprite[0];

        public Sprite HoleMask(int index) => index >= 0 && index < holeMasks.Length ? holeMasks[index] : null;

        [Tooltip("방 표식 7종 (1016 원본/ui/room_marks.png, 틀과 같은 순서) — 문 안쪽 위에 매달아 글자 없이도 방 종류를 알게 한다")] public Sprite[] marks = new Sprite[0];

        public Sprite Mark(int index) => index >= 0 && index < marks.Length ? marks[index] : null;

        /// <summary>방 종류 → 틀 번호. 마지막 방을 깬 뒤 사당으로 나가는 문은 휴식(서낭나무) 틀.</summary>
        public static int IndexFor(RoomType type, bool isExit)
        {
            if (isExit) return Rest;
            return type switch
            {
                RoomType.Elite => Elite,
                RoomType.Shop => Shop,
                RoomType.Reward => Reward,
                RoomType.Boss => Boss,
                _ => Normal,
            };
        }

        public Sprite Frame(int index) => index >= 0 && index < frames.Length ? frames[index] : null;

        /// <summary>틀 안쪽 구멍: 바닥(그림 아래 가운데)에서 구멍 가운데까지의 높이와 구멍 크기(유닛). 모르면 false.</summary>
        public bool TryHole(int index, out Vector2 center, out Vector2 size)
        {
            center = size = Vector2.zero;
            var sprite = Frame(index);
            if (sprite == null || index >= holes.Length || holes[index] == Vector4.zero) return false;
            var h = holes[index];
            float w = sprite.rect.width, ht = sprite.rect.height;
            center = new Vector2(((h.x + h.z) * 0.5f - w * 0.5f) / Ppu, (ht - (h.y + h.w) * 0.5f) / Ppu);
            size = new Vector2((h.z - h.x) / Ppu, (h.w - h.y) / Ppu);
            return true;
        }
    }
}
