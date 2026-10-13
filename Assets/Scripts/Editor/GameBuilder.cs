using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ParryRL.EditorTools
{
    /// <summary>
    /// 실행 파일(독립 실행형 게임) 빌드. 시작 화면(Start) → 게임(Prototype) 순서로 빌드 설정에 이미 들어 있다.
    /// 에디터: Tools → ParryRL → 게임 빌드 (macOS / Windows)
    /// 배치모드: -executeMethod ParryRL.EditorTools.GameBuilder.BuildMac  (또는 BuildWindows)
    /// 런처 스크립트(Tools/launcher/)가 이 메서드를 불러 빌드하고 실행한다.
    /// </summary>
    public static class GameBuilder
    {
        public const string GameName = "귀문";
        public const string MacPath = "Builds/Mac/" + GameName + ".app";
        public const string WindowsPath = "Builds/Windows/" + GameName + ".exe";

        [MenuItem("Tools/ParryRL/게임 빌드 (macOS)")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, MacPath);

        [MenuItem("Tools/ParryRL/게임 빌드 (Windows)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsPath);

        private static void Build(BuildTarget target, string relativePath)
        {
            // 씬이 아직 없으면(처음 받은 직후 등) 만들어 둔다. 빌드 설정(Start 0 → Prototype 1)도 이때 맞춰진다.
            if (!File.Exists(PrototypeSceneBuilder.StartScenePath) || !File.Exists(PrototypeSceneBuilder.ScenePath))
                PrototypeSceneBuilder.Build();

            PlayerSettings.productName = GameName;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; // 창 모드로 시작 (Alt+Enter 등 OS 기능으로 전체화면 가능)
            PlayerSettings.resizableWindow = true;

            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.StartScenePath, PrototypeSceneBuilder.ScenePath },
                locationPathName = fullPath,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ParryRL] 빌드 성공: {fullPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}초)");
                return;
            }
            Debug.LogError($"[ParryRL] 빌드 실패: {summary.result} (오류 {summary.totalErrors}개)");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
