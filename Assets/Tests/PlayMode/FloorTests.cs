using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>바닥 타일 그림: 판정 네모는 그대로 두고 그림만 위에 깔린다.</summary>
    public class FloorTests
    {
        [UnityTest]
        public IEnumerator 플레이_씬_바닥은_타일로_깔리고_판정_네모는_숨는다()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.backgroundZone = 1;
            yield return null;
            yield return null;
            var deco = Object.FindAnyObjectByType<FloorDecorator>();
            Assert.IsNotNull(deco);
            Assert.AreEqual(1, deco.Zone);
            Assert.Greater(deco.TileCount, 30, "윗면·속 채움·벽·발판 타일");
            var ground = GameObject.Find("Ground");
            Assert.IsFalse(ground.GetComponent<SpriteRenderer>().enabled, "그림이 있으면 판정 네모는 숨는다");
            // 바닥 판정은 속 채움 그림만큼 깊다 (구멍에 떨어질 때 옆 땅을 뚫고 지나가지 않게) — 윗면은 y=0 그대로
            Assert.AreEqual(FloorDecorator.GroundDepth, ground.transform.localScale.y, 1e-4f, "판정 깊이 = 속 채움 깊이");
            Assert.AreEqual(0f, ground.transform.position.y + ground.transform.localScale.y * 0.5f, 1e-4f, "윗면은 y=0");
            GameTuning.Current.showHitboxes = true;
            yield return null;
            Assert.IsTrue(ground.GetComponent<SpriteRenderer>().enabled, "판정 상자 보기에서는 겹쳐 보인다");
            GameTuning.Current.showHitboxes = false;
        }

        [UnityTest]
        public IEnumerator 구간을_바꾸면_바닥_그림도_바뀐다()
        {
            yield return TestScene.LoadPrototype();
            var deco = Object.FindAnyObjectByType<FloorDecorator>();
            for (int z = 0; z < 4; z++)
            {
                GameTuning.Current.backgroundZone = z;
                yield return null;
                yield return null;
                Assert.AreEqual(z, deco.Zone);
                Assert.Greater(deco.TileCount, 0, $"구간 {z} 타일");
            }
        }

        [UnityTest]
        public IEnumerator 사당_구간에서는_발판이_없다_다른_구간에선_다시_생긴다()
        {
            yield return TestScene.LoadPrototype();
            var platform = GameObject.Find("Platform_L");
            GameTuning.Current.backgroundZone = 0;
            yield return null;
            yield return null;
            Assert.IsFalse(platform.activeSelf, "사당 구간: 발판 없음 (판정도 함께)");
            GameTuning.Current.backgroundZone = 2;
            yield return null;
            yield return null;
            Assert.IsTrue(platform.activeSelf, "2구간: 발판 있음");
        }

        private static float CameraEdge(string field)
        {
            var rig = Object.FindAnyObjectByType<CameraRig>();
            return (float)typeof(CameraRig).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(rig);
        }

        [UnityTest]
        public IEnumerator 옆_벽은_화면_밖_카메라가_멈추는_끝에_붙어_있다_플레이_씬과_사당()
        {
            foreach (var load in new System.Func<IEnumerator>[] { TestScene.LoadPrototype, TestScene.LoadHub })
            {
                yield return load();
                yield return null;
                float left = CameraEdge("arenaMinX"), right = CameraEdge("arenaMaxX");
                float wallRInner = GameObject.Find("Wall_R").transform.position.x - 0.5f;
                float wallLInner = GameObject.Find("Wall_L").transform.position.x + 0.5f;
                Assert.GreaterOrEqual(wallRInner, right - 0.01f, "오른쪽 벽 안쪽 면은 카메라가 보는 끝 밖");
                Assert.LessOrEqual(wallLInner, left + 0.01f, "왼쪽 벽 안쪽 면은 카메라가 보는 끝 밖");
                Assert.LessOrEqual(wallRInner, right + 0.1f, "하지만 바로 붙어 있어 화면 끝에서 막힌다");
                var ground = GameObject.Find("Ground").transform;
                Assert.Greater(ground.position.x + ground.localScale.x * 0.5f, wallRInner, "바닥은 벽 아래까지 이어진다");
            }
        }

        [UnityTest]
        public IEnumerator 사당_바닥은_타일로_깔린다()
        {
            yield return TestScene.LoadHub();
            yield return null;
            var deco = Object.FindAnyObjectByType<FloorDecorator>();
            Assert.AreEqual(0, deco.Zone, "사당은 구간 0 고정");
            Assert.Greater(deco.TileCount, 0);
        }
    }
}
