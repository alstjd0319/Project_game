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
        /// <summary>D = 스킬 1 (봉 휩쓸기 / 부적 부채꼴), F = 스킬 2 (봉 내려찍기 / 부적 봉인진). 도력을 쓴다.</summary>
        public const KeyCode Skill1 = KeyCode.D;
        public const KeyCode Skill2 = KeyCode.F;
        /// <summary>E = 상호작용 (사당의 포탈·NPC).</summary>
        public const KeyCode Interact = KeyCode.E;
        public const KeyCode Confirm = KeyCode.Return;
        public const KeyCode Pause = KeyCode.Escape;
        public const KeyCode Restart = KeyCode.R;
        /// <summary>M = 시작 화면으로 (일시정지·게임오버 화면에서).</summary>
        public const KeyCode MainMenu = KeyCode.M;
        /// <summary>H = 사당으로 (플레이 씬의 일시정지·게임오버 화면에서).</summary>
        public const KeyCode Hub = KeyCode.H;

        // 보조 창 (평소엔 접혀 있음)
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
