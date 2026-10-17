using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>한 판(런)의 방 흐름: 웨이브 → 방 클리어 → 문 고르기 → 다음 방 (스컬 방식).</summary>
    public class RunTests
    {
        private static RoomController Room => Object.FindAnyObjectByType<RoomController>();
        private static PlayerParty Party => Object.FindAnyObjectByType<PlayerParty>();

        private static EnemyController[] Enemies() =>
            Object.FindObjectsByType<EnemyController>().Where(e => !e.IsDead).ToArray();

        private static IEnumerator KillAll()
        {
            foreach (var e in Enemies()) e.TakeDamage(99999, 1f, false);
            yield return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> cond, float timeout = 8f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 안에 조건이 안 맞음");
        }

        [SetUp]
        public void SetUp() => GameTuning.UseDefaultsForTests();

        [Test]
        public void 문은_서로_다른_두_종류이고_마지막_방_직전에는_하나다()
        {
            RunState.Begin();
            for (int i = 0; i < 200; i++)
            {
                var o = RunState.RollOptions(new System.Random(i));
                Assert.AreEqual(2, o.Length);
                Assert.AreNotEqual(o[0], o[1], "두 문은 서로 다른 방");
            }
            for (int i = 0; i < RunState.TotalRooms - 2; i++) RunState.Enter(RoomType.Normal);
            var last = RunState.RollOptions();
            Assert.AreEqual(1, last.Length, "마지막 방 직전은 문이 하나");
            Assert.AreEqual(RoomType.Elite, last[0], "마지막 관문은 엘리트");
            RunState.Reset();
        }

        [UnityTest]
        public IEnumerator 테스트로_플레이_씬을_바로_열면_방_흐름_없이_몬스터가_계속_나온다()
        {
            yield return TestScene.LoadPrototype();
            Assert.IsFalse(RunState.Active);
            Assert.IsFalse(Room.IsRunRoom);
            yield return new WaitForSeconds(0.3f);
            Assert.GreaterOrEqual(Enemies().Length, 2, "예전 연습 모드: 종류마다 몬스터 유지");
        }

        [UnityTest]
        public IEnumerator 일반방은_웨이브가_이어지고_다_깨면_문_두_개가_열린다()
        {
            yield return TestScene.LoadRun();
            GameTuning.Current.godMode = true;
            Assert.IsTrue(Room.IsRunRoom);
            Assert.AreEqual(RoomType.Normal, RunState.Current);
            Assert.AreEqual(0, Enemies().Length, "시작 직후엔 아직 몬스터가 없다 (잠깐 뒤 웨이브)");

            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            Assert.Greater(Enemies().Length, 0);
            Assert.AreEqual(0, Room.WaveIndex);
            Assert.AreEqual(0, Room.Gates.Count, "싸우는 동안엔 문이 없다");

            yield return KillAll();
            yield return WaitUntil(() => Room.WaveIndex == 1 && Room.CurrentPhase == RoomController.Phase.Fighting);
            Assert.Greater(Enemies().Length, 0, "두 번째 웨이브");

            yield return KillAll();
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared);
            Assert.AreEqual(2, Room.Gates.Count, "방을 깨면 문이 둘");
            Assert.AreNotEqual(Room.Gates[0].Type, Room.Gates[1].Type);
            Assert.Less(Room.Gates[0].transform.position.x, 0f, "한쪽은 왼쪽 끝");
            Assert.Greater(Room.Gates[1].transform.position.x, 0f, "한쪽은 오른쪽 끝");
        }

        [UnityTest]
        public IEnumerator 문으로_들어가면_다음_방으로_넘어가고_체력과_도력이_이어진다()
        {
            yield return TestScene.LoadRun();
            GameTuning.Current.godMode = true;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            Party.TakeDamage(30);
            int hp = Party.Hp;
            Object.FindAnyObjectByType<SkillGauge>().Add(6);
            yield return KillAll();
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting && Room.WaveIndex == 1);
            yield return KillAll();
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared);

            var gate = Room.Gates[1];
            var type = gate.Type;
            gate.Enter();
            yield return null; yield return null; yield return null;
            Assert.AreEqual(2, RunState.RoomNumber);
            Assert.AreEqual(type, RunState.Current);
            Assert.AreEqual(GameManager.RunSceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(hp, Party.Hp, "체력 이어짐");
            Assert.AreEqual(6, Object.FindAnyObjectByType<SkillGauge>().Value, "도력 이어짐");
            Assert.IsTrue(Room.IsRunRoom);
        }

        [UnityTest]
        public IEnumerator 엘리트방은_체력이_많은_엘리트가_나온다()
        {
            yield return TestScene.LoadRun(RoomType.Elite);
            GameTuning.Current.godMode = true;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            var elites = Enemies().Where(e => e.IsElite).ToArray();
            Assert.AreEqual(1, elites.Length);
            var normal = Enemies().First(e => !e.IsElite && e.Kind == elites[0].Kind);
            Assert.AreEqual(1f, normal.HpRatio);
            // 같은 한 방에 엘리트는 체력 3배: 같은 피해로 비율이 더 천천히 준다
            elites[0].TakeDamage(60, 1f, false);
            normal.TakeDamage(60, 1f, false);
            Assert.Greater(elites[0].HpRatio, normal.HpRatio);
        }

        [UnityTest]
        public IEnumerator 보상방과_상점방은_몬스터_없이_바로_문이_열린다()
        {
            foreach (var type in new[] { RoomType.Reward, RoomType.Shop })
            {
                yield return TestScene.LoadRun(type);
                yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared);
                Assert.AreEqual(0, Enemies().Length);
                Assert.AreEqual(2, Room.Gates.Count);
            }
        }

        [UnityTest]
        public IEnumerator 마지막_방을_깨면_사당으로_나가는_문이_열리고_판이_끝난다()
        {
            var rooms = Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2).Append(RoomType.Elite).ToArray();
            yield return TestScene.LoadRun(rooms);
            Assert.IsTrue(RunState.IsFinalRoom);
            GameTuning.Current.godMode = true;
            for (int i = 0; i < 4 && Room.CurrentPhase != RoomController.Phase.Cleared; i++)
            {
                yield return new WaitForSeconds(1.4f);
                yield return KillAll();
            }
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared, 12f);
            Assert.AreEqual(1, Room.Gates.Count);
            Assert.IsTrue(Room.Gates[0].IsExit);
            Room.Gates[0].Enter();
            yield return null; yield return null; yield return null;
            Assert.AreEqual(GameManager.HubSceneName, SceneManager.GetActiveScene().name);
            Assert.IsFalse(RunState.Active, "판이 끝났다");
        }

        [UnityTest]
        public IEnumerator 사당_포탈로_들어가면_한_판이_시작된다()
        {
            yield return TestScene.LoadHub();
            var portal = Object.FindAnyObjectByType<ShrinePortal>();
            portal.GetComponent<Interactable>().Interact();
            yield return null; yield return null; yield return null;
            Assert.IsTrue(RunState.Active);
            Assert.AreEqual(1, RunState.RoomNumber);
            Assert.AreEqual(GameManager.RunSceneName, SceneManager.GetActiveScene().name);
        }
    }
}
