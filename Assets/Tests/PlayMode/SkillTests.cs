using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>D / F 스킬 (봉 휩쓸기·내려찍기 / 부적 부채꼴·봉인진): 도력 소모·쿨타임·효과.</summary>
    public class SkillTests
    {
        private PlayerCombat _combat;
        private SkillGauge _gauge;
        private PlayerMotor _motor;
        private EnemyController _enemy;
        private Vector2 PlayerPos => _combat.transform.position;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            _combat = Object.FindAnyObjectByType<PlayerCombat>();
            _gauge = _combat.GetComponent<SkillGauge>();
            _motor = _combat.GetComponent<PlayerMotor>();
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) { e.enabled = false; Place(e, 15f); }
            _enemy = enemies.First(e => e.Kind == EnemyKind.Melee);
            yield return new WaitForSeconds(0.3f);
            _motor.FaceTowards(PlayerPos.x + 1f);
        }

        private void Place(EnemyController enemy, float dx)
        {
            var pos = new Vector3(PlayerPos.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        private IEnumerator ComboHit(float enemyDx, System.Action<int> report)
        {
            // 적을 앞에 세워 두고 A를 한 번 친 뒤 줄어든 체력(= 그 단의 데미지)을 알려 준다
            Place(_enemy, enemyDx);
            float before = _enemy.HpRatio;
            Assert.IsTrue(_combat.TryAttackSkill(), "쿨타임이 끝났으니 공격 가능");
            yield return null;
            report(Mathf.RoundToInt((before - _enemy.HpRatio) * 100f));
        }

        [UnityTest]
        public IEnumerator 봉_일반공격은_이어_누르면_1_2_3단으로_커지고_3단은_양옆을_휩쓸며_기절시킨다()
        {
            GameTuning.Current.enemyInvincible = false;
            TestScene.SetAttackSkillFree(true);
            Assert.AreEqual(1, _combat.NextComboStage);
            int d1 = 0, d2 = 0, d3 = 0;
            yield return ComboHit(1.5f, d => d1 = d);
            Assert.AreEqual(2, _combat.NextComboStage, "1단을 치면 다음은 2단");
            yield return new WaitForSeconds(0.4f);
            yield return ComboHit(1.5f, d => d2 = d);
            Assert.AreEqual(3, _combat.NextComboStage);
            yield return new WaitForSeconds(0.4f);
            _enemy.Stun(0f);
            yield return ComboHit(-1.2f, d => d3 = d); // 3단은 플레이어 뒤쪽(반대편) 적도 맞는다
            Assert.AreEqual(1, _combat.NextComboStage, "마무리 뒤에는 다시 1단");
            Assert.Less(d1, d2, "2단이 1단보다 세다");
            Assert.Less(d2, d3, "3단 마무리가 가장 세다");
            Assert.IsTrue(_enemy.IsStunned, "3단은 기절시킨다");
            Assert.Greater(d1, 0);
        }

        [UnityTest]
        public IEnumerator 이어치기_시간이_지나거나_무기를_바꾸면_1단부터_다시()
        {
            TestScene.SetAttackSkillFree(true);
            Assert.IsTrue(_combat.TryAttackSkill());
            Assert.AreEqual(2, _combat.NextComboStage);
            yield return new WaitForSeconds(1.2f); // 쿨타임 0.35 + 이어치기 0.6 이 지난다
            Assert.AreEqual(1, _combat.NextComboStage, "늦으면 1단부터");

            Assert.IsTrue(_combat.TryAttackSkill());
            Assert.AreEqual(2, _combat.NextComboStage);
            yield return new WaitForSeconds(0.1f);
            // 무기를 바꾸면 처음부터 (바꾸기 쿨타임·방어 조건은 통과한다)
            Assert.IsTrue(_combat.TrySwitchWeapon());
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(_combat.TrySwitchWeapon());
            Assert.AreEqual(1, _combat.NextComboStage, "무기를 바꾸면 처음부터");
        }

        [UnityTest]
        public IEnumerator 부적을_들면_단계_없이_화살_한_종류다()
        {
            Assert.IsTrue(_combat.TrySwitchWeapon());
            TestScene.SetAttackSkillFree(true);
            yield return null;
            Assert.IsTrue(_combat.TryAttackSkill());
            Assert.AreEqual(1, _combat.NextComboStage, "원거리는 단계가 없다");
        }

        [UnityTest]
        public IEnumerator D스킬은_도력_3을_쓰고_부족하면_안_나간다()
        {
            Assert.IsFalse(_combat.TrySkill(1), "도력 0 — 발동 불가");
            _gauge.Add(5);
            Assert.IsTrue(_combat.TrySkill(1));
            Assert.AreEqual(2, _gauge.Value, "도력 3 소모");
            Assert.IsFalse(_combat.TrySkill(1), "쿨타임 중");
            yield return null;
        }

        [UnityTest]
        public IEnumerator F스킬은_도력_5를_쓴다()
        {
            _gauge.Add(8);
            Assert.IsTrue(_combat.TrySkill(2));
            Assert.AreEqual(3, _gauge.Value);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 봉_휩쓸기는_앞쪽_넓은_범위를_때리고_기절시킨다()
        {
            Place(_enemy, 3.2f); // 일반공격 박스(2.4)는 닿지 않지만 휩쓸기(4.0)는 닿는다
            _gauge.Add(3);
            Assert.IsTrue(_combat.TrySkill(1));
            Assert.Less(_enemy.HpRatio, 1f, "맞았다");
            Assert.IsTrue(_enemy.IsStunned, "짧게 기절");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 봉_내려찍기는_잠깐_뒤_충격파로_크게_때리고_오래_기절시킨다()
        {
            Place(_enemy, 2.8f);
            _gauge.Add(5);
            Assert.IsTrue(_combat.TrySkill(2));
            Assert.AreEqual(1f, _enemy.HpRatio, "내리꽂는 순간(약 0.08초 뒤) 전에는 아직");
            yield return new WaitForSeconds(0.3f);
            Assert.Less(_enemy.HpRatio, 0.8f, "큰 데미지");
            Assert.IsTrue(_enemy.IsStunned);
        }

        [UnityTest]
        public IEnumerator 부적_부채꼴은_부적을_여러_장_던진다()
        {
            Assert.IsTrue(_combat.TrySwitchWeapon());
            yield return null;
            _gauge.Add(3);
            Assert.IsTrue(_combat.TrySkill(1));
            Assert.AreEqual(5, Object.FindObjectsByType<PlayerProjectile>().Length);
        }

        [UnityTest]
        public IEnumerator 부적_봉인진은_진_안의_적을_묶는다()
        {
            Assert.IsTrue(_combat.TrySwitchWeapon());
            yield return null;
            Place(_enemy, 3.2f); // 진 가운데 = 플레이어 앞 3.2
            _gauge.Add(5);
            Assert.IsTrue(_combat.TrySkill(2));
            Assert.AreEqual(1, Object.FindObjectsByType<SealZone>().Length);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_enemy.IsStunned, "진 안에서 계속 기절");
            yield return new WaitForSeconds(1.0f);
            Assert.Less(_enemy.HpRatio, 1f, "간격마다 약하게 데미지");
        }
    }
}
