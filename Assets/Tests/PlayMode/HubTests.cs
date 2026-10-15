using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>사당(대기) 씬: 구성 · 포탈로 플레이 씬 · NPC 스킬 트리 창(틀).</summary>
    public class HubTests
    {
        private PlayerInteractor _interactor;
        private Rigidbody2D _playerBody;
        private ShrinePortal _portal;
        private ShrineNpc _npc;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadHub();
            _interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            _playerBody = _interactor.GetComponent<Rigidbody2D>();
            _portal = Object.FindAnyObjectByType<ShrinePortal>();
            _npc = Object.FindAnyObjectByType<ShrineNpc>();
            yield return new WaitForSeconds(0.3f); // 착지 대기
        }

        private void MoveTo(float x)
        {
            var pos = new Vector3(x, _interactor.transform.position.y, 0f);
            _interactor.transform.position = pos;
            _playerBody.position = pos;
            _playerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        [Test]
        public void 빌드_순서는_시작_사당_플레이()
        {
            // (테스트 러너가 임시 씬을 뒤에 하나 더 넣을 수 있어 개수는 확인하지 않는다)
            Assert.IsTrue(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(0).EndsWith("Start.unity"));
            Assert.IsTrue(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(1).EndsWith("Hub.unity"));
            Assert.IsTrue(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(2).EndsWith("Prototype.unity"));
        }

        [Test]
        public void 사당은_포탈이_오른쪽_끝_NPC가_있고_적은_없다()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_npc);
            Assert.AreEqual(0, Object.FindObjectsByType<EnemyController>().Length, "사당에는 적이 없다");
            Assert.Greater(_portal.transform.position.x, _npc.transform.position.x, "포탈은 NPC보다 오른쪽");
            Assert.Greater(_portal.transform.position.x, 10f, "포탈은 오른쪽 끝");
            Assert.Less(_interactor.transform.position.x, _npc.transform.position.x, "플레이어는 왼쪽에서 시작해 NPC를 지나 포탈로");
        }

        [UnityTest]
        public IEnumerator 포탈과_겹치지_않으면_상호작용이_안_된다()
        {
            MoveTo(6f); // 포탈·NPC 어디와도 멀다
            yield return null;
            Assert.IsNull(_interactor.Current);
            Assert.IsFalse(_interactor.TryInteract());
        }

        [UnityTest]
        public IEnumerator 포탈_앞에서_상호작용하면_플레이_씬으로_넘어간다()
        {
            MoveTo(_portal.transform.position.x + 0.5f);
            yield return null;
            Assert.AreEqual(_portal.GetComponent<Interactable>(), _interactor.Current);
            Assert.IsTrue(_interactor.TryInteract());
            yield return null;
            yield return null;
            Assert.AreEqual(GameManager.RunSceneName, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator NPC에게_상호작용하면_스킬_트리_창이_열리고_닫으면_게임이_다시_흐른다()
        {
            MoveTo(_npc.transform.position.x);
            yield return null;
            Assert.AreEqual(_npc.GetComponent<Interactable>(), _interactor.Current);
            Assert.IsTrue(_interactor.TryInteract());
            Assert.IsTrue(SkillTreePanel.IsOpen);
            Assert.IsTrue(GameManager.InputBlocked, "창이 열려 있으면 플레이 입력은 막힌다");
            Assert.AreEqual(0f, Time.timeScale);

            SkillTreePanel.Close();
            Assert.IsFalse(SkillTreePanel.IsOpen);
            Assert.IsFalse(GameManager.InputBlocked);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 창이_열려_있으면_포탈이_작동하지_않는다()
        {
            MoveTo(_npc.transform.position.x);
            yield return null;
            _interactor.TryInteract();
            MoveTo(_portal.transform.position.x + 0.5f);
            yield return null; // 열린 동안은 Update에서 대상을 찾지 않는다
            Assert.IsNull(_interactor.Current);
            SkillTreePanel.Close();
        }
    }
}
