using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>몬스터 공격 모양 — 근접 베기(몸에서 뻗음) / 원거리 투사체 (기획서 3.1·7장).</summary>
    public class EnemyTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private SkillGauge _gauge;
        private EnemyController _melee;
        private EnemyController _ranged;

        private static GameTuning T => GameTuning.Current;
        private Vector2 PlayerPos => _defense.transform.position;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            _defense = Object.FindAnyObjectByType<PlayerDefense>();
            _party = _defense.GetComponent<PlayerParty>();
            _gauge = _defense.GetComponent<SkillGauge>();
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = false;
            _melee = enemies.First(e => e.Kind == EnemyKind.Melee);
            _ranged = enemies.First(e => e.Kind == EnemyKind.Ranged);
            Place(_melee, 15f);
            Place(_ranged, -15f);
            T.heavyChance = 0f; // 기본은 일반 공격만 (반응시간 기준 검사)
            yield return new WaitForSeconds(0.3f); // 착지
        }

        private void Place(EnemyController enemy, float dx)
        {
            var pos = new Vector3(PlayerPos.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        private static IEnumerator WaitForAttack(System.Action<EnemyAttack> found, float timeout = 5f)
        {
            float t = 0f;
            EnemyAttack attack = null;
            while (attack == null && t < timeout)
            {
                attack = Object.FindAnyObjectByType<EnemyAttack>();
                t += Time.deltaTime;
                if (attack == null) yield return null;
            }
            Assert.IsNotNull(attack, "공격이 나와야 함");
            found(attack);
        }

        /// <summary>예비 모션이 시작될 때까지 기다린다 (몸이 커지기 시작).</summary>
        private IEnumerator WaitForWindup()
        {
            float t = 0f;
            while (_melee.transform.localScale.x <= 1.001f && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.Less(t, 5f, "예비 모션 시작");
        }

        private void MovePlayerTo(float x)
        {
            var p = new Vector2(x, PlayerPos.y);
            _defense.transform.position = p;
            _defense.GetComponent<Rigidbody2D>().position = p;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator 근접몬스터는_몸앞에서_한번에_벤다_투사체가_아님()
        {
            _melee.enabled = true;
            Place(_melee, 2f);
            yield return WaitForWindup();
            MovePlayerTo(_melee.transform.position.x - 4f); // 사거리 밖으로 비켜서 베기가 끝까지 뻗는 걸 본다

            EnemyAttack slash = null;
            yield return WaitForAttack(a => slash = a);
            Assert.IsTrue(slash.IsSlash, "근접 공격은 베기");
            Assert.AreSame(_melee, slash.Source);
            float frontEdge = _melee.transform.position.x - _melee.HalfWidth; // 플레이어가 왼쪽
            Assert.AreEqual(frontEdge, slash.SlashAnchor.x, 1e-3f, "몸 앞면에 붙어서 시작");

            yield return new WaitForSeconds(0.08f);
            Assert.IsTrue(slash != null, "다 뻗은 뒤 잠깐 남는다");
            float length = slash.transform.localScale.x;
            Assert.AreEqual(2.9f - _melee.HalfWidth, length, 1e-3f, "0.08초 안에 끝까지 — 한 번에 삭");
            float nearEnd = slash.transform.position.x + length * 0.5f;
            Assert.AreEqual(frontEdge, nearEnd, 1e-3f, "몬스터 쪽 끝은 그대로 — 날아가지 않는다");
        }

        [UnityTest]
        public IEnumerator 근접베기_반응시간은_예비모션_길이()
        {
            _melee.enabled = true;
            Place(_melee, 2f);
            yield return WaitForWindup();
            float windupStart = Time.time;
            MovePlayerTo(_melee.transform.position.x - 2.6f + 0.01f); // 사거리 끝

            EnemyAttack slash = null;
            yield return WaitForAttack(a => slash = a);
            float spawned = Time.time;
            while (_party.Hp == 100 && Time.time - windupStart < 2f) yield return null;
            Assert.Less(_party.Hp, 100, "사거리 끝에 선 플레이어에게 닿는다");
            Assert.LessOrEqual(Time.time - spawned, 0.08f, "베기가 나오면 거의 바로 닿는다");
            Assert.That(Time.time - windupStart, Is.InRange(T.meleeNormalReaction - 0.03f, T.meleeNormalReaction + 0.1f),
                "예비 모션 시작부터 맞기까지 ≈ 반응시간");
        }

        [UnityTest]
        public IEnumerator 근접베기를_패링하면_부서지고_플레이어_반격이_닿는다()
        {
            _melee.enabled = true;
            Place(_melee, 2f);
            EnemyAttack slash = null;
            yield return WaitForAttack(a => slash = a);
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSecondsRealtime(0.45f);

            Assert.AreEqual(100, _party.Hp);
            Assert.AreEqual(1, _gauge.Value, "패링 성공");
            Assert.IsTrue(slash == null, "패링하면 베기가 부서짐");
            Assert.AreEqual(0.7f, _melee.HpRatio, 1e-4f, "바짝 붙은 근접 몬스터라 플레이어 반격(사거리 1.8)이 닿는다 — 30");
        }

        [UnityTest]
        public IEnumerator 원거리몬스터는_그대로_투사체()
        {
            T.godMode = true;
            _ranged.enabled = true;
            Place(_ranged, -7f);
            EnemyAttack shot = null;
            yield return WaitForAttack(a => shot = a);
            Assert.IsFalse(shot.IsSlash);
            float x0 = shot.transform.position.x;
            yield return new WaitForSeconds(0.1f);
            if (shot != null) Assert.Greater(shot.transform.position.x, x0, "플레이어 쪽으로 날아감");
        }
    }
}
