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
