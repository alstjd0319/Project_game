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
            Assert.AreEqual(DefenseState.Active, _defense.State, "성공해도 활성은 끝까지 유지 (연속 패링)");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(DefenseState.Ready, _defense.State, "창이 끝나면 헛스윙 쿨타임 없이 바로 준비");
            Assert.AreEqual(CharacterKind.Player, _party.Current.kind);
        }

        [UnityTest]
        public IEnumerator 방어없이_맞으면_피격_쿨타임없음()
        {
            SpawnAttack(AttackType.Normal, null);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.AreEqual(93, _party.Hp, "플레이어 방어 패시브: 10 → 7");
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
        public IEnumerator 강공격_성공_게이지3_플레이어가_그대로_반격()
        {
            Place(_melee, 1.5f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Heavy, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.AreEqual(3, _gauge.Value, "강공격 성공 +3칸");
            Assert.AreEqual(100, _party.Hp);
            Assert.AreEqual(0.7f, _melee.HpRatio, 1e-4f, "플레이어 반격 20 × 1.5 = 30");
        }

        [UnityTest]
        public IEnumerator 플레이어_반격은_사거리_1점8_안쪽만()
        {
            Place(_melee, 1.5f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0.7f, _melee.HpRatio, 1e-4f, "사거리 안: 반격 20 × 플레이어 공격력 1.5 = 30 명중");

            yield return new WaitForSecondsRealtime(0.6f);
            Place(_melee, 3.5f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(0.4f);
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
        public IEnumerator 플레이어는_받는_피해가_30퍼센트_줄어든다()
        {
            SpawnAttack(AttackType.Heavy, null);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(86, _party.Hp, "플레이어: 강공격 20 → 30% 감소 → 14");
        }

        [UnityTest]
        public IEnumerator 플레이어_공격_스킬은_공격력_배율이_적용된다()
        {
            Place(_melee, 1.5f);
            _gauge.Add(3);
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return null;
            Assert.AreEqual(1f - 22f / 100f, _melee.HpRatio, 1e-4f, "일반공격 15 × 1.5 = 22.5 → 22");
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
        public IEnumerator 대시_중에는_무적이라_공격이_통과한다()
        {
            _gauge.Add(1);
            Assert.IsTrue(_combat.TryMoveSkill());
            var attack = SpawnAttack(AttackType.Heavy, null, 0.72f); // 대시 도중 바로 닿음
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.AreEqual(100, _party.Hp, "대시 중 피격 무시");
            Assert.IsTrue(attack == null || attack.IsGhost, "공격은 판정 없이 통과");
            Assert.AreEqual(0, _gauge.Value, "무적 통과는 방어 성공이 아님 — 게이지 없음");
            Assert.AreEqual(CharacterKind.Player, _party.Current.kind, "강공격이어도 캐릭터는 그대로");
        }

        [UnityTest]
        public IEnumerator 대시가_끝나면_다시_맞는다()
        {
            _gauge.Add(1);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.35f); // 대시(0.15초) 종료 후
            SpawnAttack(AttackType.Normal, null, 0.72f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(93, _party.Hp, "대시 끝난 뒤엔 정상 피격 (플레이어 방어 패시브 적용)");
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

            _combat.TryAttackSkill();   // 게이지 부족
            _gauge.Add(13);
            _combat.TryAttackSkill();   // 성공
            _combat.TryAttackSkill();   // 쿨타임
            _defense.TryActivate();     // 성공
            _defense.TryActivate();     // 이미 활성

            CollectionAssert.AreEqual(new[] { PlayerAction.Attack, PlayerAction.Defend }, used);
            CollectionAssert.AreEqual(new[] { PlayerAction.Attack, PlayerAction.Attack, PlayerAction.Defend }, denied);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 패링은_앞뒤_양방향에서_모두_성공하고_공격쪽을_바라본다()
        {
            var motor = _defense.GetComponent<PlayerMotor>();
            motor.SetFacing(1f); // 오른쪽을 보는 중

            // 뒤(왼쪽)에서 온 공격
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, null, -1.2f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(100, _party.Hp, "뒤에서 온 공격도 패링");
            Assert.AreEqual(1, _gauge.Value);
            Assert.AreEqual(-1, motor.Facing, "막은 뒤 공격해 온 쪽을 바라봄");

            yield return new WaitForSecondsRealtime(0.5f);

            // 앞(오른쪽)에서 온 공격
            motor.SetFacing(-1f);
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(100, _party.Hp, "반대편에서 온 공격도 패링");
            Assert.AreEqual(2, _gauge.Value);
            Assert.AreEqual(1, motor.Facing);
        }

        [UnityTest]
        public IEnumerator 연속_패링_한_번의_창_안에서_여러_공격을_모두_막는다()
        {
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.12f);
            SpawnAttack(AttackType.Normal, null, -1.2f); // 같은 활성 창 안에서 반대쪽 공격
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.AreEqual(100, _party.Hp, "두 번째 공격도 막음");
            Assert.AreEqual(2, _gauge.Value, "성공마다 게이지 +1");
        }

        [UnityTest]
        public IEnumerator 연속_패링_막은_직후_다시_누르면_창이_새로_열린다_헛스윙이면_쿨타임()
        {
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(1, _gauge.Value);

            Assert.IsTrue(_defense.TryActivate(), "막은 직후 재입력 허용");
            yield return new WaitForSecondsRealtime(0.2f);
            SpawnAttack(AttackType.Normal, null, 1.2f); // 첫 입력의 창은 이미 끝났을 시점 — 새 창이 막는다
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(100, _party.Hp);
            Assert.AreEqual(2, _gauge.Value);

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSecondsRealtime(0.5f); // 아무것도 안 막은 새 입력은 헛스윙
            Assert.AreEqual(DefenseState.Cooldown, _defense.State);
        }

        [UnityTest]
        public IEnumerator 공격이_동시에_겹쳐_닿아도_둘다_패링된다()
        {
            Assert.IsTrue(_defense.TryActivate());
            // 같은 프레임에 양쪽에서 동시에 + 같은 쪽에서 겹쳐서 하나 더 (일반 둘 · 강공격 하나)
            SpawnAttack(AttackType.Normal, null, 1.2f);
            SpawnAttack(AttackType.Normal, null, -1.2f);
            SpawnAttack(AttackType.Heavy, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(100, _party.Hp, "겹친 공격이 전부 패링됨");
            Assert.AreEqual(1 + 1 + 3, _gauge.Value, "성공마다 게이지 (일반 +1 ×2, 강공격 +3)");
            Assert.AreEqual(0, System.Linq.Enumerable.Count(Object.FindObjectsByType<EnemyAttack>(), a => !a.IsGhost));
        }

        [UnityTest]
        public IEnumerator Q로_무기를_바꾸면_쿨타임_0점5초_게이지_소모_없음()
        {
            Assert.AreEqual(WeaponMode.Melee, _combat.Weapon);
            WeaponMode changed = WeaponMode.Melee;
            _combat.WeaponChanged += w => changed = w;

            Assert.IsTrue(_combat.TrySwitchWeapon());
            Assert.AreEqual(WeaponMode.Ranged, _combat.Weapon);
            Assert.AreEqual(WeaponMode.Ranged, changed);
            Assert.AreEqual(0, _gauge.Value, "전환은 게이지를 쓰지 않음");
            Assert.IsFalse(_combat.TrySwitchWeapon(), "쿨타임 중");

            yield return new WaitForSeconds(0.55f);
            Assert.IsTrue(_combat.TrySwitchWeapon());
            Assert.AreEqual(WeaponMode.Melee, _combat.Weapon, "다시 누르면 근접");
        }

        [UnityTest]
        public IEnumerator 방어_활성_중에는_무기를_못_바꾼다()
        {
            Assert.IsTrue(_defense.TryActivate());
            Assert.IsFalse(_combat.TrySwitchWeapon());
            Assert.AreEqual(WeaponMode.Melee, _combat.Weapon);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 원거리_무기_일반공격은_화살_근접과_다른_스킬()
        {
            TestScene.SetAttackSkillFree(true);
            Assert.IsTrue(_combat.TrySwitchWeapon());
            Assert.AreEqual(12, _combat.AttackSkillBaseDamage, "원거리 일반공격 기본 12");
            Assert.AreEqual(0.45f, _combat.AttackSkillCooldown, 1e-4f, "원거리 쿨타임 0.45");

            _melee.transform.position = new Vector3(PlayerPos.x + 5f * _defense.GetComponent<PlayerMotor>().Facing, _melee.transform.position.y, 0f);
            _melee.GetComponent<Rigidbody2D>().position = _melee.transform.position;
            Physics2D.SyncTransforms();
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerProjectile>(), "화살이 나감 (근접 박스 판정이 아님)");
            Assert.AreEqual(1f, _melee.HpRatio, "근접 박스는 5칸 밖 적에게 닿지 않는다 — 화살은 날아가는 중");

            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1f - 12f / 100f, _melee.HpRatio, 1e-4f, "화살 12 × 원거리 배율 1.0 (근접 배율 1.5가 적용되면 18)");
        }

        [UnityTest]
        public IEnumerator 원거리_무기로_패링하면_반격도_화살이다()
        {
            Assert.IsTrue(_combat.TrySwitchWeapon());
            Place(_melee, 6f); // 근접 반격 사거리(1.8) 밖 — 화살만 닿는다
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(AttackType.Normal, _melee, 1.2f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(100, _party.Hp);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerProjectile>(), "반격 화살");

            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0.8f, _melee.HpRatio, 1e-4f, "반격 20 × 원거리 배율 1.0");
        }
    }
}
