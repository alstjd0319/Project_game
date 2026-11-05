using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>얇은 발판 착지: 방향키를 누른 채 올라가도 착지하면 그대로 서 있어야 한다 (착지 모션만 나오고 떨어지던 버그).</summary>
    public class PlatformLandingTests
    {
        private const float JumpVelocity = 7f;
        private PlayerMotor _motor;
        private Rigidbody2D _rb;

        private float Feet => _motor.transform.position.y - _motor.transform.localScale.y * 0.5f;

        [SetUp]
        public void SetUp() => GameTuning.UseDefaultsForTests();

        [TearDown]
        public void TearDown() => Controls.TestHorizontal = 0f;

        private IEnumerator Load()
        {
            yield return TestScene.LoadPrototype();
            GameTuning.Current.godMode = true;
            GameTuning.Current.hideMelee = true;
            GameTuning.Current.hideRanged = true;
            yield return null;
            _motor = Object.FindAnyObjectByType<PlayerMotor>();
            _rb = _motor.GetComponent<Rigidbody2D>();
        }

        private void Put(float x, float feet)
        {
            var pos = new Vector3(x, feet + _motor.transform.localScale.y * 0.5f + 0.02f, 0f);
            _motor.transform.position = pos;
            _rb.position = pos;
            _rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// 발판 옆·아래 여러 자리에서 방향키를 누른 채 뛰어 본다. "착지했다"(IsGrounded)고 나온 뒤 발이 발판 윗면 아래로 빠지면 실패.
        /// </summary>
        [UnityTest]
        public IEnumerator 방향키를_누른_채_뛰어올라도_착지했으면_떨어지지_않는다()
        {
            yield return Load();
            var platform = RoomLayouts.All[RoomLayouts.Basic].Platforms[0]; // 왼쪽 1층 발판
            float top = RoomLayouts.Top(platform), left = platform.X - platform.Width * 0.5f;
            var failed = new List<string>();
            int landings = 0;
            foreach (float dir in new[] { 1f, -1f })
            for (float start = -3.2f; start <= 0.2f; start += 0.2f)
            {
                float edge = dir > 0f ? left : left + platform.Width;
                Put(edge + start * dir, 0f);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Controls.TestHorizontal = dir;
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpVelocity);
                bool landed = false, sank = false;
                for (float t = 0f; t < 1.1f; t += Time.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                    bool over = Mathf.Abs(_motor.transform.position.x - platform.X) < platform.Width * 0.5f - 0.5f;
                    if (t > 0.08f && _motor.IsGrounded && Feet > 0.3f && over) landed = true;
                    if (landed && over && Feet < top - 0.1f) sank = true;
                }
                Controls.TestHorizontal = 0f;
                if (sank) failed.Add($"dir {dir} start {start:0.0}");
                if (landed) landings++;
            }
            Assert.Greater(landings, 10, "점프 한 번으로 1층에 실제로 올라선다");
            Assert.IsEmpty(failed, "착지한 뒤 발판을 뚫고 떨어짐: " + string.Join(", ", failed));
        }

        [UnityTest]
        public IEnumerator 발판_위에서_방향키로_걸어도_떨어지지_않는다()
        {
            yield return Load();
            var platform = RoomLayouts.All[RoomLayouts.Basic].Platforms[0];
            float top = RoomLayouts.Top(platform);
            Put(platform.X - 1.2f, top);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(_motor.IsGrounded);
            foreach (float dir in new[] { 1f, -1f, 1f })
            {
                Controls.TestHorizontal = dir;
                for (float t = 0f; t < 0.2f; t += Time.deltaTime)
                {
                    Assert.Greater(Feet, top - 0.08f, "걷는 동안 발판 위");
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator 아래로_내려가기는_올려_세우기에_걸리지_않는다()
        {
            yield return Load();
            var platform = RoomLayouts.All[RoomLayouts.Basic].Platforms[0];
            float top = RoomLayouts.Top(platform);
            Put(platform.X, top);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(_motor.TryDropThrough());
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Feet, top - 0.5f, "발판 아래로 내려갔다");
        }
    }
}
