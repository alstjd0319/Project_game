using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>경험치 / 레벨업 (기획서 5장). 증강은 폐지됐다.</summary>
    public class ProgressionTests
    {
        private PlayerLevel _level;
        private EnemyController _melee;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();

            _level = Object.FindAnyObjectByType<PlayerLevel>();
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
            Assert.IsFalse(GameManager.InputBlocked, "레벨업해도 게임이 멈추지 않는다 (증강 선택 창 폐지)");
            Assert.AreEqual(1f, Time.timeScale);
            yield return null;
        }

    }
}
