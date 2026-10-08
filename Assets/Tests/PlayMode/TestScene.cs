using System.Collections;
using UnityEngine.SceneManagement;

namespace ParryRL.Tests
{
    /// <summary>테스트 공통: 튜닝 파일을 무시하고 기본값으로 프로토타입 씬을 연다.</summary>
    public static class TestScene
    {
        public static IEnumerator LoadPrototype()
        {
            // 사용자가 F1 패널로 바꿔 저장한 값이 테스트 기대값을 흔들지 않도록
            GameTuning.UseDefaultsForTests();
            UnityEngine.Time.timeScale = 1f; // 앞선 테스트(게임오버·히트스탑)가 시간을 멈춘 채 끝나도 영향이 없게
            SceneManager.LoadScene("Prototype");
            yield return null;
            yield return null;
            SetAttackSkillFree(false); // 게이지 규칙 테스트의 기준은 "A 스킬이 게이지를 쓴다" — 공짜 설정은 따로 검증
        }

        public static void SetAttackSkillFree(bool free)
        {
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            typeof(PlayerCombat).GetField("attackSkillFree", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(combat, free);
        }
    }
}
