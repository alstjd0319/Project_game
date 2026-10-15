using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>사당 ↔ 플레이 씬을 넘나들어도 체력·레벨·도력·무기가 이어진다.</summary>
    public class SessionTests
    {
        private static PlayerParty Party => Object.FindAnyObjectByType<PlayerParty>();
        private static PlayerLevel Level => Object.FindAnyObjectByType<PlayerLevel>();
        private static SkillGauge Gauge => Object.FindAnyObjectByType<SkillGauge>();
        private static PlayerCombat Combat => Object.FindAnyObjectByType<PlayerCombat>();

        private static IEnumerator WaitSceneLoaded(string name)
        {
            for (int i = 0; i < 10 && SceneManager.GetActiveScene().name != name; i++) yield return null;
            yield return null;
            yield return null; // Start()에서 이어받은 값이 적용되도록
        }

        /// <summary>사당에서 상태를 만든다: 체력 -20, 레벨 2(경험치 5), 도력 7, 원거리 무기.</summary>
        private static IEnumerator MakeState()
        {
            yield return TestScene.LoadHub();
            yield return new WaitForSeconds(0.2f);
            Party.TakeDamage(30); // 방어 -30% → 21
            Level.AddXp(25);
            Gauge.Add(7);
            Assert.IsTrue(Combat.TrySwitchWeapon());
        }

        [UnityTest]
        public IEnumerator 사당에서_플레이_씬으로_가도_체력_레벨_도력_무기가_이어진다()
        {
            yield return MakeState();
            int hp = Party.Hp, lv = Level.Level, xp = Level.Xp;
            Assert.Less(hp, Party.MaxHp);
            Assert.AreEqual(2, lv);

            GameManager.LoadRun();
            yield return WaitSceneLoaded(GameManager.RunSceneName);

            Assert.AreEqual(hp, Party.Hp, "체력");
            Assert.AreEqual(lv, Level.Level, "레벨");
            Assert.AreEqual(xp, Level.Xp, "경험치");
            Assert.AreEqual(7, Gauge.Value, "도력");
            Assert.AreEqual(WeaponMode.Ranged, Combat.Weapon, "무기");
        }

        [UnityTest]
        public IEnumerator 플레이_씬에서_사당으로_돌아와도_이어진다()
        {
            yield return MakeState();
            GameManager.LoadRun();
            yield return WaitSceneLoaded(GameManager.RunSceneName);
            Gauge.Add(3); // 플레이 씬에서 더 모았다
            int hp = Party.Hp;

            GameManager.LoadHub();
            yield return WaitSceneLoaded(GameManager.HubSceneName);

            Assert.AreEqual(10, Gauge.Value, "플레이 씬에서 모은 도력이 사당까지");
            Assert.Less(hp, Party.MaxHp, "다쳐서 돌아왔지만");
            Assert.AreEqual(Party.MaxHp, Party.Hp, "사당에 들어오면 체력이 자동으로 가득 찬다");
            Assert.AreEqual(2, Level.Level);
            Assert.AreEqual(WeaponMode.Ranged, Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator 사당에서_회복해도_플레이_씬으로_갈_때는_그_체력을_가져간다()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            yield return new WaitForSeconds(0.2f);
            Party.TakeDamage(30);
            GameManager.LoadHub();
            yield return WaitSceneLoaded(GameManager.HubSceneName);
            Assert.AreEqual(Party.MaxHp, Party.Hp);
            Party.TakeDamage(20); // 사당에서 다쳤다 (회복은 들어올 때 한 번)
            int hp = Party.Hp;
            GameManager.LoadRun();
            yield return WaitSceneLoaded(GameManager.RunSceneName);
            Assert.AreEqual(hp, Party.Hp, "플레이 씬에는 사당에서의 체력 그대로 (자동 회복은 사당에 들어올 때만)");
        }

        [UnityTest]
        public IEnumerator 쓰러진_채_사당으로_가면_체력은_가득_차지만_나머지는_이어진다()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            yield return new WaitForSeconds(0.2f);
            Gauge.Add(6);
            Party.TakeDamage(1000);
            Assert.IsTrue(Party.IsDead);

            GameManager.LoadHub();
            yield return WaitSceneLoaded(GameManager.HubSceneName);

            Assert.AreEqual(Party.MaxHp, Party.Hp, "죽어서 돌아오면 체력은 가득");
            Assert.AreEqual(6, Gauge.Value);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 다시_시작하면_이어받은_상태가_비워진다()
        {
            yield return MakeState();
            GameManager.LoadRun();
            yield return WaitSceneLoaded(GameManager.RunSceneName);
            Assert.AreEqual(7, Gauge.Value);

            GameManager.Instance.Restart();
            yield return WaitSceneLoaded(GameManager.RunSceneName);

            Assert.IsFalse(PlayerSession.HasData);
            Assert.AreEqual(0, Gauge.Value);
            Assert.AreEqual(1, Level.Level);
            Assert.AreEqual(Party.MaxHp, Party.Hp);
            Assert.AreEqual(WeaponMode.Melee, Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator 시작_화면으로_나가면_비워지고_새_게임은_처음부터다()
        {
            yield return MakeState();
            GameManager.LoadRun();
            yield return WaitSceneLoaded(GameManager.RunSceneName);

            GameManager.Instance.GoToStartMenu();
            yield return WaitSceneLoaded(GameManager.StartSceneName);
            Assert.IsFalse(PlayerSession.HasData);

            StartMenu.Instance.StartGame();
            yield return WaitSceneLoaded(GameManager.HubSceneName);
            Assert.AreEqual(0, Gauge.Value);
            Assert.AreEqual(1, Level.Level);
            Assert.AreEqual(WeaponMode.Melee, Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator 이어받은_데이터가_없으면_기본값으로_시작한다()
        {
            yield return TestScene.LoadHub();
            Assert.IsFalse(PlayerSession.HasData);
            Assert.AreEqual(Party.MaxHp, Party.Hp);
            Assert.AreEqual(0, Gauge.Value);
            Assert.AreEqual(1, Level.Level);
        }
    }
}
