using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>사당 스킬 트리: 레벨업 포인트로 체력·공격력·자원 스탯을 올린다.</summary>
    public class SkillTreeTests
    {
        private static PlayerParty Party => Object.FindAnyObjectByType<PlayerParty>();
        private static PlayerLevel Level => Object.FindAnyObjectByType<PlayerLevel>();

        private static int Idx(string id) => System.Array.FindIndex(SkillTreeState.Nodes, n => n.Id == id);

        [UnityTest]
        public IEnumerator 포인트는_레벨에서_나오고_모자라면_못_익힌다()
        {
            yield return TestScene.LoadHub();
            Assert.AreEqual(0, SkillTreeState.Available(Level.Level), "Lv.1은 포인트 0");
            Assert.IsFalse(SkillTreeState.TryBuy(Idx("hp1"), Level.Level));
            Level.AddXp(Level.XpToNext);
            Assert.AreEqual(1, SkillTreeState.Available(Level.Level));
            Assert.IsTrue(SkillTreeState.TryBuy(Idx("hp1"), Level.Level));
            Assert.AreEqual(0, SkillTreeState.Available(Level.Level));
            Assert.AreEqual(1, SkillTreeState.Rank(Idx("hp1")));
        }

        [UnityTest]
        public IEnumerator 앞_노드를_익혀야_다음_노드가_열리고_최대_랭크를_넘지_못한다()
        {
            yield return TestScene.LoadHub();
            Level.Restore(30, 0);
            Assert.IsFalse(SkillTreeState.CanBuy(Idx("mel2"), 30), "앞 노드 전엔 잠김");
            Assert.IsTrue(SkillTreeState.TryBuy(Idx("mel1"), 30));
            Assert.IsTrue(SkillTreeState.CanBuy(Idx("mel2"), 30));
            for (int i = 0; i < 5; i++) SkillTreeState.TryBuy(Idx("mel1"), 30);
            Assert.AreEqual(3, SkillTreeState.Rank(Idx("mel1")), "최대 랭크 3");
        }

        [UnityTest]
        public IEnumerator 체력_노드는_최대_체력과_현재_체력을_올린다()
        {
            yield return TestScene.LoadHub();
            Level.Restore(5, 0);
            int max = Party.MaxHp, hp = Party.Hp;
            var panel = SkillTreePanel.Open("만신");
            Assert.AreEqual(0, SkillTreePanel.SelectedIndex);
            yield return null;
            Assert.IsTrue(panel.TryBuySelected());
            Assert.AreEqual(max + 10, Party.MaxHp);
            Assert.AreEqual(hp + 10, Party.Hp, "늘어난 만큼 체력도 찬다");
            SkillTreePanel.Close();
        }

        [UnityTest]
        public IEnumerator 초반엔_약하게_시작하고_스킬_트리로_강해진다()
        {
            yield return TestScene.LoadHub();
            Assert.AreEqual(0.7f, new GameTuning().startDamageScale, 0.001f, "기본 초반 배율 0.7");
            GameTuning.Current.startDamageScale = 0.7f;
            Level.Restore(30, 0);
            var hit = HitInfo.Create(DamageSource.AttackSkill, CharacterKind.Player, Vector2.zero);
            float weapon = GameTuning.Current.playerAttackMultiplier;
            Assert.AreEqual(Mathf.RoundToInt(100 * 0.7f * weapon), DamageCalc.Compute(100, hit, null), 1, "처음엔 30% 약하다");
            foreach (var id in new[] { "mel1", "mel1", "mel1", "mel2", "mel2", "mel3" }) SkillTreeState.TryBuy(Idx(id), 30);
            Assert.AreEqual(2.1f, SkillTreeState.MeleeAttackMultiplier, 0.001f, "근접 줄을 다 익히면 +110%");
            Assert.Greater(DamageCalc.Compute(100, hit, null), Mathf.RoundToInt(100 * weapon * 1.4f), "다 키우면 예전(배율 1) 기준보다 강하다");
        }

        [UnityTest]
        public IEnumerator 공격력_경험치_도력_보너스가_계산에_반영된다()
        {
            yield return TestScene.LoadHub();
            Level.Restore(30, 0);
            var hit = HitInfo.Create(DamageSource.AttackSkill, CharacterKind.Player, Vector2.zero);
            int before = DamageCalc.Compute(100, hit, null);
            SkillTreeState.TryBuy(Idx("mel1"), 30); SkillTreeState.TryBuy(Idx("mel1"), 30); // +20%
            Assert.AreEqual(Mathf.RoundToInt(before * 1.20f), DamageCalc.Compute(100, hit, null), 1);
            var ranged = HitInfo.Create(DamageSource.AttackSkill, CharacterKind.Player, Vector2.zero);
            ranged.Ranged = true;
            Assert.AreEqual(1f, SkillTreeState.RangedAttackMultiplier, 0.001f, "근접 트리는 원거리에 영향이 없다");
            int rangedBefore = DamageCalc.Compute(100, ranged, null);
            SkillTreeState.TryBuy(Idx("rng1"), 30); // 원거리 +10%
            Assert.AreEqual(Mathf.RoundToInt(rangedBefore * 1.10f), DamageCalc.Compute(100, ranged, null), 1);
            Assert.AreEqual(Mathf.RoundToInt(before * 1.20f), DamageCalc.Compute(100, hit, null), 1, "원거리 트리는 근접에 영향이 없다");
            SkillTreeState.TryBuy(Idx("res1"), 30);
            Assert.AreEqual(1.1f, SkillTreeState.XpMultiplier, 0.001f);
            SkillTreeState.TryBuy(Idx("res2"), 30);
            Assert.AreEqual(1, SkillTreeState.GaugeGainBonus);
        }

        [UnityTest]
        public IEnumerator 익힌_스탯은_플레이_씬으로_이어지고_새_게임에서는_비워진다()
        {
            yield return TestScene.LoadHub();
            Level.Restore(10, 0);
            SkillTreeState.TryBuy(Idx("hp1"), 10);
            SkillTreeState.TryBuy(Idx("res1"), 10);
            SkillTreeState.TryBuy(Idx("res2"), 10);
            SkillTreeState.TryBuy(Idx("res3"), 10);
            Party.SetMaxHpBonus(SkillTreeState.MaxHpBonus, true);
            GameManager.LoadRun();
            for (int i = 0; i < 12 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameManager.RunSceneName; i++) yield return null;
            yield return null; yield return null;
            Assert.AreEqual(110, Party.MaxHp, "최대 체력 +10 이어짐");
            Assert.AreEqual(Party.MaxHp, Party.Hp);
            Assert.AreEqual(3, Object.FindAnyObjectByType<SkillGauge>().Value, "판 시작 도력 +3");
            PlayerSession.Reset();
            Assert.AreEqual(0, SkillTreeState.Spent);
        }
    }
}
