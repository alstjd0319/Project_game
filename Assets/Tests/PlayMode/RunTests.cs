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
        public IEnumerator 방에_들어오면_플레이어는_가장_왼쪽에서_시작하고_카메라가_바로_따라온다()
        {
            yield return TestScene.LoadRun();
            var left = Object.FindAnyObjectByType<EnemySpawner>().ArenaMinX;
            Assert.AreEqual(left + 2f, Party.transform.position.x, 0.2f);
            var cam = Camera.main;
            float halfWidth = cam.orthographicSize * cam.aspect;
            Assert.LessOrEqual(Mathf.Abs(cam.transform.position.x - (left + halfWidth - 1f)), 1.5f, "카메라는 왼쪽 끝에 붙어 있다");
            // 연습 모드는 예전 시작 위치
            yield return TestScene.LoadPrototype();
            Assert.AreEqual(-3f, Party.transform.position.x, 0.2f);
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
            Assert.Greater(Room.Gates[0].transform.position.x, Room.Gates[1].transform.position.x, "바깥 문 · 안쪽 문 순서");
            Assert.Greater(Room.Gates[1].transform.position.x, 12f, "문은 둘 다 오른쪽 끝에 나란히 선다");
            Assert.Greater(Room.Gates[0].transform.position.x - Room.Gates[1].transform.position.x, 3.4f, "글자가 겹치지 않을 간격");
            foreach (var gate in Room.Gates) Assert.IsNotNull(gate.GetComponent<DoorEffects>(), "문마다 일렁이는 소용돌이·빛무리");
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

        private static IEnumerator ClearFight()
        {
            for (int i = 0; i < 6 && Room.CurrentPhase != RoomController.Phase.Cleared; i++)
            {
                yield return new WaitForSeconds(1.4f);
                yield return KillAll();
            }
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared, 12f);
        }

        [UnityTest]
        public IEnumerator 몬스터를_잡으면_엽전을_얻는다_엘리트는_훨씬_많다()
        {
            yield return TestScene.LoadRun(RoomType.Elite);
            GameTuning.Current.godMode = true;
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            Assert.AreEqual(0, RunState.Gold);
            var normal = Enemies().First(e => !e.IsElite && e.Kind == EnemyKind.Melee);
            normal.TakeDamage(99999, 1f, false);
            Assert.AreEqual(5, RunState.Gold, "근접 5");
            var elite = Enemies().First(e => e.IsElite);
            elite.TakeDamage(99999, 1f, false);
            Assert.AreEqual(30, RunState.Gold, "엘리트 +25");
        }

        [UnityTest]
        public IEnumerator 엘리트방을_깨면_영혼석_또는_아이템_제단이_뜨고_하나를_고르면_문이_열린다()
        {
            yield return TestScene.LoadRun(RoomType.Elite);
            GameTuning.Current.godMode = true;
            yield return ClearFight();
            Assert.AreEqual(2, Room.Altars.Count);
            Assert.AreEqual(0, Room.Gates.Count, "하나를 고르기 전엔 문이 닫혀 있다");
            Assert.IsTrue(Room.Altars.Any(a => a.Offer.Kind == OfferKind.Soul));
            Assert.IsTrue(Room.Altars.Any(a => a.Offer.Kind == OfferKind.Item));

            var soul = Room.Altars.First(a => a.Offer.Kind == OfferKind.Soul);
            Assert.IsTrue(soul.TryTake());
            Assert.AreEqual(RoomOffer.EliteSoulStones, RunState.SoulStones);
            Assert.AreEqual(2, Room.Gates.Count, "고르면 문이 열린다");
            var other = Room.Altars.First(a => a != soul);
            Assert.IsTrue(other.Locked);
            Assert.IsFalse(other.TryTake(), "하나만 고를 수 있다");
            Assert.AreEqual(0, RunState.Items.Count);
        }

        [UnityTest]
        public IEnumerator 아이템을_고르면_스탯에_더해지고_최대체력_아이템은_체력도_찬다()
        {
            yield return TestScene.LoadRun(RoomType.Reward);
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared);
            Assert.AreEqual(3, Room.Altars.Count, "보상방: 아이템 · 회복 · 영혼석");
            Assert.AreEqual(0, Enemies().Length);
            int max = Party.MaxHp;
            RoomOffer.ForReward(); // 목록 생성이 안전한지
            var amber = RunItemCatalog.All.First(i => i.Id == "amber");
            new RoomOffer { Kind = OfferKind.Item, Item = amber }.Apply();
            Assert.AreEqual(max + 20, Party.MaxHp);
            var red = RunItemCatalog.All.First(i => i.Id == "bell_red");
            Assert.AreEqual(1f, SkillTreeState.MeleeAttackMultiplier, 0.001f);
            RunState.AddItem(red);
            Assert.AreEqual(1.1f, SkillTreeState.MeleeAttackMultiplier, 0.001f);
            Assert.AreEqual(1f, SkillTreeState.RangedAttackMultiplier, 0.001f, "근접 아이템은 원거리에 영향 없음");
        }

        [UnityTest]
        public IEnumerator 보상방에서_체력_회복을_고르면_체력이_차고_문이_열린다()
        {
            yield return TestScene.LoadRun(RoomType.Reward);
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared);
            Party.TakeDamage(50);
            Assert.Less(Party.Hp, Party.MaxHp);
            Room.Altars.First(a => a.Offer.Kind == OfferKind.Heal).TryTake();
            Assert.AreEqual(Party.MaxHp, Party.Hp);
            Assert.AreEqual(2, Room.Gates.Count);
        }

        [UnityTest]
        public IEnumerator 상점은_엽전으로_사고_모자라면_못_사고_문은_바로_열려_있다()
        {
            yield return TestScene.LoadRun(RoomType.Shop);
            yield return WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Cleared);
            Assert.AreEqual(4, Room.Altars.Count);
            Assert.AreEqual(2, Room.Gates.Count, "상점은 문이 바로 열려 있다");
            var heal = Room.Altars.First(a => a.Offer.Kind == OfferKind.Heal);
            Assert.AreEqual(30, heal.Offer.Price);

            Party.TakeDamage(60);
            int hp = Party.Hp;
            Assert.IsFalse(heal.TryTake(), "엽전 0이면 못 산다");
            Assert.AreEqual(hp, Party.Hp);
            Assert.IsFalse(heal.Taken);

            RunState.AddGold(100);
            Assert.IsTrue(heal.TryTake());
            Assert.AreEqual(70, RunState.Gold, "30 지불");
            Assert.Greater(Party.Hp, hp);
            Assert.IsTrue(heal.Taken);
            Assert.IsFalse(heal.TryTake(), "품절");
            // 여러 개 살 수 있다 (하나만 고르는 방이 아니다)
            var gauge = Room.Altars.First(a => a.Offer.Kind == OfferKind.Gauge);
            Assert.IsTrue(gauge.TryTake());
            Assert.AreEqual(50, RunState.Gold);
            Assert.AreEqual(Object.FindAnyObjectByType<SkillGauge>().Max, Object.FindAnyObjectByType<SkillGauge>().Value);
        }

        [UnityTest]
        public IEnumerator 엽전과_아이템은_판이_끝나면_사라지고_영혼석은_남고_새_게임에서_비워진다()
        {
            yield return TestScene.LoadRun();
            RunState.AddGold(40);
            RunState.AddSoulStones(3);
            RunState.AddItem(RunItemCatalog.All[0]);
            GameManager.LoadHub();
            yield return null; yield return null; yield return null;
            Assert.AreEqual(0, RunState.Gold);
            Assert.AreEqual(0, RunState.Items.Count);
            Assert.AreEqual(3, RunState.SoulStones, "영혼석은 판이 끝나도 남는다");
            PlayerSession.Reset();
            Assert.AreEqual(0, RunState.SoulStones, "새 게임은 비운다");
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
