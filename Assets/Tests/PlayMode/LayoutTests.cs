using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>방 지형(발판 배치): 방마다 다르게, 하지만 늘 올라갈 수 있고 문 앞을 가리지 않게.</summary>
    public class LayoutTests
    {
        private const float PlatformThickness = 0.4f;
        private const float DoubleJumpReach = 2.9f;  // 이단 점프로 닿는 높이(약 3.1)에서 여유를 둔 값
        private const float JumpGap = 3.5f;          // 올라가며 건널 수 있는 가로 간격

        private static float Surface(PlatformSpec p) => p.Y + PlatformThickness * 0.5f;

        [Test]
        public void 모든_발판은_바닥이나_다른_발판에서_점프로_올라갈_수_있다()
        {
            foreach (var layout in RoomLayouts.All)
            {
                Assert.LessOrEqual(layout.Platforms.Length, RoomLayouts.MaxPlatforms, layout.Name);
                foreach (var p in layout.Platforms)
                {
                    if (Surface(p) <= DoubleJumpReach) continue; // 바닥에서 바로
                    bool reachable = layout.Platforms.Any(q =>
                    {
                        if (q.Equals(p) || Surface(q) >= Surface(p) || Surface(p) - Surface(q) > DoubleJumpReach) return false;
                        float gap = Mathf.Max(0f, Mathf.Abs(p.X - q.X) - (p.Width + q.Width) * 0.5f);
                        return gap <= JumpGap;
                    });
                    Assert.IsTrue(reachable, $"{layout.Name}: x={p.X}, y={p.Y} 발판에 올라갈 수 없다");
                }
            }
        }

        [Test]
        public void 발판은_왼쪽_끝의_다음_방_문_글자를_가리지_않는다()
        {
            foreach (var layout in RoomLayouts.All)
                foreach (var p in layout.Platforms)
                    Assert.GreaterOrEqual(p.X - p.Width * 0.5f, -RoomLayouts.MaxLeftReach, $"{layout.Name}: x={p.X}");
        }

        [Test]
        public void 발판은_화면_위로_너무_높지_않다()
        {
            foreach (var layout in RoomLayouts.All)
                foreach (var p in layout.Platforms) Assert.LessOrEqual(Surface(p), 7.0f, layout.Name);
        }

        [Test]
        public void 방마다_지형이_다르고_바로_앞_방과_같은_지형이_연달아_나오지_않는다()
        {
            // 첫 방은 기본, 보상·상점은 평지, 마지막은 탑
            Assert.AreEqual(RoomLayouts.Basic, RoomLayouts.Pick(true, RoomType.Normal, 1, 7, RoomLayouts.Basic));
            Assert.AreEqual(RoomLayouts.Flat, RoomLayouts.Pick(true, RoomType.Shop, 3, 7, 1));
            Assert.AreEqual(RoomLayouts.Flat, RoomLayouts.Pick(true, RoomType.Reward, 3, 7, 1));
            Assert.AreEqual(RoomLayouts.Tower, RoomLayouts.Pick(true, RoomType.Elite, RunState.TotalRooms, 7, 1));
            Assert.AreEqual(RoomLayouts.Basic, RoomLayouts.Pick(false, RoomType.Normal, 4, 7, 1), "연습 모드는 기본");
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                int prev = RoomLayouts.Basic;
                for (int room = 2; room < RunState.TotalRooms; room++)
                {
                    int i = RoomLayouts.Pick(true, RoomType.Normal, room, seed, prev);
                    Assert.AreNotEqual(prev, i, $"seed {seed} 방 {room}: 앞 방과 같은 지형");
                    seen.Add(i);
                    prev = i;
                }
            }
            Assert.GreaterOrEqual(seen.Count, 4, "여러 지형이 골고루 나온다");
        }

        [UnityTest]
        public IEnumerator 연습_모드는_예전_발판_세_개_그대로다()
        {
            yield return TestScene.LoadPrototype();
            yield return null;
            var deco = Object.FindAnyObjectByType<FloorDecorator>();
            Assert.AreEqual(3, deco.ActivePlatformCount);
            Assert.AreEqual(RoomLayouts.Basic, RoomController.Instance.LayoutIndex);
            var platform = GameObject.Find("Platform_Top");
            Assert.AreEqual(new Vector3(0f, 4.4f, 0f), platform.transform.position);
        }

        [UnityTest]
        public IEnumerator 런의_방은_레이아웃대로_발판이_놓이고_남는_발판은_꺼진다()
        {
            foreach (int index in new[] { RoomLayouts.Stairs, RoomLayouts.Zigzag, RoomLayouts.Tower, RoomLayouts.Roof, RoomLayouts.Flat })
            {
                RoomLayouts.ForcedIndex = index;
                yield return TestScene.LoadRun(RoomType.Normal);
                yield return null;
                var layout = RoomLayouts.All[index];
                var deco = Object.FindAnyObjectByType<FloorDecorator>();
                Assert.AreEqual(layout.Platforms.Length, deco.ActivePlatformCount, layout.Name);
                var colliders = Object.FindObjectsByType<PlatformEffector2D>().Where(e => e.gameObject.activeInHierarchy).ToArray();
                Assert.AreEqual(layout.Platforms.Length, colliders.Length, $"{layout.Name}: 켜진 발판 판정 수");
                foreach (var spec in layout.Platforms)
                    Assert.IsTrue(colliders.Any(c => Mathf.Abs(c.transform.position.x - spec.X) < 0.01f && Mathf.Abs(c.transform.position.y - spec.Y) < 0.01f
                        && Mathf.Abs(c.transform.localScale.x - spec.Width) < 0.01f), $"{layout.Name}: {spec.X},{spec.Y}");
            }
            RoomLayouts.ForcedIndex = -1;
        }

        [TearDown]
        public void TearDown() => RoomLayouts.ForcedIndex = -1;
    }
}
