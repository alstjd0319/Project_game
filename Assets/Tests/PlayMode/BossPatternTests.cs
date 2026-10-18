using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>불가살이 특수 패턴 (make_bossfx 이펙트 + EnemyAttack 판정): 가시 발사 · 땅 찍기 · 돌진(2페이즈).</summary>
    public class BossPatternTests
    {
        private static BossController Boss => BossController.Instance;

        private static IEnumerator LoadBossRoom(float playerDx)
        {
            var rooms = Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2).Append(RoomType.Boss).ToArray();
            yield return TestScene.LoadRun(rooms);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true; // 일반 공격·자동 패턴은 끄고 패턴을 직접 부른다
            float t = 0f;
            while (BossController.Instance == null && t < 5f) { t += Time.deltaTime; yield return null; }
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            var pos = new Vector3(Boss.transform.position.x + playerDx, player.transform.position.y, 0f);
            player.transform.position = pos;
            player.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            yield return null;
        }

        private static EnemyAttack[] BossAttacks() =>
            Object.FindObjectsByType<EnemyAttack>().Where(a => a.Source == Boss.Enemy).ToArray();

        private static bool HasFx(string prefix) =>
            Object.FindObjectsByType<SpriteRenderer>().Any(r => r.sprite != null && r.sprite.name.StartsWith(prefix));

        private static IEnumerator WaitPatternEnd(float timeout = 6f)
        {
            float t = 0f;
            while (Boss.IsUsingPattern && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator 가시_발사는_예비_동작과_느낌표_뒤_가시를_부채꼴로_쏜다()
        {
            yield return LoadBossRoom(-9f);
            int cues = Boss.Enemy.PressCueCount;
            Boss.StartPattern(BossPattern.SpineVolley);
            Assert.IsTrue(Boss.IsUsingPattern);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, BossAttacks().Length, "예비 동작 중엔 아직 안 쏜다");
            Assert.IsNotNull(Object.FindAnyObjectByType<PressCueIcon>(), "느낌표");
            yield return new WaitForSeconds(0.8f);
            var spines = BossAttacks();
            Assert.AreEqual(5, spines.Length, "가시 다섯 개");
            Assert.Greater(Boss.Enemy.PressCueCount, cues, "빨간 느낌표");
            Assert.IsTrue(spines.All(sp => sp.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite != null && r.sprite.name.StartsWith("bossfx_spine"))),
                "가시마다 날아가는 가시 그림");
            yield return WaitPatternEnd();
            Assert.AreEqual(1, Boss.PatternCount);
            Assert.IsFalse(Boss.Enemy.Frozen, "패턴이 끝나면 다시 움직인다");
        }

        [UnityTest]
        public IEnumerator 땅_찍기는_앞뒤로_바닥을_타는_충격파를_보낸다()
        {
            yield return LoadBossRoom(-16f); // 충격파가 닿지 않는 거리에서 지켜본다
            Boss.StartPattern(BossPattern.GroundSlam);
            yield return new WaitForSeconds(1.1f);
            var waves = BossAttacks();
            Assert.AreEqual(2, waves.Length, "앞뒤 두 갈래");
            Assert.IsTrue(waves.Any(w => w.Type == AttackType.Heavy), "플레이어 쪽은 강공격");
            float x0 = waves[0].transform.position.x, x1 = waves[1].transform.position.x;
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Mathf.Abs(waves[0].transform.position.x - waves[1].transform.position.x), Mathf.Abs(x0 - x1), "서로 멀어진다");
            foreach (var w in waves) Assert.Less(w.transform.position.y, 1.2f, "바닥을 탄다 (점프로 넘을 수 있는 높이)");
            Assert.IsTrue(HasFx("bossfx_slam"), "땅 찍기 충격파 그림");
            foreach (var w in waves)
            {
                Assert.IsTrue(w.GetComponentsInChildren<SpriteRenderer>().Any(r => r != w.GetComponent<SpriteRenderer>() && r.sprite != null && r.sprite.name.StartsWith("bossfx_wave")),
                    "충격파마다 가시 파도 그림이 붙는다");
                Assert.IsFalse(w.GetComponent<SpriteRenderer>().enabled, "판정 네모(주황)는 숨겨진다");
            }
        }

        [UnityTest]
        public IEnumerator 돌진은_플레이어_쪽으로_달려가고_패링하면_멈춘다()
        {
            yield return LoadBossRoom(-12f);
            float x0 = Boss.transform.position.x;
            Boss.StartPattern(BossPattern.Dash);
            bool trail = false;
            for (float w = 0f; Boss.IsUsingPattern && w < 6f; w += Time.deltaTime)
            {
                trail |= HasFx("bossfx_dashtrail");
                yield return null;
            }
            Assert.IsTrue(trail, "돌진 잔상 그림");
            Assert.Less(Boss.transform.position.x, x0 - 4f, "왼쪽(플레이어 쪽)으로 달려갔다");

            // 다시: 이번엔 플레이어가 패링 → 판정이 부서지고 그 자리에서 멈춘다
            yield return LoadBossRoom(-12f);
            x0 = Boss.transform.position.x;
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            Boss.StartPattern(BossPattern.Dash);
            float t = 0f;
            bool parried = false;
            while (Boss.IsUsingPattern && t < 6f)
            {
                var hit = BossAttacks().FirstOrDefault();
                if (!parried && hit != null && Mathf.Abs(hit.transform.position.x - defense.transform.position.x) < 2.2f)
                {
                    parried = defense.TryActivate();
                }
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(parried);
            Assert.IsTrue(defense.SucceededThisActive || Boss.Enemy.PressCueCount > 0);
            Assert.Greater(Boss.transform.position.x, defense.transform.position.x, "패링한 자리 앞에서 멈췄다 (플레이어를 지나가지 않음)");
        }

        [UnityTest]
        public IEnumerator 일페이즈에도_멀리_있으면_곧바로_돌진해_온다()
        {
            yield return LoadBossRoom(-13f);
            GameTuning.Current.enemyPassive = false;
            Assert.AreEqual(1, Boss.Phase);
            var player = Object.FindAnyObjectByType<PlayerDefense>().transform;
            float t = 0f;
            while (!(Boss.IsUsingPattern && Boss.LastPattern == BossPattern.Dash) && t < 4f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(BossPattern.Dash, Boss.LastPattern, "멀리 있으면 특수 패턴 간격(첫 5초)을 기다리지 않고 돌진");
            Assert.Less(t, 4f);
            float px = player.position.x;
            while (Boss.IsUsingPattern && t < 10f) { t += Time.deltaTime; yield return null; }
            float frontX = Boss.transform.position.x - Boss.transform.localScale.x * 0.5f;
            Assert.Less(frontX, px + 4f, "플레이어 가까이까지 달려왔다");
            Assert.Greater(frontX, px - 4f, "피해도 방 끝까지 미끄러지지 않는다");
        }

        [UnityTest]
        public IEnumerator 자동으로_특수_패턴을_섞어_쓰고_변신하면_하던_패턴을_멈춘다()
        {
            yield return LoadBossRoom(-9f);
            GameTuning.Current.enemyPassive = false;
            float t = 0f;
            while (Boss.PatternCount == 0 && !Boss.IsUsingPattern && t < 9f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(Boss.IsUsingPattern || Boss.PatternCount > 0, "첫 특수 패턴은 몇 초 안에");
            Boss.ForcePhase2();
            yield return null;
            Assert.IsFalse(Boss.IsUsingPattern, "변신이 패턴보다 앞선다");
        }
    }
}
