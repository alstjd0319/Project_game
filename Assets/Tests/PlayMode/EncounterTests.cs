using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>레벨 디자인: 직접 짠 전투 장면(인카운터) · 방 번호 → 구간 · 다수전 공격권.</summary>
    public class EncounterTests
    {
        private static RoomController Room => Object.FindAnyObjectByType<RoomController>();

        private static EnemyController[] Enemies() =>
            Object.FindObjectsByType<EnemyController>().Where(e => !e.IsDead).ToArray();

        private static IEnumerator WaitUntil(System.Func<bool> cond, float timeout = 8f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 안에 조건이 안 맞음");
        }

        [SetUp]
        public void SetUp() => GameTuning.UseDefaultsForTests();

        [TearDown]
        public void TearDown()
        {
            EncounterCatalog.ForcedId = null;
            RoomLayouts.ForcedIndex = -1;
        }

        [Test]
        public void 방_번호는_세_구간으로_나뉜다()
        {
            Assert.AreEqual(6, RunState.TotalRooms, "이 테스트는 6방 기준");
            int[] expected = { 1, 1, 2, 2, 3, 3 };
            for (int n = 1; n <= RunState.TotalRooms; n++)
                Assert.AreEqual(expected[n - 1], RunState.ZoneFor(n), $"방 {n}");
        }

        [Test]
        public void 싸우는_방마다_예산과_구간에_맞는_장면이_있다()
        {
            for (int n = 1; n < RunState.TotalRooms; n++)
            {
                int zone = RunState.ZoneFor(n);
                Assert.Greater(EncounterCatalog.Candidates(RoomType.Normal, n, zone).Count, 0, $"일반방 {n}");
                if (n >= 2) Assert.Greater(EncounterCatalog.Candidates(RoomType.Elite, n, zone).Count, 0, $"엘리트방 {n}");
                foreach (var e in EncounterCatalog.Candidates(RoomType.Normal, n, zone))
                    Assert.LessOrEqual(e.Cost, EncounterCatalog.Budget(n), $"{e.Name}: 예산 초과");
            }
            Assert.AreEqual("first", EncounterCatalog.Candidates(RoomType.Normal, 1, 1).Single().Id, "첫 방은 첫 만남만");
            // 반복감을 줄이려면 방마다 고를 장면이 넉넉해야 한다 (A: 일반 4개 이상 · 엘리트 2개 이상)
            for (int n = 2; n < RunState.TotalRooms; n++)
            {
                int zone = RunState.ZoneFor(n);
                Assert.GreaterOrEqual(EncounterCatalog.Candidates(RoomType.Normal, n, zone).Count, 4, $"일반방 {n}: 장면 4개 이상");
                Assert.GreaterOrEqual(EncounterCatalog.Candidates(RoomType.Elite, n, zone).Count, 2, $"엘리트방 {n}: 장면 2개 이상");
            }
            Assert.IsFalse(EncounterCatalog.Candidates(RoomType.Normal, 3, 2).Any(e => e.MaxZone < 2), "2구간에 1구간 전용 장면이 나오지 않는다");
        }

        [Test]
        public void 장면은_어울리는_지형과_방_안쪽_자리를_쓴다()
        {
            var ids = new HashSet<string>();
            foreach (var e in EncounterCatalog.All)
            {
                Assert.IsTrue(ids.Add(e.Id), $"장면 번호 중복: {e.Id}");
                Assert.Greater(e.Layouts.Length, 0, e.Name);
                foreach (int layout in e.Layouts)
                {
                    Assert.AreNotEqual(RoomLayouts.Flat, layout, $"{e.Name}: 싸우는 방에 평지는 쓰지 않는다");
                    if (e.OnlyRoom != 1) Assert.AreNotEqual(RoomLayouts.Basic, layout, $"{e.Name}: 기본 지형은 첫 방 전용");
                }
                bool high = false;
                foreach (var wave in e.Waves)
                foreach (var s in wave)
                {
                    if (s.Anchor == SpawnAnchor.Room) Assert.LessOrEqual(Mathf.Abs(s.X), 19f, $"{e.Name}: 방 밖 자리");
                    if (s.Height > 0f)
                    {
                        Assert.AreEqual(EnemyKind.Ranged, s.Kind, $"{e.Name}: 떠서 나오는 건 도깨비불만");
                        Assert.LessOrEqual(s.Height, RoomLayouts.Level(3) + 0.01f, $"{e.Name}: 3층까지");
                        high = true;
                    }
                }
                if (high)
                    foreach (int layout in e.Layouts)
                        Assert.Greater(RoomLayouts.All[layout].Platforms.Length, 0, $"{e.Name}: 높이 뜬 도깨비불은 발판이 있는 지형에서만");
            }
        }

        [Test]
        public void 같은_장면이_연달아_나오지_않는다()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var first = EncounterCatalog.Pick(RoomType.Normal, 4, 2, seed, null);
                var second = EncounterCatalog.Pick(RoomType.Normal, 4, 2, seed + 1, first.Id);
                Assert.AreNotEqual(first.Id, second.Id);
            }
        }

        [UnityTest]
        public IEnumerator 런의_방은_구간에_맞는_배경과_바닥을_쓴다()
        {
            yield return TestScene.LoadRun();
            yield return null;
            Assert.AreEqual(1, Object.FindAnyObjectByType<ParallaxBackground>().Zone, "방 1 = 1구간");
            Assert.AreEqual(1, Object.FindAnyObjectByType<FloorDecorator>().Zone);

            GameTuning.Current.backgroundZone = 1; // 설정 창의 구간 값과 상관없이
            yield return TestScene.LoadRun(RoomType.Normal, RoomType.Normal);
            yield return null;
            Assert.AreEqual(3, RunState.RoomNumber);
            Assert.AreEqual(2, Object.FindAnyObjectByType<ParallaxBackground>().Zone, "방 3 = 2구간");
            Assert.AreEqual(2, Object.FindAnyObjectByType<FloorDecorator>().Zone);
            Assert.IsNotNull(Room.Encounter);
            Assert.GreaterOrEqual(2, Room.Encounter.MinZone);
            Assert.Contains(Room.LayoutIndex, Room.Encounter.Layouts, "지형은 장면이 허용한 것");

            yield return TestScene.LoadPrototype();
            yield return null;
            Assert.AreEqual(GameTuning.Current.backgroundZone, Object.FindAnyObjectByType<ParallaxBackground>().Zone, "연습 모드는 설정 값 그대로");
        }

        [UnityTest]
        public IEnumerator 늦게_나오는_몬스터는_시간이_지나면_들어온다()
        {
            EncounterCatalog.ForcedId = "stagger"; // 첫 웨이브: 바로 하나 · 1.2초 뒤 하나 · 2.4초 뒤 하나
            yield return TestScene.LoadRun(RoomType.Normal);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            Assert.AreEqual("stagger", Room.Encounter.Id);
            Assert.AreEqual(1, Enemies().Length);
            Assert.AreEqual(2, Room.PendingSpawns);
            yield return WaitUntil(() => Enemies().Length == 2, 2f);
            yield return WaitUntil(() => Enemies().Length == 3, 2f);
            Assert.AreEqual(0, Room.PendingSpawns);
        }

        [UnityTest]
        public IEnumerator 다_잡으면_늦게_나올_몬스터를_기다리지_않는다()
        {
            EncounterCatalog.ForcedId = "stagger";
            yield return TestScene.LoadRun(RoomType.Normal);
            GameTuning.Current.godMode = true;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            foreach (var e in Enemies()) e.TakeDamage(99999, 1f, false);
            yield return null;
            yield return null;
            Assert.AreEqual(1, Enemies().Length, "바로 다음 몬스터가 나온다");
            Assert.AreEqual(1, Room.PendingSpawns);
        }

        [UnityTest]
        public IEnumerator 엄호_사격_장면의_도깨비불은_발판_위에서_나오고_발판을_벗어나지_않는다()
        {
            EncounterCatalog.ForcedId = "cover";
            yield return TestScene.LoadRun(RoomType.Normal, RoomType.Normal, RoomType.Normal);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            var ranged = Enemies().Where(e => e.Kind == EnemyKind.Ranged).ToArray();
            Assert.AreEqual(1, ranged.Length);
            var r = ranged[0];
            Assert.IsTrue(r.IsConfined, "발판에 묶여 있다");
            // 발판 위에 서 있다: 발판 윗면 + 몸 절반 (땅 위 도깨비불과 같은 뜬 높이)
            var layout = Room.Layout;
            bool onPlatform = layout.Platforms.Any(p =>
                Mathf.Abs(r.transform.position.y - (RoomLayouts.Top(p) + r.transform.localScale.y * 0.5f)) < 0.05f &&
                Mathf.Abs(r.transform.position.x - p.X) <= p.Width * 0.5f);
            Assert.IsTrue(onPlatform, $"허공이 아니라 발판 위에서 나온다 (y={r.transform.position.y:0.00})");
            var melee = Enemies().Where(e => e.Kind == EnemyKind.Melee).ToArray();
            Assert.AreEqual(2, melee.Length);
            foreach (var m in melee) Assert.Less(m.transform.position.y, 1.5f, "창귀는 땅 위");
            // 플레이어는 왼쪽 끝(멀리) — 다가오려 해도 발판 끝에서 멈춘다
            float y0 = r.transform.position.y;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                Assert.GreaterOrEqual(r.transform.position.x, r.ConfineMin - 0.001f, "발판 밖(허공)으로 나가지 않는다");
                Assert.LessOrEqual(r.transform.position.x, r.ConfineMax + 0.001f);
                yield return null;
            }
            Assert.AreEqual(y0, r.transform.position.y, 0.01f, "높이 그대로");
            Assert.AreEqual(r.ConfineMin, r.transform.position.x, 0.05f, "플레이어 쪽 발판 끝까지 와서 멈춘다");
        }

        [UnityTest]
        public IEnumerator 협공_장면은_플레이어_뒤에서도_나온다()
        {
            EncounterCatalog.ForcedId = "pincer";
            RoomLayouts.ForcedIndex = RoomLayouts.Stairs; // 구멍 없는 지형 — 탑(가운데 구멍)이 뽑히면 x=0에 세운 플레이어가 떨어져 시작 자리로 돌아간다
            yield return TestScene.LoadRun(RoomType.Normal, RoomType.Normal);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            var party = Object.FindAnyObjectByType<PlayerParty>();
            // 웨이브 전에 플레이어를 방 가운데로 옮겨 두면 뒤(왼쪽)에서 들어올 자리가 있다
            party.transform.position = new Vector3(0f, party.transform.position.y, 0f);
            party.GetComponent<Rigidbody2D>().position = party.transform.position;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            yield return WaitUntil(() => Enemies().Length == 2, 4f);
            Assert.IsTrue(Enemies().Any(e => e.transform.position.x < party.transform.position.x - 2f), "플레이어 뒤에 하나");
            Assert.IsTrue(Enemies().Any(e => e.transform.position.x > party.transform.position.x + 2f), "앞에 하나");
        }

        [UnityTest]
        public IEnumerator 지나온_쪽에서는_화면_안에서만_나온다()
        {
            yield return TestScene.LoadRun();
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            var party = Object.FindAnyObjectByType<PlayerParty>();
            var pos = new Vector3(12f, party.transform.position.y, 0f); // 방 오른쪽까지 나아간 상태
            party.transform.position = pos;
            party.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            CameraRig.Instance.SnapToTarget();
            yield return null;
            var cam = Camera.main;
            float camLeft = cam.transform.position.x - cam.orthographicSize * cam.aspect;
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();

            var melee = spawner.SpawnEnemyAt(EnemyKind.Melee, -15f, 0f, false); // 한참 지나온 자리
            var ranged = spawner.SpawnEnemyAt(EnemyKind.Ranged, pos.x - 14f, 2f, false); // 등 뒤 14 (화면 밖)
            foreach (var e in new[] { melee, ranged })
            {
                Assert.Greater(e.transform.position.x - e.HalfWidth, camLeft, $"{e.Kind}: 화면 안에서 나온다");
                Assert.Less(e.transform.position.x, pos.x, "뒤쪽은 그대로 뒤쪽");
                Assert.GreaterOrEqual(pos.x - e.transform.position.x, EnemySpawner.MinSpawnGap - 0.01f, "플레이어 바로 옆은 아니다");
            }
            var ahead = spawner.SpawnEnemyAt(EnemyKind.Melee, 20f, 0f, false);
            Assert.Greater(ahead.transform.position.x, pos.x, "앞쪽은 손대지 않는다");
        }

        [UnityTest]
        public IEnumerator 뒤에_보이는_자리가_없으면_앞에서_나온다()
        {
            yield return TestScene.LoadRun();
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            var cam = Camera.main;
            cam.GetComponent<CameraRig>().enabled = false;
            var party = Object.FindAnyObjectByType<PlayerParty>();
            float camLeft = cam.transform.position.x - cam.orthographicSize * cam.aspect;
            var pos = new Vector3(camLeft + 2f, party.transform.position.y, 0f); // 화면 왼쪽 끝 가까이
            party.transform.position = pos;
            party.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            yield return null;
            var e = Object.FindAnyObjectByType<EnemySpawner>().SpawnEnemyAt(EnemyKind.Melee, pos.x - 6f, 0f, false);
            Assert.Greater(e.transform.position.x, pos.x, "뒤가 화면 밖이면 앞에서");
        }

        [UnityTest]
        public IEnumerator 구간이_깊을수록_싸우는_방이_길다()
        {
            float[] expected = { 1f, 1f, 1.3f, 1.3f, 1.6f };
            for (int n = 2; n <= 5; n += 1)
            {
                RoomLayouts.ForcedIndex = RoomLayouts.Stairs;
                yield return TestScene.LoadRun(Enumerable.Repeat(RoomType.Normal, n - 1).ToArray());
                yield return null;
                float scale = expected[n - 1];
                Assert.AreEqual(scale, Room.ArenaScale, 0.001f, $"방 {n}");
                float half = RoomLayouts.BaseArenaHalf * scale;
                var spawner = Object.FindAnyObjectByType<EnemySpawner>();
                Assert.AreEqual(half, spawner.ArenaMaxX, 0.01f);
                Assert.AreEqual(half + 1f, CameraRig.Instance.ArenaMaxX, 0.01f, "카메라 끝 = 벽 안쪽 면");
                var wallR = GameObject.Find("Wall_R").transform;
                Assert.AreEqual(half + 1f, wallR.position.x - wallR.localScale.x * 0.5f, 0.01f, "오른쪽 벽 안쪽 면");
                var party = Object.FindAnyObjectByType<PlayerParty>();
                Assert.AreEqual(-half + 2f, party.transform.position.x, 0.3f, "왼쪽 끝에서 시작");
                Assert.IsTrue(GroundProbe.HasGround(-half + 0.5f, 0f) && GroundProbe.HasGround(half - 0.5f, 0f), "늘어난 끝까지 바닥");
                var widest = RoomLayouts.All[RoomLayouts.Stairs].Platforms.Max(p => p.X + p.Width * 0.5f);
                Assert.AreEqual(widest * scale, Room.Layout.Platforms.Max(p => p.X + p.Width * 0.5f), 0.01f, "발판도 같은 비율로");
            }
            // 보상·상점·보스방과 연습 모드는 그대로
            yield return TestScene.LoadRun(RoomType.Normal, RoomType.Normal, RoomType.Reward);
            Assert.AreEqual(1f, Room.ArenaScale);
            yield return TestScene.LoadPrototype();
            Assert.AreEqual(1f, Room.ArenaScale);
        }

        [UnityTest]
        public IEnumerator 창귀는_발판_위_플레이어를_아래서_기다렸다가_내려오면_바로_공격한다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.hideRanged = true;
            yield return null;
            var party = Object.FindAnyObjectByType<PlayerParty>();
            var rb = party.GetComponent<Rigidbody2D>();
            void Put(Vector3 pos)
            {
                party.transform.position = pos;
                rb.position = pos;
                rb.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            // 기본 지형 가운데 발판(2층) 위
            Put(new Vector3(0f, RoomLayouts.Level(2) + party.transform.localScale.y * 0.5f + 0.05f, 0f));
            var melee = Enemies().First(e => e.Kind == EnemyKind.Melee);
            bool woundUp = false;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                woundUp |= melee.IsWindingUp;
                yield return null;
            }
            Assert.IsTrue(melee.WaitingBelow, "발판 아래에서 기다린다");
            Assert.LessOrEqual(Mathf.Abs(melee.transform.position.x - party.transform.position.x), 0.8f, "바로 아래까지 왔다");
            Assert.IsFalse(woundUp, "닿지 않는 공격을 헛휘두르지 않는다");

            // 내려오면 바로 공격 준비
            Put(new Vector3(melee.transform.position.x - 1.6f, party.transform.localScale.y * 0.5f + 0.05f, 0f));
            float waited = 0f;
            while (!melee.IsWindingUp && waited < 1f) { waited += Time.deltaTime; yield return null; }
            Assert.IsTrue(melee.IsWindingUp, "내려오자마자 공격");
            Assert.Less(waited, 0.5f);
            Assert.IsFalse(melee.WaitingBelow);
        }

        [UnityTest]
        public IEnumerator 도깨비불은_불덩이가_날아가는_동안_공격권을_쥐고_있다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.hideMelee = true;
            yield return null;
            var ranged = Enemies().First(e => e.Kind == EnemyKind.Ranged);
            float t = 0f;
            while (!ranged.IsRecovering && t < 8f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(ranged.IsRecovering, "불덩이를 쐈다");
            Assert.IsTrue(AttackTokens.Holds(ranged), "쏜 직후에도 공격권을 쥐고 있다");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, AttackTokens.Count, "날아가는 동안 (회복 동작이 끝나도)");
            yield return new WaitForSeconds(0.9f);
            Assert.IsFalse(AttackTokens.Holds(ranged) && !ranged.IsWindingUp, "다 날아간 뒤엔 돌려준다");
        }

        /// <summary>몬스터를 플레이어 양옆 사거리 안으로 모은다 (모두가 바로 공격하려는 상황).</summary>
        private static IEnumerator GatherAroundPlayer(int count)
        {
            yield return WaitUntil(() => Enemies().Length >= count);
            var player = Object.FindAnyObjectByType<PlayerParty>().transform.position;
            var all = Enemies();
            for (int i = 0; i < all.Length; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                var pos = new Vector3(player.x + side * (1.6f + 0.15f * (i / 2)), all[i].transform.position.y, 0f);
                all[i].transform.position = pos;
                all[i].GetComponent<Rigidbody2D>().position = pos;
            }
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator 몬스터가_많아도_동시에_공격하는_수와_시작_간격이_제한된다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.hideRanged = true;
            GameTuning.Current.enemiesPerKind = 5;
            GameTuning.Current.attackIntervalMin = 0.3f; // 다들 최대한 자주 치려 한다
            GameTuning.Current.attackIntervalMax = 0.3f;
            yield return GatherAroundPlayer(5);
            int maxWinding = 0;
            var starts = new List<float>();
            var wasWinding = new Dictionary<EnemyController, bool>();
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                var all = Enemies();
                // 모두 플레이어 곁으로 (사거리 안)
                maxWinding = Mathf.Max(maxWinding, all.Count(e => e.IsWindingUp));
                Assert.LessOrEqual(AttackTokens.Count, GameTuning.Current.maxAttackers);
                foreach (var e in all)
                {
                    bool w = e.IsWindingUp;
                    if (w && !(wasWinding.TryGetValue(e, out var before) && before)) starts.Add(Time.time);
                    wasWinding[e] = w;
                }
                yield return null;
            }
            Assert.AreEqual(5, Enemies().Length);
            Assert.LessOrEqual(maxWinding, 2, "예비 모션 중인 몬스터는 둘까지");
            Assert.Greater(starts.Count, 3, "공격은 계속 나온다");
            for (int i = 1; i < starts.Count; i++)
                Assert.GreaterOrEqual(starts[i] - starts[i - 1], GameTuning.Current.attackStagger - 0.05f, "공격 시작 사이 최소 간격");
        }

        [UnityTest]
        public IEnumerator 공격권_제한을_끄면_예전처럼_한꺼번에_공격한다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.hideRanged = true;
            GameTuning.Current.enemiesPerKind = 5;
            GameTuning.Current.attackIntervalMin = 0.3f;
            GameTuning.Current.attackIntervalMax = 0.3f;
            GameTuning.Current.maxAttackers = 0;
            yield return GatherAroundPlayer(5);
            int maxWinding = 0;
            float minGap = float.MaxValue, last = float.NegativeInfinity;
            var wasWinding = new Dictionary<EnemyController, bool>();
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                var all = Enemies();
                maxWinding = Mathf.Max(maxWinding, all.Count(e => e.IsWindingUp));
                foreach (var e in all)
                {
                    bool w = e.IsWindingUp;
                    if (w && !(wasWinding.TryGetValue(e, out var before) && before))
                    {
                        minGap = Mathf.Min(minGap, Time.time - last);
                        last = Time.time;
                    }
                    wasWinding[e] = w;
                }
                yield return null;
            }
            Assert.IsTrue(maxWinding > 2 || minGap < GameTuning.Current.attackStagger * 0.5f, $"제한이 없으면 겹친다 (최대 {maxWinding}마리, 최소 간격 {minGap:0.00}초)");
        }
    }
}
