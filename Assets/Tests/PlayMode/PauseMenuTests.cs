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
        public IEnumerator 일시정지_화면에는_계속_다시시작_사당_시작화면_버튼이_있다()
        {
            Assert.AreEqual(5, ButtonsOf("PausePanel").Length, "계속 · 다시 시작 · 사당으로 · 설정 · 시작 화면");
            Assert.AreEqual(3, ButtonsOf("GameOverPanel").Length, "게임오버 화면: 다시 시작 · 사당으로 · 시작 화면");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 시작화면_버튼을_누르면_시작_씬으로_가고_시간이_복귀한다()
        {
            GameManager.Instance.SetPaused(true);
            Assert.AreEqual(0f, Time.timeScale);
            var buttons = ButtonsOf("PausePanel");
            buttons[4].onClick.Invoke();
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
            ButtonsOf("GameOverPanel")[2].onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GameManager.StartSceneName, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator 사당으로_버튼을_누르면_사당으로_가고_시간이_복귀한다()
        {
            GameManager.Instance.SetPaused(true);
            ButtonsOf("PausePanel")[2].onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GameManager.HubSceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNotNull(Object.FindAnyObjectByType<ShrinePortal>(), "사당 씬");
        }

        [UnityTest]
        public IEnumerator 게임오버_화면에서도_사당으로_갈_수_있다()
        {
            GameManager.Instance.TriggerGameOver();
            yield return null;
            ButtonsOf("GameOverPanel")[1].onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GameManager.HubSceneName, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator 사당에서는_사당으로_버튼이_없다()
        {
            yield return TestScene.LoadHub();
            Assert.AreEqual(4, ButtonsOf("PausePanel").Length, "계속 · 다시 시작 · 설정 · 시작 화면");
            Assert.AreEqual(2, ButtonsOf("GameOverPanel").Length);
        }

        private static GameObject PausePanelObject()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                    if (t.name == "PausePanel") return t.gameObject;
            return null;
        }

        [UnityTest]
        public IEnumerator 설정_버튼은_일시정지_메뉴를_숨기고_설정창만_따로_띄운다()
        {
            GameManager.Instance.SetPaused(true);
            Assert.IsTrue(PausePanelObject().activeSelf);
            ButtonsOf("PausePanel")[3].onClick.Invoke(); // 설정
            yield return null;
            Assert.IsTrue(TuningPanel.Instance.IsVisible, "설정 창이 뜬다");
            Assert.IsFalse(PausePanelObject().activeSelf, "일시정지 메뉴는 숨는다 — 창만 보인다");
            Assert.IsTrue(GameManager.Instance.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            Object.FindAnyObjectByType<Hud>().CloseSettings(); // 닫기 / ESC
            Assert.IsFalse(TuningPanel.Instance.IsVisible);
            Assert.IsTrue(PausePanelObject().activeSelf, "일시정지 메뉴로 돌아온다");
            Assert.IsTrue(GameManager.Instance.IsPaused, "계속이 아니라 메뉴로");
        }

        [UnityTest]
        public IEnumerator 일시정지_없이도_설정창을_열면_멈추고_닫으면_일시정지_메뉴가_뜬다()
        {
            Object.FindAnyObjectByType<Hud>().OpenSettings();
            yield return null;
            Assert.IsTrue(GameManager.Instance.IsPaused);
            Assert.IsTrue(TuningPanel.Instance.IsVisible);
            Object.FindAnyObjectByType<Hud>().CloseSettings();
            GameManager.Instance.SetPaused(false);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 스킬_포인트_무제한과_초기화는_설정창_연습탭에서_된다()
        {
            var level = Object.FindAnyObjectByType<PlayerLevel>();
            Assert.AreEqual(1, level.Level);
            Assert.IsFalse(SkillTreeState.TryBuy(0, level.Level), "Lv.1은 포인트 0");
            GameTuning.Current.infiniteSkillPoints = true;
            Assert.IsTrue(SkillTreeState.TryBuy(0, level.Level), "무제한이면 레벨과 상관없이 찍힌다");
            Assert.IsTrue(SkillTreeState.TryBuy(0, level.Level));
            var party = Object.FindAnyObjectByType<PlayerParty>();
            party.SetMaxHpBonus(SkillTreeState.MaxHpBonus, true);
            Assert.AreEqual(120, party.MaxHp);

            // 설정창 연습 탭의 "스킬 트리 초기화" 버튼
            TuningPanel.Instance.SetVisible(true);
            TuningPanel.Instance.SelectTab(4);
            yield return null;
            Button reset = null;
            foreach (var b in TuningPanel.Instance.GetComponentsForTest<Button>())
                if (b.GetComponentInChildren<Text>().text.StartsWith("스킬 트리 초기화")) reset = b;
            Assert.IsNotNull(reset, "초기화 버튼");
            reset.onClick.Invoke();
            Assert.AreEqual(0, SkillTreeState.Spent);
            Assert.AreEqual(100, party.MaxHp);
            GameTuning.Current.infiniteSkillPoints = false;
        }

        [UnityTest]
        public IEnumerator 설정창_캡처()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            Object.FindAnyObjectByType<Hud>().OpenSettings();
            TuningPanel.Instance.SelectTab(4);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return CaptureTests.Capture("settings_window");
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
