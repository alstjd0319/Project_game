using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>퍼펙트 방어(연출 전용)와 도트 아트 적용 검증.</summary>
    public class PerfectDefenseTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private PlayerCombat _combat;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            _defense = Object.FindAnyObjectByType<PlayerDefense>();
            _party = _defense.GetComponent<PlayerParty>();
            _combat = _defense.GetComponent<PlayerCombat>();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        private void SpawnAttack()
        {
            Vector2 p = _defense.transform.position;
            EnemyAttack.Spawn(null, AttackType.Normal, p + new Vector2(2f, 0f), Vector2.left, new Vector2(0.6f, 0.6f),
                40f, 6f, Color.red, 10);
        }

        [UnityTest]
        public IEnumerator 입력_직후_닿으면_퍼펙트_늦게_닿으면_일반()
        {
            bool perfect = false;
            _combat.DefenseSucceeded += s => perfect = s.Perfect;

            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(); // 40/s, 2유닛 → 약 0.04초 안에 닿음 (< 0.08)
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(perfect, "입력 직후 닿았는데 퍼펙트가 아님");

            yield return new WaitForSecondsRealtime(0.6f);
            Time.timeScale = 1f;
            perfect = true;
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSecondsRealtime(0.15f);
            SpawnAttack();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(perfect, "0.08초 넘게 지나 닿았는데 퍼펙트로 처리됨");
        }

        [UnityTest]
        public IEnumerator 퍼펙트_패링은_히트스탑이_걸리고_끝나면_시간이_복귀한다()
        {
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack();
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.AreEqual(0f, Time.timeScale, "퍼펙트 패링인데 히트스탑이 걸리지 않음");

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "연출이 끝났는데 timeScale이 복귀하지 않음");
            Assert.IsNull(GameObject.Find("Fx_Dim") is { activeSelf: true } ? GameObject.Find("Fx_Dim") : null, "하이라이트 오버레이가 남아 있음");
        }

        [UnityTest]
        public IEnumerator 근접_원거리_몬스터_모두_도트_애니메이션을_쓴다()
        {
            foreach (var kind in new[] { EnemyKind.Melee, EnemyKind.Ranged })
            {
                var enemy = Object.FindObjectsByType<EnemyController>().First(e => e.Kind == kind);
                var art = enemy.GetComponent<EnemySprite>();
                Assert.IsNotNull(art, kind.ToString());
                yield return null;
                Assert.IsTrue(art.IsShowingArt, kind + " 그림이 보임");
                Assert.IsFalse(enemy.GetComponent<SpriteRenderer>().enabled, kind + " 판정 네모는 숨김");
            }
        }

        [UnityTest]
        public IEnumerator 플레이어_일반공격은_제자리에서_벤다_내딛지_않는다()
        {
            // (예전엔 칼을 따라 0.45유닛 내딛었다 — 사용자 요청으로 없앰)
            var motor = _defense.GetComponent<PlayerMotor>();
            TestScene.SetAttackSkillFree(true);
            motor.SetFacing(1f);
            float x0 = _defense.transform.position.x;
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(x0, _defense.transform.position.x, 0.02f, "제자리");
            Assert.IsFalse(motor.IsDashInvulnerable);
        }
    }
}
