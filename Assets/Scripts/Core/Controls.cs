using UnityEngine;

namespace ParryRL
{
    /// <summary>조작키 (기획서 4.5 확정안).</summary>
    public static class Controls
    {
        public const KeyCode Left = KeyCode.LeftArrow;
        public const KeyCode Right = KeyCode.RightArrow;
        public const KeyCode Jump = KeyCode.Space;
        /// <summary>Q = 근접 ↔ 원거리 무기 전환.</summary>
        public const KeyCode Weapon = KeyCode.Q;
        /// <summary>↓ + 점프 = 얇은 발판 아래로 내려가기.</summary>
        public const KeyCode Down = KeyCode.DownArrow;
        public const KeyCode Defend = KeyCode.S;
        public const KeyCode AttackSkill = KeyCode.A;
        public const KeyCode MoveSkill = KeyCode.LeftShift;
        public const KeyCode Confirm = KeyCode.Return;
        public const KeyCode Pause = KeyCode.Escape;
        public const KeyCode Restart = KeyCode.R;

        // 보조 창 (평소엔 접혀 있음)
        public const KeyCode AugmentList = KeyCode.C;
        public const KeyCode TuningPanel = KeyCode.F1;

        public static float Horizontal
        {
            get
            {
                float h = 0f;
                if (Input.GetKey(Left)) h -= 1f;
                if (Input.GetKey(Right)) h += 1f;
                return h;
            }
        }
    }
}
