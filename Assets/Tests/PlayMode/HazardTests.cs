using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>바닥 구멍(추락 → 피해 + 안전한 자리로 복귀)과 피격 직후 무적.</summary>
    public class HazardTests
    {
        private static PlayerParty Party => Object.FindAnyObjectByType<PlayerParty>();
        private static RoomController Room => Object.FindAnyObjectByType<RoomController>();

        private static void MovePlayer(Vector2 pos)
        {
            var p = Party;
            p.transform.position = pos;
            var rb = p.GetComponent<Rigidbody2D>();
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void TearDown() => RoomLayouts.ForcedIndex = -1;

        // ───────────── 구멍 배치 ─────────────

        [Test]
        public void 구멍은_점프로_건널_수_있고_시작_자리와_문_앞에서_떨어져_있고_개수_한도_안이다()
        {
            foreach (var layout in RoomLayouts.All)
            {
                Assert.LessOrEqual(layout.Pits.Length, RoomLayouts.MaxPits, layout.Name);
                foreach (var pit in layout.Pits)
                {
                    Assert.LessOrEqual(pit.Width, RoomLayouts.MaxPitWidth, $"{layout.Name}: 건널 수 있는 폭");
                    Assert.LessOrEqual(Mathf.Abs(pit.X) + pit.Width * 0.5f, RoomLayouts.PitZone, $"{layout.Name}: 시작·문 앞은 안전해야 한다");
                }
                // 구멍끼리도 점프 한 번에 이어 건너지 않게 충분히 떨어져 있다
                var sorted = layout.Pits.OrderBy(p => p.X).ToArray();
                for (int i = 1; i < sorted.Length; i++) Assert.GreaterOrEqual(sorted[i].Left - sorted[i - 1].Right, 4f, layout.Name);
            }
            Assert.AreEqual(0, RoomLayouts.All[RoomLayouts.Basic].Pits.Length, "첫 방(기본)에는 구멍이 없다");
            Assert.AreEqual(0, RoomLayouts.All[RoomLayouts.Flat].Pits.Length, "보상·상점(평지)에는 구멍이 없다");
        }

        [Test]
        public void 바닥_구간은_구멍을_빼고_이어진다()
        {
            var zig = RoomLayouts.All[RoomLayouts.Zigzag];
            var iv = RoomLayouts.GroundIntervals(zig);
            Assert.AreEqual(zig.Pits.Length + 1, iv.Count);
            Assert.AreEqual(-RoomLayouts.GroundHalfExtent, iv[0].x0, 0.001f);
            Assert.AreEqual(RoomLayouts.GroundHalfExtent, iv[^1].x1, 0.001f);
            for (int i = 0; i < zig.Pits.Length; i++)
            {
                var pit = zig.Pits.OrderBy(p => p.X).ElementAt(i);
                Assert.AreEqual(pit.Left, iv[i].x1, 0.001f);
                Assert.AreEqual(pit.Right, iv[i + 1].x0, 0.001f);
            }
            Assert.AreEqual(1, RoomLayouts.GroundIntervals(RoomLayouts.All[RoomLayouts.Basic]).Count);
        }

        // ───────────── 추락 ─────────────

        [UnityTest]
        public IEnumerator 구멍에_빠지면_피해를_입고_떨어지기_전_자리로_돌아온다()
        {
            RoomLayouts.ForcedIndex = RoomLayouts.Zigzag;
            yield return TestScene.LoadRun(RoomType.Normal);
            GameTuning.Current.enemyPassive = true;
            var pit = RoomLayouts.All[RoomLayouts.Zigzag].Pits[0]; // x -5.5, 폭 3.4
            // 구멍 왼쪽 바닥(가장자리에서 충분히 안쪽)에 서 있는다 → 안전한 자리로 기억
            float safeX = pit.Left - 2.5f;
            MovePlayer(new Vector2(safeX, 0.8f));
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(safeX, Room.SafePosition.x, 0.3f, "안전한 자리를 기억");
            int hp = Party.Hp;

            // 구멍 한가운데 위로 옮기면 떨어진다
            MovePlayer(new Vector2(pit.X, 0.8f));
            float t = 0f;
            while (Room.FallCount == 0 && t < 4f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(1, Room.FallCount, "구멍에 빠졌다");
            Assert.AreEqual(hp - RoomController.FallDamage, Party.Hp, "추락 피해(방어 패시브 무시)");
            Assert.AreEqual(safeX, Party.transform.position.x, 0.3f, "떨어지기 전 자리로 돌아왔다");
            Assert.Greater(Party.transform.position.y, 0f);
            Assert.IsTrue(Party.IsHurtInvulnerable, "돌아온 직후 무적");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, Room.FallCount, "다시 떨어지지 않는다");
        }

        private static EnemyController PlaceEnemy(EnemyKind kind, float x)
        {
            var enemy = Object.FindObjectsByType<EnemyController>().First(e => !e.IsDead && e.Kind == kind);
            foreach (var other in Object.FindObjectsByType<EnemyController>()) if (other != enemy && !other.IsDead) other.Despawn();
            var pos = new Vector3(x, enemy.transform.position.y, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            return enemy;
        }

        [UnityTest]
        public IEnumerator 창귀는_구멍_앞에서_멈추고_도깨비불은_떠서_건너온다()
        {
            RoomLayouts.ForcedIndex = RoomLayouts.Zigzag;
            yield return TestScene.LoadRun(RoomType.Normal);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            var pit = RoomLayouts.All[RoomLayouts.Zigzag].Pits[0]; // [-7.2, -3.8]
            yield return new WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            MovePlayer(new Vector2(-13f, 0.8f)); // 구멍 왼쪽 (멀리)

            // 창귀: 구멍 오른쪽에서 플레이어 쪽으로 걸어오다 구멍 앞에서 멈춘다
            var melee = PlaceEnemy(EnemyKind.Melee, -1f);
            float minLeft = float.MaxValue;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                minLeft = Mathf.Min(minLeft, melee.transform.position.x - melee.HalfWidth);
                yield return null;
            }
            Assert.GreaterOrEqual(minLeft, pit.Right - 0.05f, "창귀는 구멍 위로 들어가지 않는다");
            Assert.Less(melee.transform.position.x - melee.HalfWidth, pit.Right + 0.6f, "구멍 바로 앞까지는 온다");
            Assert.Greater(melee.transform.position.y, 0f);
        }

        [UnityTest]
        public IEnumerator 도깨비불은_구멍_위를_떠서_건너온다()
        {
            RoomLayouts.ForcedIndex = RoomLayouts.Zigzag;
            yield return TestScene.LoadRun(RoomType.Normal);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            var pit = RoomLayouts.All[RoomLayouts.Zigzag].Pits[0];
            yield return new WaitUntil(() => Room.CurrentPhase == RoomController.Phase.Fighting);
            MovePlayer(new Vector2(-17f, 0.8f)); // 사정거리(7) 밖 → 다가온다
            var ranged = PlaceEnemy(EnemyKind.Ranged, -1f);
            yield return new WaitForSeconds(4f);
            Assert.Less(ranged.transform.position.x, pit.Left, "도깨비불은 구멍을 건너왔다");
        }

        [UnityTest]
        public IEnumerator 구멍_옆_땅은_속까지_판정이_있어_떨어질_때_뚫고_지나가지_않는다()
        {
            RoomLayouts.ForcedIndex = RoomLayouts.Zigzag;
            yield return TestScene.LoadRun(RoomType.Normal);
            yield return null;
            var pit = RoomLayouts.All[RoomLayouts.Zigzag].Pits[0];
            var filter = new ContactFilter2D { useTriggers = false };
            var hits = new Collider2D[4];
            Assert.Greater(Physics2D.OverlapPoint(new Vector2(pit.Left - 0.3f, -3.5f), filter, hits), 0, "구멍 왼쪽 벽: 깊은 곳까지 땅 판정");
            Assert.Greater(Physics2D.OverlapPoint(new Vector2(pit.Right + 0.3f, -3.5f), filter, hits), 0, "구멍 오른쪽 벽");
            Assert.AreEqual(0, Physics2D.OverlapPoint(new Vector2(pit.X, -3.5f), filter, hits), "구멍 안은 비어 있다");
            Assert.IsFalse(GroundProbe.HasGround(pit.X, 0f));
            Assert.IsTrue(GroundProbe.HasGround(pit.Left - 1f, 0f));
            // 걷는 몬스터가 구멍 위에 생기려 하면 가까운 땅으로 옮긴다
            float x = GroundProbe.NudgeOntoGround(pit.X, 0.5f, 0f, -22f, 22f);
            Assert.IsTrue(x + 0.5f <= pit.Left + 0.001f || x - 0.5f >= pit.Right - 0.001f, $"구멍 밖으로 옮김: {x}");
        }

        [UnityTest]
        public IEnumerator 구멍이_없는_방과_연습_모드에는_추락_감시가_아무_일도_하지_않는다()
        {
            yield return TestScene.LoadPrototype();
            Assert.AreEqual(0, Room.Layout.Pits.Length);
            Assert.AreEqual(0, Room.FallCount);
        }

        // ───────────── 피격 직후 무적 ─────────────

        [UnityTest]
        public IEnumerator 맞으면_잠깐_무적이라_연달아_맞지_않고_시간이_지나면_다시_맞는다()
        {
            yield return TestScene.LoadPrototype();
            var party = Party;
            int first = party.TakeDamage(20);
            Assert.Greater(first, 0);
            Assert.IsTrue(party.IsHurtInvulnerable);
            int hp = party.Hp;
            Assert.AreEqual(0, party.TakeDamage(20), "무적 중에는 안 맞는다");
            Assert.AreEqual(hp, party.Hp);
            Assert.Greater(party.TakeDamage(20, ignoreInvulnerability: true), 0, "환경 피해는 무적도 무시");

            yield return new WaitForSeconds(party.HurtInvulnerableTime + 0.1f);
            Assert.IsFalse(party.IsHurtInvulnerable);
            Assert.Greater(party.TakeDamage(5), 0, "무적이 끝나면 다시 맞는다");
        }

        [UnityTest]
        public IEnumerator 무적_중에는_적_공격이_몸을_통과하고_끝나면_다시_맞는다()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            var party = Party;
            party.TakeDamage(10);
            int hp = party.Hp;
            Vector2 p = party.transform.position;
            EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x + 0.72f, p.y), Vector2.left, new Vector2(0.6f, 0.6f), 10f, 6f, Color.red, 10);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(hp, party.Hp, "무적 중엔 공격이 통과");

            yield return new WaitForSeconds(party.HurtInvulnerableTime);
            EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x + 0.72f, p.y), Vector2.left, new Vector2(0.6f, 0.6f), 10f, 6f, Color.red, 10);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(party.Hp, hp, "무적이 끝난 뒤엔 맞는다");
        }

        [UnityTest]
        public IEnumerator 패링은_무적_중에도_그대로_된다()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            var party = Party;
            party.TakeDamage(10);
            var defense = party.GetComponent<PlayerDefense>();
            Assert.IsTrue(defense.TryActivate());
            Vector2 p = party.transform.position;
            EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x + 0.72f, p.y), Vector2.left, new Vector2(0.6f, 0.6f), 10f, 6f, Color.red, 10);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(defense.SucceededThisActive, "무적 중에도 패링 성공(도력·반격)");
        }
    }
}
