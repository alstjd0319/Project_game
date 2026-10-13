using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParryRL.Tests
{
    /// <summary>일시정지·게임오버 화면에서 시작 화면으로 돌아가기.</summary>
    public class PauseMenuTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
        }

        private static Button[] ButtonsOf(string panelName)
        {
            // 비활성 오브젝트도 찾아야 하므로 캔버스 아래를 직접 뒤진다
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                    if (t.name == panelName) return t.GetComponentsInChildren<Button>(true);
            return new Button[0];
        }

        [UnityTest]
        public IEnumerator 일시정지_화면에는_계속_다시시작_시작화면_버튼이_있다()
        {
            Assert.AreEqual(3, ButtonsOf("PausePanel").Length);
            Assert.AreEqual(2, ButtonsOf("GameOverPanel").Length, "게임오버 화면: 다시 시작 · 시작 화면");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 시작화면_버튼을_누르면_시작_씬으로_가고_시간이_복귀한다()
        {
            GameManager.Instance.SetPaused(true);
            Assert.AreEqual(0f, Time.timeScale);
            var buttons = ButtonsOf("PausePanel");
            buttons[2].onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GameManager.StartSceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale, "멈춘 채로 넘어가지 않는다");
            Assert.IsNotNull(StartMenu.Instance, "시작 화면 메뉴");
        }

        [UnityTest]
        public IEnumerator 계속_버튼은_일시정지를_푼다()
        {
            GameManager.Instance.SetPaused(true);
            ButtonsOf("PausePanel")[0].onClick.Invoke();
            Assert.IsFalse(GameManager.Instance.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 게임오버_화면에서도_시작화면으로_갈_수_있다()
        {
            GameManager.Instance.TriggerGameOver();
            yield return null;
            ButtonsOf("GameOverPanel")[1].onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GameManager.StartSceneName, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator 일시정지_화면_캡처()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            GameManager.Instance.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return CaptureTests.Capture("pause_menu");
        }
    }
}
