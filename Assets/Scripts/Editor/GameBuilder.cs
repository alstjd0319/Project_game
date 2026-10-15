using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ParryRL.EditorTools
{
    /// <summary>
    /// 실행 파일(독립 실행형 게임) 빌드. 시작 화면(Start) → 사당(Hub) → 게임(Prototype) 순서로 빌드한다.
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

        /// <summary>
        /// 빌드할 씬 = 빌드 설정(씬 빌더가 Start → Hub → Prototype 순서로 맞춘다)의 켜진 씬 전부.
        /// 목록을 여기 따로 적어 두면 씬이 늘어날 때 빠뜨려 "게임 시작"이 안 넘어가므로(사당 추가 때 실제로 그랬다) 한 곳(빌드 설정)만 쓴다.
        /// </summary>
        private static string[] BuildScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && File.Exists(scene.path)) list.Add(scene.path);
            return list.ToArray();
        }

        private static void Build(BuildTarget target, string relativePath)
        {
            // 씬이 아직 없으면(처음 받은 직후 등) 만들어 둔다. 빌드 설정(Start 0 → Hub 1 → Prototype 2)도 이때 맞춰진다.
            if (!File.Exists(PrototypeSceneBuilder.StartScenePath) || !File.Exists(PrototypeSceneBuilder.HubScenePath) || !File.Exists(PrototypeSceneBuilder.ScenePath))
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
                scenes = BuildScenes(),
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
