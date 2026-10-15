using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ParryRL.Tests
{
    /// <summary>
    /// 씬 파일이 실행 파일(빌드)에서도 읽히는지 미리 확인한다.
    /// MonoBehaviour 클래스가 자기 이름과 다른 파일에 들어 있으면 씬에 스크립트가 GUID가 아닌 임시 번호로 저장되고,
    /// 에디터에서는 멀쩡해도 빌드된 게임은 그 씬을 못 읽고 죽는다 ("level0 is corrupted").
    /// 이 테스트가 깨지면: 그 클래스를 <b>클래스 이름과 같은 파일</b>로 옮긴 뒤 씬을 다시 만든다.
    /// </summary>
    public class SceneIntegrityTests
    {
        private static readonly string[] Scenes = { "Start", "Hub", "Prototype" };

        [Test]
        public void 씬의_모든_스크립트는_GUID로_연결돼_있다()
        {
            // m_Script: {fileID: 11500000, guid: ..., type: 3} 가 정상. fileID만 있고 guid가 없으면 파일 이름 불일치.
            var bad = new Regex(@"m_Script: \{fileID: (?!11500000,)(\d+)\}");
            foreach (string scene in Scenes)
            {
                string path = Path.Combine(Application.dataPath, "Scenes", scene + ".unity");
                Assert.IsTrue(File.Exists(path), scene + " 씬 파일");
                var match = bad.Match(File.ReadAllText(path));
                Assert.IsFalse(match.Success,
                    $"{scene}.unity에 GUID 없는 스크립트 참조({match.Value}) — MonoBehaviour를 클래스 이름과 같은 파일로 옮기고 씬을 다시 만들 것");
            }
        }

        [Test]
        public void 씬에_빠진_스크립트가_없다()
        {
            foreach (string scene in Scenes)
            {
                string text = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes", scene + ".unity"));
                Assert.IsFalse(text.Contains("m_Script: {fileID: 0}"), scene + ".unity에 빠진(Missing) 스크립트");
            }
        }
    }
}
