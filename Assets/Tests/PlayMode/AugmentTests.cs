using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>증강 6개 효과와 "이번 판 보정" 층 (기획서 5.1~5.3).</summary>
    public class AugmentTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private PlayerCombat _combat;
        private SkillGauge _gauge;
        private AugmentManager _augments;
        private EnemyController _melee;
        private EnemyController _ranged;

        private Vector2 PlayerPos => _defense.transform.position;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();

            _defense = Object.FindAnyObjectByType<PlayerDefense>();
            _party = _defense.GetComponent<PlayerParty>();
            _combat = _defense.GetComponent<PlayerCombat>();
            _gauge = _defense.GetComponent<SkillGauge>();
            _augments = _defense.GetComponent<AugmentManager>();

            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = false;
            _melee = enemies.First(e => e.Kind == EnemyKind.Melee);
            _ranged = enemies.First(e => e.Kind == EnemyKind.Ranged);
            foreach (var e in enemies) Place(e, 15f);

            yield return new WaitForSeconds(0.3f); // 착지 대기
        }

        // ───────────── 헬퍼 ─────────────

        private void Grant(string id) => _augments.Grant(AugmentCatalog.Get(id));

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

        /// <summary>방어를 켜고 공격을 맞받는다 (성공).</summary>
        private IEnumerator Defend(AttackType type, EnemyController source = null, float fromDx = 1.2f)
        {
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(type, source, fromDx);
            // 성공 연출(히트스탑·슬로우)이 끝나고 쿨타임 없이 다음 방어를 받을 수 있을 때까지
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // ───────────── 구조 ─────────────

        [UnityTest]
        public IEnumerator 목록의_모든_증강은_효과가_구현되어_있다()
        {
            Assert.AreEqual(6, AugmentCatalog.All.Count);
            Assert.AreEqual(3, AugmentCatalog.All.Count(a => a.Owner == AugmentOwner.Common));
            Assert.AreEqual(3, AugmentCatalog.All.Count(a => a.Owner == AugmentOwner.Player));
            Assert.AreEqual(6, AugmentCatalog.All.Select(a => a.Id).Distinct().Count(), "id 중복 없음");
            foreach (var def in AugmentCatalog.All)
            {
                Assert.IsTrue(def.IsImplemented, def.Name);
                Assert.IsTrue(typeof(AugmentEffect).IsAssignableFrom(def.Effect), def.Name);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator 게이지로_쓰는_딜은_1칸당_기본_10에서_15()
        {
            // 기획서 2장: 게이지를 쓰는 딜은 1칸당 기본 딜 10~15 (캐릭터 패시브·증강 보너스 적용 전 기준)
            float dashSlash = (float)DashSlashAugment.BaseDamage / _combat.MoveSkillCost;
            Assert.That(dashSlash, Is.InRange(10f, 15f), "돌진 베기");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 증강보너스는_더하고_캐릭터패시브에만_곱한다()
        {
            // 플레이어 반격 20 × 패시브 1.5 × (1 + 지척 0.5) = 45 (보너스가 둘 이상이면 더하고, 전부 곱하지 않는다)
            Grant("w_close_strike");
            Place(_melee, 1.3f);
            var hit = HitInfo.Create(DamageSource.Counter, CharacterKind.Player, PlayerPos);
            Assert.AreEqual(45, DamageCalc.Compute(20, hit, _melee));
            yield return null;
        }

        // ───────────── 공용 ─────────────

        [UnityTest]
        public IEnumerator 넓은판정_활성시간_증가_튜닝값은_그대로()
        {
            Assert.AreEqual(0.3f, _defense.ActiveTime, 1e-4f);
            Grant("wide_window");
            Grant("wide_window");
            Assert.AreEqual(0.4f, _defense.ActiveTime, 1e-4f, "0.05 × 2");
            Assert.AreEqual(0.3f, GameTuning.Current.defenseActiveTime, 1e-4f, "증강은 튜닝 값을 바꾸지 않는다");

            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSeconds(0.34f);
            Assert.AreEqual(DefenseState.Active, _defense.State, "기본 0.3초가 지나도 아직 활성");
        }

        [UnityTest]
        public IEnumerator 연쇄방어_3연속마다_게이지2_피격시_끊김()
        {
            Grant("chain_defense");
            for (int i = 0; i < 3; i++) yield return Defend(AttackType.Normal);
            Assert.AreEqual(3 + 2, _gauge.Value, "3연속 성공 → +2칸 보너스");

            yield return Defend(AttackType.Normal);
            yield return Defend(AttackType.Normal);
            SpawnAttack(AttackType.Normal, null, 1.2f); // 피격 → 끊김
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Defend(AttackType.Normal);
            Assert.AreEqual(5 + 3, _gauge.Value, "끊긴 뒤 1연속뿐이라 보너스 없음");
            Assert.AreEqual(1, _defense.GetComponent<ChainDefenseAugment>().Count);
        }

        [UnityTest]
        public IEnumerator 강공격사냥꾼_강공격_성공시_게이지5()
        {
            Grant("heavy_hunter");
            yield return Defend(AttackType.Heavy);
            Assert.AreEqual(5, _gauge.Value);
        }

        // ───────────── 플레이어 ─────────────

        [UnityTest]
        public IEnumerator 기절하면_예비모션중인_공격이_취소된다()
        {
            _melee.enabled = true;
            GameTuning.Current.heavyOnly = true; // 예비 모션 0.45초로 길게
            Place(_melee, 2f);
            float t = 0f;
            while (_melee.transform.localScale.x <= 1.001f && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.Less(t, 5f, "예비 모션 시작");

            _melee.Stun(0.8f);
            Assert.AreEqual(1f, _melee.transform.localScale.x, 1e-4f, "몸 크기 원래대로 (공격 취소)");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, Object.FindObjectsByType<EnemyAttack>().Length, "기절 중엔 공격이 안 나감");
        }

        [UnityTest]
        public IEnumerator 지척의일격_바짝붙은_적에게만_반격50퍼센트()
        {
            Grant("w_close_strike");
            Place(_melee, 1.3f); // 가까운 가장자리 0.8
            yield return Defend(AttackType.Normal, _melee, 1.0f);
            Assert.AreEqual(1f - 45f / 100f, _melee.HpRatio, 1e-4f, "20 × 1.5 × (1 + 0.5) = 45");

            float before = _melee.HpRatio;
            Place(_melee, 2.2f); // 가까운 가장자리 1.7 — 반격 박스(1.8) 안이지만 1.2 밖
            yield return Defend(AttackType.Normal, _melee, 1.0f);
            Assert.AreEqual(before - 0.30f, _melee.HpRatio, 1e-4f, "보너스 없이 30");
        }

        [UnityTest]
        public IEnumerator 피의패링_패링시_회복_강공격6()
        {
            Grant("w_blood_parry");
            SpawnAttack(AttackType.Normal, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(93, _party.Hp);

            yield return Defend(AttackType.Normal);
            Assert.AreEqual(95, _party.Hp, "+2");

            yield return Defend(AttackType.Heavy);
            Assert.AreEqual(100, _party.Hp, "+6 (최대 체력까지만)");
        }

        [UnityTest]
        public IEnumerator 돌진베기_지나간_적에게_1번만_피해()
        {
            Grant("w_dash_slash");
            Place(_melee, 1.8f);
            _gauge.Add(2);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1f - 22f / 100f, _melee.HpRatio, 1e-4f, "15 × 1.5 = 22 — 한 번만");
            Assert.Greater(PlayerPos.x, _melee.transform.position.x, "적을 뚫고 지나감");
        }

    }
}
