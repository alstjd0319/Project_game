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
        public IEnumerator 퍼펙트_회피는_세상이_멈추고_끝나면_시간이_복귀한다()
        {
            _party.Swap(SwapCause.Manual); // 궁수로
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(CharacterKind.Archer, _party.Current.kind);

            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(0f, Time.timeScale, "퍼펙트 회피인데 세상이 멈추지 않음");

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "연출이 끝났는데 timeScale이 복귀하지 않음");
            Assert.IsNull(GameObject.Find("Fx_Dim") is { activeSelf: true } ? GameObject.Find("Fx_Dim") : null, "하이라이트 오버레이가 남아 있음");
        }

        [UnityTest]
        public IEnumerator 도트_아트가_연결되고_방어_중에는_자세가_바뀐다()
        {
            var sr = _party.GetComponent<SpriteRenderer>();
            Assert.AreEqual("warrior", sr.sprite.name);
            Assert.AreEqual(SpriteDrawMode.Sliced, sr.drawMode);
            Assert.AreEqual(Vector2.one, sr.size, "몸 크기(1×1 × 스케일)와 그림이 어긋남 — 판정-시각 일치 원칙");

            Assert.IsTrue(_defense.TryActivate());
            yield return null;
            yield return null;
            Assert.AreEqual("warrior_parry", sr.sprite.name);

            var enemy = Object.FindObjectsByType<EnemyController>().First(e => e.Kind == EnemyKind.Ranged); // 근접은 도트 애니메이션(EnemySprite)
            Assert.AreEqual("enemy_idle", enemy.GetComponent<SpriteRenderer>().sprite.name);
        }

        [UnityTest]
        public IEnumerator 일반_회피는_시간을_멈추지_않고_뒤로_물러나며_그동안_무적이다()
        {
            _party.Swap(SwapCause.Manual);
            yield return new WaitForSecondsRealtime(0.4f);
            var motor = _defense.GetComponent<PlayerMotor>();
            float startX = _defense.transform.position.x;

            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSecondsRealtime(0.15f); // 0.08초 넘겨서 일반 회피
            SpawnAttack();
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.IsTrue(motor.IsBackstepping, "회피 성공 후 후퇴하지 않음");
            Assert.AreEqual(1f, Time.timeScale, "일반 회피가 시간을 바꿈");
            int hp = _party.Hp;
            SpawnAttack();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(hp, _party.Hp, "후퇴 중 피격됨");
            Assert.IsFalse(_defense.TryActivate(), "후퇴 중 입력을 받음");

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(motor.IsBackstepping);
            Assert.Less(_defense.transform.position.x, startX - 0.3f, "뒤(왼쪽)로 물러나지 않음");
            Assert.AreEqual(Quaternion.identity, _defense.transform.rotation);
        }

        [UnityTest]
        public IEnumerator 전사_일반공격은_칼을_따라_살짝_내딛는다_무적은_아니다()
        {
            var motor = _defense.GetComponent<PlayerMotor>();
            TestScene.SetAttackSkillFree(true);
            motor.SetFacing(1f);
            float x0 = _defense.transform.position.x;
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return new WaitForSeconds(0.3f);
            float moved = _defense.transform.position.x - x0;
            Assert.That(moved, Is.InRange(0.3f, 0.6f), "내딛기 거리 ≈ 0.45");
            Assert.IsFalse(motor.IsDashInvulnerable, "내딛기는 무적이 아님");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(_defense.transform.position.x - x0, moved, 0.05f, "내딛기가 끝나면 멈춤");
        }
    }
}
