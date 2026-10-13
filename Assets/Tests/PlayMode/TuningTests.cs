using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>입력 버퍼 / 판정 타이밍 기록 / 실시간 튜닝 / 연습 스위치 / 튜닝 저장 형식.</summary>
    public class TuningTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private EnemyController _melee;
        private readonly List<DefenseTiming> _timings = new();

        private static GameTuning T => GameTuning.Current;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            _defense = Object.FindAnyObjectByType<PlayerDefense>();
            _party = _defense.GetComponent<PlayerParty>();
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = false;
            _melee = enemies.First(e => e.Kind == EnemyKind.Melee);
            foreach (var e in enemies) Place(e, 15f);

            _timings.Clear();
            _defense.TimingReported += _timings.Add;
            yield return new WaitForSeconds(0.3f); // 착지
        }

        private void Place(EnemyController enemy, float dx)
        {
            var pos = new Vector3(_defense.transform.position.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        /// <summary>플레이어 오른쪽에서 왼쪽으로 오는 0.6 크기 공격. fromDx가 작을수록 빨리 닿는다.</summary>
        private void SpawnAttack(float fromDx)
        {
            Vector2 p = _defense.transform.position;
            EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x + fromDx, p.y), Vector2.left,
                new Vector2(0.6f, 0.6f), 10f, 6f, Color.red, 10);
        }

        [UnityTest]
        public IEnumerator 활성시간_직후_버퍼안에_닿으면_성공()
        {
            Assert.IsTrue(T.inputBufferEnabled);
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSeconds(T.defenseActiveTime + 0.015f); // 활성은 끝났고 버퍼(0.06) 안
            Assert.AreEqual(DefenseState.Active, _defense.State, "버퍼 동안은 아직 헛스윙이 아님");

            SpawnAttack(0.72f); // 거의 붙은 채로 생성 → 곧바로 닿음
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.AreEqual(100, _party.Hp);
            Assert.AreEqual(TimingKind.Buffered, _timings.Last().Kind);
        }

        [UnityTest]
        public IEnumerator 버퍼를_끄면_활성시간이_끝나는_즉시_헛스윙()
        {
            T.inputBufferEnabled = false;
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSeconds(T.defenseActiveTime + 0.015f);
            Assert.AreEqual(DefenseState.Cooldown, _defense.State);

            SpawnAttack(0.72f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(93, _party.Hp); // 플레이어 방어 패시브
            Assert.AreEqual(TimingKind.TooEarly, _timings.Last().Kind, "헛스윙 쿨타임 중 피격 = 너무 일찍 누름");
        }

        [UnityTest]
        public IEnumerator 활성중_성공은_누른뒤_닿기까지의_시간을_기록()
        {
            Assert.IsTrue(_defense.TryActivate());
            SpawnAttack(2f); // 약 0.13초 뒤 닿음
            yield return new WaitForSecondsRealtime(0.3f);

            var t = _timings.Last();
            Assert.AreEqual(TimingKind.InWindow, t.Kind);
            Assert.That(t.Seconds, Is.InRange(0.05f, T.defenseActiveTime));
        }

        [UnityTest]
        public IEnumerator 맞은_직후에_누르면_늦음으로_기록()
        {
            SpawnAttack(0.72f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(93, _party.Hp); // 플레이어 방어 패시브

            _defense.TryActivate();
            Assert.AreEqual(TimingKind.TooLate, _timings.Last().Kind);
        }

        [UnityTest]
        public IEnumerator 튜닝값을_바꾸면_다음_방어부터_바로_적용()
        {
            T.defenseActiveTime = 0.5f;
            T.inputBufferEnabled = false;
            Assert.IsTrue(_defense.TryActivate());
            yield return new WaitForSeconds(0.42f);
            Assert.AreEqual(DefenseState.Active, _defense.State, "0.3초가 지나도 0.5초 설정이면 아직 활성");
        }

        [UnityTest]
        public IEnumerator 연습스위치_무적과_적_무한체력()
        {
            T.godMode = true;
            SpawnAttack(0.72f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(100, _party.Hp, "무적: 체력이 줄지 않음");

            T.enemyInvincible = true;
            _melee.TakeDamage(9999, 1f, false);
            yield return null;
            Assert.IsFalse(_melee.IsDead);
            Assert.AreEqual(1f, _melee.HpRatio);
        }

        [UnityTest]
        public IEnumerator 튜닝패널은_어느_탭이든_높이_상한을_넘지_않는다()
        {
            var panel = TuningPanel.Instance;
            panel.SetVisible(true);
            Assert.AreEqual(5, panel.TabCount);
            for (int i = 0; i < panel.TabCount; i++)
            {
                panel.SelectTab(i);
                yield return null;
                Assert.LessOrEqual(panel.PanelHeight, 146f + 420f + 76f + 0.5f, $"탭 {i}: 판정 대상(적)을 가리지 않도록 높이 상한");
            }
            panel.SelectTab(1);
            Assert.IsTrue(panel.IsScrollable, "판정 탭은 내용이 길어 스크롤");
        }

        [UnityTest]
        public IEnumerator 몬스터_수를_늘리면_종류별로_그만큼_유지되고_줄이면_치워진다()
        {
            int Count(EnemyKind k) => Object.FindObjectsByType<EnemyController>().Count(e => e.Kind == k && !e.IsDead);

            T.enemiesPerKind = 4;
            yield return null;
            yield return null;
            Assert.AreEqual(4, Count(EnemyKind.Melee));
            Assert.AreEqual(4, Count(EnemyKind.Ranged));

            // 하나 잡으면 리스폰 대기 동안은 목표를 채우려고 더 만들지 않는다
            var one = Object.FindObjectsByType<EnemyController>().First(e => e.Kind == EnemyKind.Melee);
            one.TakeDamage(9999, 1f, false);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(3, Count(EnemyKind.Melee), "리스폰 대기 중");
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(4, Count(EnemyKind.Melee), "대기가 끝나면 다시 4마리");

            T.enemiesPerKind = 1;
            yield return null;
            yield return null;
            Assert.AreEqual(1, Count(EnemyKind.Melee));
            Assert.AreEqual(1, Count(EnemyKind.Ranged));
        }

        [UnityTest]
        public IEnumerator 연습_몬스터_종류별로_끄고_켜기()
        {
            var level = _defense.GetComponent<PlayerLevel>();
            int Count(EnemyKind k) => Object.FindObjectsByType<EnemyController>().Count(e => e.Kind == k && !e.IsDead);

            T.hideMelee = true;
            yield return null;
            yield return null;
            Assert.AreEqual(0, Count(EnemyKind.Melee), "근접 몬스터가 치워짐");
            Assert.AreEqual(1, Count(EnemyKind.Ranged), "원거리는 그대로");
            Assert.AreEqual(0, level.Xp, "치운 건 처치가 아니라 경험치 없음");

            yield return new WaitForSeconds(3f); // 리스폰 시간이 지나도
            Assert.AreEqual(0, Count(EnemyKind.Melee), "꺼져 있는 동안 다시 안 나옴");

            T.hideMelee = false;
            yield return null;
            yield return null;
            Assert.AreEqual(1, Count(EnemyKind.Melee), "켜면 바로 다시 나옴");

            T.hideRanged = true;
            yield return null;
            yield return null;
            Assert.AreEqual(0, Count(EnemyKind.Ranged));
            Assert.AreEqual(1, Count(EnemyKind.Melee));
        }

        [UnityTest]
        public IEnumerator 연습_게이지무한_써도_줄지않는다()
        {
            var gauge = _defense.GetComponent<SkillGauge>();
            var combat = _defense.GetComponent<PlayerCombat>();
            T.infiniteGauge = true;
            Assert.AreEqual(0, gauge.Value);
            Assert.IsTrue(combat.TryAttackSkill(), "게이지 0이어도 공격 스킬");
            Assert.IsTrue(combat.TryMoveSkill(), "이동 스킬");
            Assert.AreEqual(0, gauge.Value, "줄지 않음");
            yield return new WaitForSeconds(1.1f); // 쿨타임

            T.infiniteGauge = false;
            Assert.IsFalse(combat.TryAttackSkill(), "끄면 원래대로 게이지 부족");
        }

        [Test]
        public void 튜닝_저장형식_왕복_연습스위치는_저장안됨()
        {
            var a = new GameTuning { defenseActiveTime = 0.27f, inputBuffer = 0.08f, godMode = true };
            a.heavyParry.hitStop = 0.09f;
            string json = JsonUtility.ToJson(a);

            var b = new GameTuning();
            JsonUtility.FromJsonOverwrite(json, b);
            Assert.AreEqual(0.27f, b.defenseActiveTime, 1e-5f);
            Assert.AreEqual(0.08f, b.inputBuffer, 1e-5f);
            Assert.AreEqual(0.09f, b.heavyParry.hitStop, 1e-5f);
            Assert.IsFalse(b.godMode, "연습 스위치는 파일에 남지 않음");
            StringAssert.DoesNotContain("godMode", json);
        }

        [UnityTest]
        public IEnumerator 배경은_3겹_시차로_카메라를_따라가고_구간을_바꿀_수_있다()
        {
            var bg = Object.FindAnyObjectByType<ParallaxBackground>();
            Assert.IsNotNull(bg, "씬에 배경");
            Assert.AreEqual(4, bg.ZoneCount, "사당·1구간·2구간·3구간");
            yield return null;
            yield return null;
            Assert.AreEqual(1, bg.Zone, "기본은 1구간");
            Assert.AreEqual(3, bg.LayerCount, "먼·중간·가까운");

            var cam = Camera.main;
            var far = bg.transform.Find("Layer_Far");
            var near = bg.transform.Find("Layer_Near");
            Assert.IsNotNull(far);
            // 카메라 폭을 빈틈없이 덮는다: 가장 왼쪽·오른쪽 그림이 화면 밖까지
            float halfW = cam.orthographicSize * cam.aspect;
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var sr in far.GetComponentsInChildren<SpriteRenderer>())
            {
                minX = Mathf.Min(minX, sr.bounds.min.x);
                maxX = Mathf.Max(maxX, sr.bounds.max.x);
            }
            Assert.Less(minX, cam.transform.position.x - halfW, "왼쪽 끝이 화면 밖");
            Assert.Greater(maxX, cam.transform.position.x + halfW, "오른쪽 끝이 화면 밖");

            // 화면 높이(12유닛)에 딱 맞는다
            Assert.AreEqual(12f, far.GetComponentInChildren<SpriteRenderer>().bounds.size.y, 0.01f);

            // 장수 계산: 감아 돌리는 최악의 위치에서도 초광폭(32:9)까지 덮는다
            float tile = far.GetComponentInChildren<SpriteRenderer>().sprite.bounds.size.x;
            float ultraHalf = cam.orthographicSize * 32f / 9f;
            int n = ParallaxBackground.RequiredCopies(tile, ultraHalf);
            Assert.AreEqual(1, n % 2, "홀수");
            Assert.GreaterOrEqual(((n - 1) / 2 - 0.5f) * tile, ultraHalf, "한쪽 최소 덮는 폭이 화면 반폭 이상");
            Assert.AreEqual(n, far.childCount, "먼 배경 장수");

            // 구간 바꾸기: 2구간은 가까운 배경이 앞(전경)
            T.backgroundZone = 2;
            yield return null;
            yield return null;
            Assert.AreEqual(2, bg.Zone);
            int nearOrder = bg.transform.Find("Layer_Near").GetComponentInChildren<SpriteRenderer>().sortingOrder;
            Assert.Greater(nearOrder, 12, "2구간 near는 플레이어 그림(12) 앞");
            T.backgroundZone = 1;
            yield return null;
            yield return null;
            Assert.Less(bg.transform.Find("Layer_Near").GetComponentInChildren<SpriteRenderer>().sortingOrder, 0, "1구간 near는 뒤");
        }
    }
}
