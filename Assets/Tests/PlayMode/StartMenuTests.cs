using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>시작 화면: 게임 시작 · 종료하기 두 버튼, 선택·실행, 나중에 항목을 더 붙일 수 있는 구조.</summary>
    public class StartMenuTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameTuning.UseDefaultsForTests();
            SceneManager.LoadScene("Start");
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator 시작_화면에는_게임_시작과_종료하기_두_버튼이_있다()
        {
            var menu = StartMenu.Instance;
            Assert.IsNotNull(menu);
            Assert.AreEqual(2, menu.Entries.Count);
            Assert.AreEqual("게임 시작", menu.Entries[0].Label);
            Assert.AreEqual("종료하기", menu.Entries[1].Label);
            Assert.AreEqual(0, menu.SelectedIndex, "처음엔 게임 시작이 선택돼 있다");
            Assert.IsNotNull(Object.FindAnyObjectByType<ParallaxBackground>(), "배경");
            Assert.AreEqual(0, Object.FindAnyObjectByType<ParallaxBackground>().Zone, "시작 화면은 사당 배경");
            Assert.AreEqual("Start", SceneManager.GetActiveScene().name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 위아래로_선택하고_끝에서는_돌아가지_않는다()
        {
            var menu = StartMenu.Instance;
            menu.MoveSelection(1);
            Assert.AreEqual(1, menu.SelectedIndex);
            menu.MoveSelection(1);
            Assert.AreEqual(1, menu.SelectedIndex, "맨 아래에서 더 내려도 그대로");
            menu.MoveSelection(-1);
            Assert.AreEqual(0, menu.SelectedIndex);
            menu.MoveSelection(-1);
            Assert.AreEqual(0, menu.SelectedIndex);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 게임_시작을_누르면_게임_씬으로_간다()
        {
            StartMenu.Instance.Activate(0);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(StartMenu.GameSceneName, SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerDefense>(), "게임 씬이 열림");
        }

        [UnityTest]
        public IEnumerator 종료하기를_누르면_종료_동작이_불린다()
        {
            int quits = 0;
            var original = StartMenu.QuitHandler;
            StartMenu.QuitHandler = () => quits++;
            try
            {
                StartMenu.Instance.Activate(1);
                Assert.AreEqual(1, quits);
                Assert.AreEqual("Start", SceneManager.GetActiveScene().name, "종료는 씬을 바꾸지 않는다");
            }
            finally { StartMenu.QuitHandler = original; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator 메뉴_항목은_나중에_더_붙일_수_있고_비활성_항목은_건너뛴다()
        {
            var menu = StartMenu.Instance;
            int called = 0;
            var disabled = menu.AddEntry("준비 중", () => called++, enabled: false, hint: "아직 없는 기능");
            var extra = menu.AddEntry("설정", () => called += 10);
            Assert.AreEqual(4, menu.Entries.Count);

            menu.Select(1);
            menu.MoveSelection(1); // 비활성 "준비 중"을 건너뛰고 "설정"으로
            Assert.AreEqual(3, menu.SelectedIndex);
            menu.Activate(2);
            Assert.AreEqual(0, called, "비활성 항목은 실행되지 않는다");
            menu.Activate(3);
            Assert.AreEqual(10, called);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 시작_화면_캡처()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            yield return CaptureTests.Capture("start_menu");
        }
    }
}
