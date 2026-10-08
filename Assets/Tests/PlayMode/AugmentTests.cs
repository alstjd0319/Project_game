using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>증강 12개 효과와 "이번 판 보정" 층 (기획서 5.1~5.3).</summary>
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
            // 성공하면 스왑(무적 0.3초) + 회피 후퇴(최대 ~0.7초)가 끝나야 다음 방어를 받는다
            yield return new WaitForSecondsRealtime(0.9f);
        }

        /// <summary>게이지 10칸으로 수동 스왑하고 스왑 무적이 끝날 때까지 기다린다.</summary>
        private IEnumerator SwapTo(CharacterKind kind)
        {
            if (_party.Current.kind == kind) yield break;
            _gauge.Add(10);
            Assert.IsTrue(_party.TryManualSwap());
            yield return new WaitForSeconds(0.4f);
        }

        // ───────────── 구조 ─────────────

        [UnityTest]
        public IEnumerator 목록의_모든_증강은_효과가_구현되어_있다()
        {
            Assert.AreEqual(12, AugmentCatalog.All.Count);
            Assert.AreEqual(4, AugmentCatalog.All.Count(a => a.Owner == AugmentOwner.Common));
            Assert.AreEqual(4, AugmentCatalog.All.Count(a => a.Owner == AugmentOwner.Warrior));
            Assert.AreEqual(4, AugmentCatalog.All.Count(a => a.Owner == AugmentOwner.Archer));
            Assert.AreEqual(12, AugmentCatalog.All.Select(a => a.Id).Distinct().Count(), "id 중복 없음");
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
            // 전사 반격 20 × 패시브 1.5 × (1 + 지척 0.5 + 공명 0.3) = 54 (전부 곱하면 58.5)
            Grant("w_close_strike");
            Grant("swap_resonance");
            _party.Swap(SwapCause.Manual);
            _party.Swap(SwapCause.Manual); // 전사로 돌아옴 → 공명 시작
            Place(_melee, 1.3f);
            var hit = HitInfo.Create(DamageSource.Counter, CharacterKind.Warrior, PlayerPos);
            Assert.AreEqual(54, DamageCalc.Compute(20, hit, _melee));
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

        [UnityTest]
        public IEnumerator 교대공명_스왑직후_반격만_강해지고_3초뒤엔_원래대로()
        {
            Grant("swap_resonance");
            Place(_melee, 6f);
            yield return Defend(AttackType.Heavy, _melee); // 전사 패링 → 궁수 등장 → 반격
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(1f - 26f / 100f, _melee.HpRatio, 1e-4f, "20 × (1 + 0.3) = 26");

            yield return new WaitForSeconds(2f); // 스왑 후 3초 지남
            var resonance = _defense.GetComponent<SwapResonanceAugment>();
            Assert.IsFalse(resonance.IsActive, "3초가 지나면 공명 끝");

            // 어떤 방어든 스왑을 일으키므로, 새 방어 성공이 공명을 다시 시작한다
            Place(_melee, 6f);
            yield return Defend(AttackType.Normal, _melee);
            Assert.IsTrue(resonance.IsActive, "방어 성공 스왑으로 공명 재시작");
        }

        // ───────────── 전사 ─────────────

        [UnityTest]
        public IEnumerator 등장충격파_주변적에게_피해와_기절()
        {
            Grant("w_shockwave");
            yield return SwapTo(CharacterKind.Archer);
            Place(_melee, 2f);  // 2.5 안
            Place(_ranged, 4f); // 2.5 밖 (가장자리 3.55)

            _gauge.Add(10);
            Assert.IsTrue(_party.TryManualSwap()); // 전사 등장
            Assert.AreEqual(1f - 22f / 100f, _melee.HpRatio, 1e-4f, "15 × 전사 1.5 = 22.5 → 22");
            Assert.IsTrue(_melee.IsStunned);
            Assert.AreEqual(1f, _ranged.HpRatio, "범위 밖");
            Assert.IsFalse(_ranged.IsStunned);

            yield return new WaitForSeconds(0.9f);
            Assert.IsFalse(_melee.IsStunned, "0.8초 뒤 풀림");
        }

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
            // 전사 반격은 궁수가 회피해 전사가 등장할 때 나간다
            yield return SwapTo(CharacterKind.Archer);
            Place(_melee, 1.3f); // 가까운 가장자리 0.8
            yield return Defend(AttackType.Normal, _melee, 1.0f);
            Assert.AreEqual(1f - 45f / 100f, _melee.HpRatio, 1e-4f, "20 × 1.5 × (1 + 0.5) = 45");

            float before = _melee.HpRatio;
            yield return SwapTo(CharacterKind.Archer);
            Place(_melee, 2.2f); // 가까운 가장자리 1.7 — 반격 박스(1.8) 안이지만 1.2 밖
            yield return Defend(AttackType.Normal, _melee, 1.0f);
            Assert.AreEqual(before - 0.30f, _melee.HpRatio, 1e-4f, "보너스 없이 30");
        }

        [UnityTest]
        public IEnumerator 피의패링_전사_패링시_회복_강공격6()
        {
            Grant("w_blood_parry");
            SpawnAttack(AttackType.Normal, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(93, _party.Hp);

            yield return Defend(AttackType.Normal);
            Assert.AreEqual(95, _party.Hp, "+2");

            yield return SwapTo(CharacterKind.Warrior); // 방어 성공으로 궁수가 됐으니 다시 전사로
            yield return Defend(AttackType.Heavy);
            Assert.AreEqual(100, _party.Hp, "+6 (최대 체력까지만)");
            Assert.AreEqual(CharacterKind.Archer, _party.Current.kind);

            SpawnAttack(AttackType.Normal, null, 1.2f); // 스왑 무적 끝난 뒤 궁수 피격
            yield return new WaitForSeconds(0.4f);
            SpawnAttack(AttackType.Normal, null, 1.2f);
            yield return new WaitForSecondsRealtime(0.3f);
            int hp = _party.Hp;
            yield return Defend(AttackType.Normal);
            Assert.AreEqual(hp, _party.Hp, "궁수 회피는 회복 없음");
        }

        [UnityTest]
        public IEnumerator 돌진베기_지나간_적에게_1번만_피해_궁수는_없음()
        {
            Grant("w_dash_slash");
            Place(_melee, 1.8f);
            _gauge.Add(2);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1f - 22f / 100f, _melee.HpRatio, 1e-4f, "15 × 1.5 = 22 — 한 번만");
            Assert.Greater(PlayerPos.x, _melee.transform.position.x, "적을 뚫고 지나감");

            yield return SwapTo(CharacterKind.Archer);
            float before = _melee.HpRatio;
            Place(_melee, -1.8f); // 백스텝 방향
            yield return new WaitForSeconds(1f); // 이동 스킬 쿨타임
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(before, _melee.HpRatio, "궁수 이동 스킬엔 피해 없음");
        }

        // ───────────── 궁수 ─────────────

        [UnityTest]
        public IEnumerator 등장화살비_가까운적에게_5발_거리보너스()
        {
            Grant("a_arrow_rain");
            Place(_melee, 8f); // 거리 배율 최대 (맞을 때마다 넉백으로 밀려도 그대로)
            _gauge.Add(10);
            Assert.IsTrue(_party.TryManualSwap()); // 궁수 등장
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(1f - 30f / 100f, _melee.HpRatio, 1e-4f, "6 × 5발");
            Assert.AreEqual(1f, _ranged.HpRatio, "12 밖의 적은 노리지 않음");
        }

        [UnityTest]
        public IEnumerator 갈래화살_다른적을_노리고_혼자면_부채꼴로_빗나감()
        {
            Grant("a_split_arrow");
            Place(_melee, 6f);
            Place(_ranged, -9f); // 반대편 — 같은 줄에 두면 추가 화살이 앞 적에 먼저 맞는다
            yield return Defend(AttackType.Normal, _melee);
            yield return new WaitForSeconds(2f);

            Assert.AreEqual(1f - 20f / 100f, _melee.HpRatio, 1e-4f, "본 화살 20");
            Assert.AreEqual(1f - 10f / 60f, _ranged.HpRatio, 1e-4f, "추가 화살 10 (50%)");
        }

        [UnityTest]
        public IEnumerator 거리유지_궁수_백스텝_거리가_늘어난다()
        {
            Grant("a_keep_distance");
            yield return SwapTo(CharacterKind.Archer);
            float x0 = PlayerPos.x;
            _gauge.Add(1);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForSeconds(0.3f);
            Assert.That(x0 - PlayerPos.x, Is.InRange(4.3f, 5.2f), "백스텝 3 + 1.5 (고정 스텝 오차 포함)");
        }

        [UnityTest]
        public IEnumerator 관통화살_줄선_적을_뚫고_뚫을때마다_20퍼센트()
        {
            Grant("a_pierce");
            yield return SwapTo(CharacterKind.Archer);
            _defense.GetComponent<PlayerMotor>().SetFacing(1f);
            Place(_melee, 3f);
            Place(_ranged, 6f);
            _gauge.Add(3);
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(1f - 15f / 100f, _melee.HpRatio, 1e-4f, "첫 적: 15 × 거리 3(×1.0) = 15");
            Assert.AreEqual(1f - 18f / 60f, _ranged.HpRatio, 1e-4f, "둘째 적: 15 × (1 + 0.2) = 18");
        }
    }
}
