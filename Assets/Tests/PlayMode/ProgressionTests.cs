using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>경험치 / 레벨업 / 증강 선택 흐름 (기획서 5장).</summary>
    public class ProgressionTests
    {
        private PlayerLevel _level;
        private AugmentManager _augments;
        private EnemyController _melee;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();

            _level = Object.FindAnyObjectByType<PlayerLevel>();
            _augments = _level.GetComponent<AugmentManager>();
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = false;
            _melee = enemies.First(e => e.Kind == EnemyKind.Melee);
        }

        [UnityTest]
        public IEnumerator 몬스터_처치시_경험치_획득()
        {
            int reward = _melee.XpReward;
            Assert.Greater(reward, 0);

            _melee.TakeDamage(9999, 1f, false);
            yield return null;

            Assert.AreEqual(reward, _level.Xp);
            Assert.AreEqual(1, _level.Level);
        }

        [UnityTest]
        public IEnumerator 필요경험치_채우면_레벨업_초과분_이월_연속레벨업()
        {
            var ups = new List<int>();
            _level.LeveledUp += ups.Add;

            Assert.AreEqual(20, _level.XpToNext);
            _level.AddXp(19);
            Assert.AreEqual(1, _level.Level);

            _level.AddXp(1);
            Assert.AreEqual(2, _level.Level);
            Assert.AreEqual(0, _level.Xp);
            Assert.AreEqual(30, _level.XpToNext, "레벨마다 필요 경험치 +10");

            _level.AddXp(30 + 40 + 5); // Lv.2→3→4, 5 남음
            Assert.AreEqual(4, _level.Level);
            Assert.AreEqual(5, _level.Xp);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, ups);
            Assert.AreEqual(3, _augments.PendingChoices, "레벨업마다 증강 선택 1개씩 대기");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 레벨업하면_증강3택이_열리고_게임정지_고르면_재개()
        {
            _level.AddXp(20);
            Assert.IsFalse(_augments.IsChoosing, "레벨업 연출 후 약간 늦게 열린다");

            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsTrue(_augments.IsChoosing);
            Assert.AreEqual(3, _augments.Options.Count);
            Assert.AreEqual(3, _augments.Options.Distinct().Count(), "후보는 서로 다름");
            Assert.AreEqual(0f, Time.timeScale, "선택 중 게임 정지");
            Assert.IsTrue(GameManager.InputBlocked, "선택 중 플레이 입력 차단");

            var pick = _augments.Options[1];
            Assert.IsTrue(_augments.Choose(1));
            Assert.AreEqual(1, _augments.Acquired.Count);
            Assert.AreSame(pick, _augments.Acquired[0]);
            Assert.IsFalse(_augments.IsChoosing);
            Assert.AreEqual(1f, Time.timeScale, "선택 후 재개");
            Assert.IsFalse(GameManager.InputBlocked);
        }

        [UnityTest]
        public IEnumerator 여러_레벨이_오르면_선택창이_연달아_열린다()
        {
            _level.AddXp(20 + 30); // Lv.3
            yield return new WaitForSecondsRealtime(0.7f);

            Assert.IsTrue(_augments.IsChoosing);
            Assert.IsTrue(_augments.Choose(0));
            Assert.IsTrue(_augments.IsChoosing, "두 번째 선택이 바로 열림");
            Assert.AreEqual(0f, Time.timeScale);

            Assert.IsTrue(_augments.Choose(0));
            Assert.IsFalse(_augments.IsChoosing);
            Assert.AreEqual(2, _augments.Acquired.Count);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 증강목록창_평소엔_접혀있고_같은_증강은_한줄로_묶는다()
        {
            var panel = AugmentListPanel.Instance;
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.IsVisible, "평소엔 접혀 있음");

            panel.SetVisible(true);
            Assert.AreEqual(0, panel.RowCount);

            Random.InitState(1234);
            _level.AddXp(20 + 30 + 40 + 50); // 4레벨
            yield return new WaitForSecondsRealtime(0.7f);
            while (_augments.IsChoosing) _augments.Choose(0);

            int distinct = _augments.Acquired.Distinct().Count();
            Assert.AreEqual(4, _augments.Acquired.Count);
            Assert.AreEqual(distinct, panel.RowCount, "열려 있는 동안 획득하면 바로 갱신, 같은 증강은 한 줄");

            panel.SetVisible(false);
            Assert.IsFalse(panel.IsVisible);
        }

        [UnityTest]
        public IEnumerator 증강을_다_모아도_목록창은_높이_상한_안에서_스크롤()
        {
            var panel = AugmentListPanel.Instance;
            panel.SetVisible(true);
            float emptyHeight = panel.PanelHeight;

            _level.AddXp(5000);
            yield return new WaitForSecondsRealtime(0.7f);
            int guard = 0;
            while (_augments.IsChoosing && guard++ < 100) _augments.Choose(0);
            yield return null;

            Assert.AreEqual(AugmentCatalog.All.Count, panel.RowCount, "모든 증강이 한 줄씩");
            Assert.IsTrue(panel.IsScrollable, "다 모으면 스크롤");
            Assert.LessOrEqual(panel.PanelHeight, 92f + 330f + 16f + 0.5f, "창 높이는 상한을 넘지 않음");
            Assert.Greater(panel.PanelHeight, emptyHeight);
        }

        [UnityTest]
        public IEnumerator 최대중첩에_도달한_증강은_다시_나오지_않고_다_떨어지면_창을_안연다()
        {
            int totalStacks = AugmentCatalog.All.Sum(a => a.MaxStacks);
            _level.AddXp(5000); // 증강 풀보다 훨씬 많이 레벨업
            Assert.Greater(_level.Level - 1, totalStacks);

            yield return new WaitForSecondsRealtime(0.7f);
            int guard = 0;
            while (_augments.IsChoosing && guard++ < 100) _augments.Choose(0);

            Assert.AreEqual(totalStacks, _augments.Acquired.Count);
            foreach (var def in AugmentCatalog.All)
                Assert.LessOrEqual(_augments.StacksOf(def), def.MaxStacks, def.Id);
            Assert.IsFalse(_augments.IsChoosing);
            Assert.AreEqual(1f, Time.timeScale, "줄 증강이 없으면 게임 재개");
        }
    }
}
