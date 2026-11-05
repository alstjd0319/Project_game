using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>웨이브 모드 (docs/기획_웨이브모드.md): 진행 · 스폰 · 도깨비불 규칙 · 가장자리 표시.</summary>
    public class WaveTests
    {
        private static WaveController Waves => WaveController.Instance;

        private static EnemyController[] Enemies() =>
            Object.FindObjectsByType<EnemyController>().Where(e => !e.IsDead).ToArray();

        private static IEnumerator WaitUntil(System.Func<bool> cond, float timeout = 10f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 안에 조건이 안 맞음");
        }

        private static IEnumerator LoadWave()
        {
            RoomController.UseWaveMap = true;
            WaveController.RestTime = 0.2f;
            yield return TestScene.LoadRun();
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            yield return WaitUntil(() => Waves != null);
        }

        private static IEnumerator ClearWave()
        {
            int wave = Waves.Wave;
            for (float t = 0f; t < 40f && Waves.Wave == wave && Waves.Killed < Waves.Def.Total; t += 0.1f)
            {
                foreach (var e in Enemies()) e.TakeDamage(99999, 1f, false);
                yield return new WaitForSeconds(0.1f);
            }
            Assert.GreaterOrEqual(Waves.Killed, Waves.Def.Total);
        }

        [SetUp]
        public void SetUp() => GameTuning.UseDefaultsForTests();

        [TearDown]
        public void TearDown()
        {
            RoomController.UseWaveMap = false;
            WaveController.RestTime = 3f;
        }

        [Test]
        public void 웨이브_표는_기획대로이고_구역마다_스폰_지점이_있다()
        {
            Assert.AreEqual(10, WavePlan.Waves.Length);
            Assert.IsTrue(WavePlan.Waves[4].Elite, "5웨이브 엘리트");
            Assert.IsTrue(WavePlan.Waves[9].Boss, "10웨이브 보스");
            Assert.AreEqual(160, WavePlan.Waves.Where(w => !w.Elite && !w.Boss).Sum(w => w.Total));
            foreach (var w in WavePlan.Waves)
            {
                if (w.Boss) continue;
                Assert.Greater(w.Stagger, 0.3f, "시작 간격은 패링 판정 창보다 넓게");
                foreach (int z in w.Zones) Assert.Greater(WavePlan.Points(z, w.MaxLevel).Count, 0, WavePlan.ZoneList[z].Name);
            }
            foreach (var p in WavePlan.Points(WavePlan.Plaza, 0)) Assert.IsFalse(p.Platform.HasValue, "1~2웨이브는 바닥만");
            foreach (var pit in RoomLayouts.WaveMap.Pits)
                for (int z = 0; z < WavePlan.ZoneList.Length; z++)
                    foreach (var p in WavePlan.Points(z, 7))
                        if (!p.Platform.HasValue) Assert.IsFalse(p.X > pit.Left - 0.5f && p.X < pit.Right + 0.5f, $"구멍 위 지점 x={p.X}");
        }

        [UnityTest]
        public IEnumerator 웨이브는_전부_잡아야_넘어가고_동시_수를_넘지_않는다()
        {
            yield return LoadWave();
            yield return WaitUntil(() => Waves.Wave == 1);
            StringAssert.Contains("남은 10/10", Hud.RunInfoText);
            int most = 0;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                most = Mathf.Max(most, Enemies().Length);
                yield return null;
            }
            Assert.AreEqual(5, most, "동시 수만큼");
            Assert.AreEqual(1, Waves.Wave, "시간이 지나도 안 넘어간다");
            Assert.IsTrue(Enemies().All(e => e.Kind == EnemyKind.Melee), "1웨이브는 창귀만");
            Assert.AreEqual(1f, Enemies()[0].HpRatio);
            var first = Enemies()[0];
            first.TakeDamage(59, 1f, false, null, HitStagger.None);
            Assert.AreEqual(1f / 60f, first.HpRatio, 0.001f, "체력 60");
            yield return ClearWave();
            yield return WaitUntil(() => Waves.Wave == 2);
            Assert.AreEqual(0, Waves.Killed);
            StringAssert.Contains("남은 12/12", Hud.RunInfoText);
        }

        [UnityTest]
        public IEnumerator 발판에서_나온_창귀는_그_발판을_걷고_떨어지지_않는다()
        {
            yield return LoadWave();
            yield return WaitUntil(() => Waves.Wave == 1);
            var p = RoomLayouts.WaveMap.Platforms.First(q => q.Width >= 8f && q.X < -10f); // 플레이어(광장)가 옆에 있는 넓은 발판
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            GameTuning.Current.enemyPassive = true;
            float min = p.X - p.Width * 0.5f + 0.4f, max = p.X + p.Width * 0.5f - 0.4f;
            var melee = spawner.SpawnEnemyAt(EnemyKind.Melee, p.X, RoomLayouts.Top(p), false, min, max, exact: true);
            float y = melee.transform.position.y, x = melee.transform.position.x;
            Assert.Greater(y, RoomLayouts.Top(p), "발판 위");
            yield return new WaitForSeconds(1.5f);
            Assert.AreNotEqual(x, melee.transform.position.x, "걸어온다");
            Assert.AreEqual(y, melee.transform.position.y, 0.01f);
            Assert.GreaterOrEqual(melee.transform.position.x, min - 0.01f);
            Assert.LessOrEqual(melee.transform.position.x, max + 0.01f);
        }

        [UnityTest]
        public IEnumerator 엘리트_웨이브와_보스_웨이브_그리고_클리어()
        {
            yield return LoadWave();
            yield return WaitUntil(() => Waves.Wave == 1);
            Waves.JumpTo(5);
            yield return WaitUntil(() => Enemies().Any(e => e.IsElite));
            Assert.AreEqual(1, Enemies().Count(e => e.IsElite));
            yield return ClearWave();
            yield return WaitUntil(() => Waves.Wave == 6);

            Waves.JumpTo(10);
            yield return WaitUntil(() => BossController.Instance != null);
            Assert.AreEqual(-6f, Object.FindAnyObjectByType<PlayerParty>().transform.position.x, 0.5f, "광장으로 옮겨 싸운다 (임시)");
            var boss = BossController.Instance.Enemy;
            boss.HpFloor = 0;
            boss.TakeDamage(99999, 1f, false);
            yield return WaitUntil(() => Waves.CurrentPhase == WaveController.Phase.Cleared);
            Assert.AreEqual(1, RoomController.Instance.Gates.Count, "사당으로 나가는 문");
        }

        [UnityTest]
        public IEnumerator 화면_밖_몬스터와_활성_구역을_가장자리에_표시한다()
        {
            yield return LoadWave();
            yield return WaitUntil(() => Waves.Wave == 1);
            Waves.JumpTo(3); // 탑 구역 — 플레이어는 광장
            yield return WaitUntil(() => Enemies().Length >= 5, 8f);
            yield return null;
            var marks = Object.FindAnyObjectByType<WaveIndicators>();
            Assert.Greater(marks.ShownCount, 0, "화면 밖 몬스터 표식");
            Assert.LessOrEqual(marks.ShownCount, 5, "가까운 순서로 5개까지");
            Assert.IsTrue(marks.ZoneArrowShown, "활성 구역 방향 화살표");
        }

        [UnityTest]
        public IEnumerator 도깨비불은_층이_두_층_이상_다르면_쏘지_않는다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.hideMelee = true;
            GameTuning.Current.attackIntervalMin = 0.3f;
            GameTuning.Current.attackIntervalMax = 0.3f;
            yield return null;
            var ranged = Enemies().First(e => e.Kind == EnemyKind.Ranged);
            var player = Object.FindAnyObjectByType<PlayerParty>();
            var rb = player.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 3층 높이 공중에 세워 둔다
            var pos = new Vector3(ranged.transform.position.x + 3f, RoomLayouts.Level(3) + 0.7f, 0f);
            player.transform.position = pos;
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            var cam = Camera.main.GetComponent<CameraRig>();
            cam.enabled = false;
            Camera.main.transform.position = new Vector3(pos.x, 4f, -10f);
            bool fired = false;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                fired |= ranged.IsWindingUp;
                yield return null;
            }
            Assert.IsFalse(fired, "다른 층을 쏘지 않는다");

            pos.y = 0.8f; // 같은 층이면 쏜다
            player.transform.position = pos;
            rb.position = pos;
            for (float t = 0f; t < 2.5f && !fired; t += Time.deltaTime)
            {
                fired |= ranged.IsWindingUp;
                yield return null;
            }
            Assert.IsTrue(fired, "같은 층이면 쏜다");
        }

        [UnityTest]
        public IEnumerator 불덩이는_발판에_막힌다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            yield return null;
            var p = RoomLayouts.All[RoomLayouts.Basic].Platforms[0];
            var shot = EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.X, p.Y - 1.5f), Vector2.up, new Vector2(0.6f, 0.6f), 8f, 6f, Color.red, 10);
            shot.BlockedByTerrain = true;
            var through = EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.X + 1f, p.Y - 1.5f), Vector2.up, new Vector2(0.6f, 0.6f), 8f, 6f, Color.red, 10);
            yield return new WaitForSeconds(0.45f);
            Assert.IsTrue(shot == null, "발판에 막혀 사라졌다");
            Assert.IsFalse(through == null, "막히지 않는 공격(보스 가시 등)은 그대로");
        }
    }
}
