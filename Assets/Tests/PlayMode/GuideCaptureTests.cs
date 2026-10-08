using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>
    /// 노션 가이드 페이지(기능 소개)용 장면 캡처. Captures/guide_*.png 로 저장된다.
    /// 기존 CaptureTests에 없는 장면만 여기서 찍는다.
    /// </summary>
    public class GuideCaptureTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private PlayerCombat _combat;
        private SkillGauge _gauge;
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
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = false;
            _melee = enemies.First(e => e.Kind == EnemyKind.Melee);
            _ranged = enemies.First(e => e.Kind == EnemyKind.Ranged);
            GameTuning.Current.godMode = true;
            yield return new WaitForSeconds(0.3f); // 착지
            Place(_melee, 5f);
            Place(_ranged, -6f);
        }

        private void Place(EnemyController enemy, float dx)
        {
            var pos = new Vector3(PlayerPos.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        private EnemyAttack SpawnAttack(AttackType type, EnemyController source, float fromDx, float speed = 8f)
        {
            bool heavy = type == AttackType.Heavy;
            var origin = new Vector2(PlayerPos.x + fromDx, PlayerPos.y);
            return EnemyAttack.Spawn(source, type, origin, new Vector2(-Mathf.Sign(fromDx), 0f),
                heavy ? new Vector2(0.9f, 0.9f) : new Vector2(0.6f, 0.6f), speed, 8f,
                heavy ? new Color(1f, 0.1f, 0.3f, 1f) : new Color(1f, 0.7f, 0.45f, 0.55f), heavy ? 20 : 10);
        }

        private static IEnumerator Capture(string name) => CaptureTests.Capture(name);

        // ───────────── 전사 ─────────────

        [UnityTest]
        public IEnumerator 전사_패링반격()
        {
            Place(_melee, 1.6f);
            _defense.TryActivate();
            var attack = SpawnAttack(AttackType.Normal, _melee, 1.2f);
            float t = 0f;
            while (attack != null && t < 1f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.05f);
            yield return Capture("guide_warrior_counter");
        }

        [UnityTest]
        public IEnumerator 전사_공격스킬()
        {
            Place(_melee, 1.8f);
            _gauge.Add(3);
            _combat.TryAttackSkill();
            yield return new WaitForSecondsRealtime(0.05f);
            yield return Capture("guide_warrior_attack");
        }

        [UnityTest]
        public IEnumerator 전사_돌진_무적()
        {
            _gauge.Add(1);
            var attack = SpawnAttack(AttackType.Normal, _melee, 2.2f, 6f);
            yield return null;
            _combat.TryMoveSkill(); // 앞으로 돌진 → 날아오던 공격이 몸을 통과 (무적)
            float t = 0f;
            while (attack != null && !attack.IsGhost && t < 1f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.06f);
            yield return Capture("guide_warrior_dash");
        }

        // ───────────── 궁수 ─────────────

        // ───────────── 방어 규칙 ─────────────

        [UnityTest]
        public IEnumerator 강공격_막으면_큰_반격()
        {
            Place(_melee, 6f);
            _defense.TryActivate();
            var attack = SpawnAttack(AttackType.Heavy, _melee, 1.3f);
            float t = 0f;
            while (attack != null && t < 1f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Capture("guide_heavy_parry");
        }

        [UnityTest]
        public IEnumerator 헛스윙()
        {
            _defense.TryActivate();
            yield return new WaitForSeconds(0.5f);
            yield return Capture("guide_whiff");
        }

        // ───────────── 성장 · 몬스터 ─────────────

        [UnityTest]
        public IEnumerator 레벨업()
        {
            _defense.GetComponent<PlayerLevel>().AddXp(20);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Capture("guide_levelup");
        }

        [UnityTest]
        public IEnumerator 원거리몬스터_투사체()
        {
            Vector2 from = _ranged.transform.position;
            EnemyAttack.Spawn(_ranged, AttackType.Normal, from, PlayerPos - from, new Vector2(0.6f, 0.6f), 8f, 16f,
                new Color(1f, 0.7f, 0.45f, 0.55f), 10);
            EnemyAttack.Spawn(_ranged, AttackType.Heavy, from + new Vector2(0f, 0.2f), PlayerPos - from, new Vector2(0.9f, 0.9f), 5f, 16f,
                new Color(1f, 0.1f, 0.3f, 1f), 20);
            yield return new WaitForSeconds(0.35f);
            yield return Capture("guide_ranged_shot");
        }
    }
}
