using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>기획서 3장 / 4장 / 8장 판정 규칙 검증.</summary>
    public class CoreLoopTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private PlayerCombat _combat;
        private SkillGauge _gauge;
        private EnemyController _melee;

        private Vector2 PlayerPos => _defense.transform.position;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();

            _defense = Object.FindAnyObjectByType<PlayerDefense>();
            _party = _defense.GetComponent<PlayerParty>();
            _combat = _defense.GetComponent<PlayerCombat>();
            _gauge = _defense.GetComponent<SkillGauge>();

            // 적 AI 정지 (테스트에서 공격을 직접 생성)
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = false;
            _melee = enemies.First(e => e.Kind == EnemyKind.Melee);
            foreach (var e in enemies) Place(e, 15f);

            yield return new WaitForSeconds(0.3f); // 착지 대기
        }

        private void Place(EnemyController enemy, float dxFromPlayer)
        {
            var pos = new Vector3(PlayerPos.x + dxFromPlayer, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        private EnemyAttack SpawnAttack(AttackType type, EnemyController source, float fromDx = 2f)
        {
            var origin = new Vector2(PlayerPos.x + fromDx, PlayerPos.y);
            return EnemyAttack.Spawn(source, type, origin, new Vector2(-Mathf.Sign(fromDx), 0f), new Vector2(0.6f, 0.6f),
                10f, 6f, Color.red, type == AttackType.Heavy ? 20 : 10);
        }

        [UnityTest]
        public IEnumerator 일반공격_방어활성중_닿으면_성공_게이지1_방어즉시종료()
        {
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, null);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.AreEqual(100, _party.Hp, "성공 시 데미지 없음");
            Assert.AreEqual(1, _gauge.Value, "일반 성공 +1칸");
            Assert.AreEqual(DefenseState.Ready, _defense.State, "성공하면 활성 즉시 종료, 헛스윙 쿨타임 없음");
            Assert.AreEqual(CharacterKind.Archer, _party.Current.kind, "어떤 공격이든 방어 성공하면 스왑");
        }

        [UnityTest]
        public IEnumerator 방어없이_맞으면_피격_쿨타임없음()
        {
            SpawnAttack(AttackType.Normal, null);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.AreEqual(93, _party.Hp, "전사 방어 패시브: 10 → 7");
            Assert.AreEqual(0, _gauge.Value);
            Assert.AreEqual(DefenseState.Ready, _defense.State);
        }

        [UnityTest]
        public IEnumerator 헛스윙_0점3초후_1초쿨타임()
        {
            Assert.AreEqual(0.3f, _defense.ActiveTime, 1e-4f);
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(DefenseState.Active, _defense.State);

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(DefenseState.Cooldown, _defense.State);
            Assert.IsFalse(_defense.TryActivate(), "쿨타임 중 방어 불가");
            Assert.AreEqual(100, _party.Hp, "헛스윙은 데미지 없음");

            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(DefenseState.Ready, _defense.State);
        }

        [UnityTest]
        public IEnumerator 강공격_성공_스왑후_등장캐릭터가_반격()
        {
            Place(_melee, 6f); // 전사 반격 사거리(1.8) 밖 → 궁수 투사체만 닿을 수 있음
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Heavy, _melee);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.AreEqual(CharacterKind.Archer, _party.Current.kind, "전사가 강공격 패링 → 궁수로 스왑");
            Assert.AreEqual(3, _gauge.Value, "강공격 성공 +3칸");
            Assert.AreEqual(100, _party.Hp);

            yield return new WaitForSeconds(1.5f); // 궁수 반격 투사체(8u/s) 도달 대기
            Assert.AreEqual(0.80f, _melee.HpRatio, 1e-4f, "등장한 궁수의 원거리 반격이 명중 — 거리와 무관하게 20");
        }

        [UnityTest]
        public IEnumerator 전사_반격은_사거리_1점8_안쪽만()
        {
            // 전사 반격은 궁수가 회피에 성공해 전사가 등장하는 순간 나간다
            yield return SwapToArcher();
            Place(_melee, 1.5f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(0.7f, _melee.HpRatio, 1e-4f, "사거리 안: 반격 20 × 전사 공격력 1.5 = 30 명중");

            yield return new WaitForSecondsRealtime(1.0f); // 후퇴·스왑 무적 종료
            yield return SwapToArcher();
            Place(_melee, 3.5f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(0.7f, _melee.HpRatio, 1e-4f, "사거리 밖: 반격 빗나감");
            Assert.AreEqual(2, _gauge.Value);
        }

        [UnityTest]
        public IEnumerator A공격스킬은_기본으로_게이지없이_발동_쿨타임만_적용()
        {
            TestScene.SetAttackSkillFree(true);
            Assert.AreEqual(0, _gauge.Value);
            Assert.IsTrue(_combat.TryAttackSkill(), "게이지 0이어도 발동");
            Assert.AreEqual(0, _gauge.Value);
            Assert.IsFalse(_combat.TryAttackSkill(), "쿨타임 중");
            yield return new WaitForSeconds(1.1f);
            Assert.IsTrue(_combat.TryAttackSkill(), "쿨타임이 지나면 다시");
        }

        [UnityTest]
        public IEnumerator 수동스왑_게이지10칸_소모_및_스왑무적()
        {
            Assert.IsFalse(_party.TryManualSwap(), "게이지 부족 시 불가");
            _gauge.Add(12);
            Assert.IsTrue(_party.TryManualSwap());
            Assert.AreEqual(CharacterKind.Archer, _party.Current.kind);
            Assert.AreEqual(2, _gauge.Value);
            Assert.IsTrue(_party.IsInvulnerable);

            SpawnAttack(AttackType.Normal, null, 1.0f); // 0.3초 안에 도달
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(100, _party.Hp, "스왑 무적 중에는 데미지 없음");

            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(_party.IsInvulnerable);
            SpawnAttack(AttackType.Normal, null);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(90, _party.Hp, "무적 끝나면 정상 피격 (궁수는 피해 감소 없음)");
        }

        [UnityTest]
        public IEnumerator 전사는_받는_피해가_줄고_궁수는_그대로()
        {
            SpawnAttack(AttackType.Heavy, null);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(86, _party.Hp, "전사: 강공격 20 → 30% 감소 → 14");

            _gauge.Add(10);
            Assert.IsTrue(_party.TryManualSwap());
            yield return new WaitForSeconds(0.4f); // 스왑 무적이 끝날 때까지
            SpawnAttack(AttackType.Heavy, null);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(66, _party.Hp, "궁수: 감소 없이 20");
        }

        [UnityTest]
        public IEnumerator 전사_공격_스킬은_공격력_배율이_적용된다()
        {
            Place(_melee, 1.5f);
            _gauge.Add(3);
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return null;
            Assert.AreEqual(1f - 22f / 100f, _melee.HpRatio, 1e-4f, "일반공격 15 × 1.5 = 22.5 → 22");
        }
        private IEnumerator SwapToArcher()
        {
            _gauge.Add(10);
            Assert.IsTrue(_party.TryManualSwap());
            Assert.AreEqual(CharacterKind.Archer, _party.Current.kind);
            yield return new WaitForSeconds(0.4f); // 스왑 무적 종료
        }

        [UnityTest]
        public IEnumerator 궁수_반격은_가까우면_기본_데미지()
        {
            // 전사가 방어에 성공하면 궁수가 등장하며 반격한다
            Place(_melee, 2f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(0.8f, _melee.HpRatio, 1e-4f, "거리 2 → 20");
        }

        [UnityTest]
        public IEnumerator 궁수_반격은_거리와_무관하게_같은_데미지()
        {
            Place(_melee, 9f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(2.5f); // 6유닛/초로 9유닛
            Assert.AreEqual(0.8f, _melee.HpRatio, 1e-4f, "거리 9여도 20 (사거리별 데미지 없음)");
        }

        [UnityTest]
        public IEnumerator 궁수는_전사보다_빠르다()
        {
            var motor = _defense.GetComponent<PlayerMotor>();
            float warriorSpeed = motor.MoveSpeed;
            yield return SwapToArcher();
            Assert.Greater(motor.MoveSpeed, warriorSpeed);
            Assert.AreEqual(GameTuning.Current.archerMoveSpeed, motor.MoveSpeed, 1e-4f);
        }
        [UnityTest]
        public IEnumerator 이동스킬_쿨타임은_1초()
        {
            _gauge.Add(5);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_combat.TryMoveSkill(), "0.5초 뒤엔 아직 쿨타임");
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(_combat.TryMoveSkill(), "1초가 지나면 다시 사용 가능");
            Assert.AreEqual(3, _gauge.Value, "두 번 써서 2칸 소모 (실패한 시도는 소모 없음)");
        }
        [UnityTest]
        public IEnumerator 패링_성공하면_공격이_그자리에서_부서진다()
        {
            Assert.IsTrue(_defense.TryActivate());
            var attack = SpawnAttack(AttackType.Normal, null);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(1, _gauge.Value, "방어 성공");
            Assert.IsTrue(attack == null, "패링: 막힌 공격은 즉시 사라짐");
        }

        [UnityTest]
        public IEnumerator 회피_성공하면_공격이_판정없이_몸을_통과해_지나간다()
        {
            yield return SwapToArcher();
            Assert.IsTrue(_defense.TryActivate());
            var attack = SpawnAttack(AttackType.Normal, null);

            float t = 0f;
            while (attack != null && !attack.IsGhost && t < 1f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(attack != null && attack.IsGhost, "회피: 공격이 사라지지 않고 흘려보내짐");
            Assert.IsFalse(attack.GetComponent<BoxCollider2D>().enabled, "흘려보낸 공격은 판정이 꺼져 있음");

            float x0 = attack.transform.position.x;
            yield return new WaitForSecondsRealtime(0.3f);
            if (attack != null) Assert.Less(attack.transform.position.x, x0, "원래 방향으로 계속 지나감");

            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(attack == null, "잠시 뒤 사라짐");
            Assert.AreEqual(100, _party.Hp, "몸을 통과해도, 그 뒤에도 다시 맞지 않음");
            Assert.AreEqual(1, _gauge.Value, "스왑에 10칸 쓴 뒤 회피 성공 +1");
        }
        [UnityTest]
        public IEnumerator 대시_중에는_무적이라_공격이_통과한다()
        {
            _gauge.Add(1);
            Assert.IsTrue(_combat.TryMoveSkill());
            var attack = SpawnAttack(AttackType.Heavy, null, 0.72f); // 대시 도중 바로 닿음
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.AreEqual(100, _party.Hp, "대시 중 피격 무시");
            Assert.IsTrue(attack == null || attack.IsGhost, "공격은 판정 없이 통과");
            Assert.AreEqual(0, _gauge.Value, "무적 통과는 방어 성공이 아님 — 게이지 없음");
            Assert.AreEqual(CharacterKind.Warrior, _party.Current.kind, "강공격이어도 스왑 없음");
        }

        [UnityTest]
        public IEnumerator 대시가_끝나면_다시_맞는다()
        {
            _gauge.Add(1);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.35f); // 대시(0.15초) 종료 후
            SpawnAttack(AttackType.Normal, null, 0.72f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(93, _party.Hp, "대시 끝난 뒤엔 정상 피격 (전사 방어 패시브 적용)");
        }
        [UnityTest]
        public IEnumerator 스킬은_게이지가_있어야_발동()
        {
            Assert.IsFalse(_combat.TryAttackSkill(), "공격 스킬 3칸 필요");
            Assert.IsFalse(_combat.TryMoveSkill(), "이동 스킬 1칸 필요");

            _gauge.Add(4);
            Assert.IsTrue(_combat.TryAttackSkill());
            Assert.AreEqual(1, _gauge.Value);
            Assert.IsFalse(_combat.TryAttackSkill(), "쿨타임 중");

            Assert.IsTrue(_combat.TryMoveSkill());
            Assert.AreEqual(0, _gauge.Value);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 스킬바용_발동_실패_이벤트()
        {
            var used = new System.Collections.Generic.List<PlayerAction>();
            var denied = new System.Collections.Generic.List<PlayerAction>();
            _combat.ActionUsed += used.Add;
            _combat.ActionDenied += denied.Add;
            _defense.Activated += () => used.Add(PlayerAction.Defend);
            _defense.ActivateDenied += () => denied.Add(PlayerAction.Defend);
            _party.SwapEntered += (_, _) => used.Add(PlayerAction.Swap);
            _party.SwapDenied += () => denied.Add(PlayerAction.Swap);

            _combat.TryAttackSkill();   // 게이지 부족
            _party.TryManualSwap();     // 게이지 부족
            _gauge.Add(13);
            _combat.TryAttackSkill();   // 성공
            _combat.TryAttackSkill();   // 쿨타임
            _party.TryManualSwap();     // 성공
            _defense.TryActivate();     // 성공
            _defense.TryActivate();     // 이미 활성

            CollectionAssert.AreEqual(new[] { PlayerAction.Attack, PlayerAction.Swap, PlayerAction.Defend }, used);
            CollectionAssert.AreEqual(
                new[] { PlayerAction.Attack, PlayerAction.Swap, PlayerAction.Attack, PlayerAction.Defend }, denied);
            yield return null;
        }
    }
}
