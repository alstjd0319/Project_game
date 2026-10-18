using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParryRL
{
    /// <summary>일시정지 / 메뉴(증강 선택 등) / 게임오버 / 재시작.</summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>시작 화면 씬 이름 (빌드 0번).</summary>
        public const string StartSceneName = "Start";
        /// <summary>사당(대기 씬) 이름 — 시작 화면과 플레이 씬 사이 (빌드 1번).</summary>
        public const string HubSceneName = "Hub";
        /// <summary>플레이(귀문 안) 씬 이름 (빌드 2번).</summary>
        public const string RunSceneName = "Prototype";

        public static GameManager Instance { get; private set; }

        /// <summary>지금 사당 씬인가 (사당에서는 "사당으로" 버튼·키가 없다).</summary>
        public static bool InHub => SceneManager.GetActiveScene().name == HubSceneName;

        public bool IsPaused { get; private set; }
        public bool IsGameOver { get; private set; }
        /// <summary>증강 선택처럼 게임을 멈추고 UI만 조작하는 상태.</summary>
        public bool IsMenuOpen { get; private set; }

        public event Action<bool> PauseChanged;
        public event Action GameOver;

        /// <summary>일시정지·메뉴·게임오버 중에는 플레이 입력을 막는다.</summary>
        public static bool InputBlocked =>
            Instance != null && (Instance.IsPaused || Instance.IsGameOver || Instance.IsMenuOpen);

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            // 설정 창이 열려 있거나 방금 ESC로 닫힌 프레임이면 일시정지는 그대로 둔다 (설정창 → 일시정지 메뉴로만 돌아감)
            bool settingsBusy = TuningPanel.Instance != null && (TuningPanel.Instance.IsVisible || TuningPanel.ClosedFrame == Time.frameCount);
            if (Input.GetKeyDown(Controls.Pause) && !IsGameOver && !IsMenuOpen && !settingsBusy) SetPaused(!IsPaused);
            if (Input.GetKeyDown(Controls.Restart) && (IsPaused || IsGameOver)) Restart();
            if (Input.GetKeyDown(Controls.MainMenu) && (IsPaused || IsGameOver)) GoToStartMenu();
            if (Input.GetKeyDown(Controls.Hub) && (IsPaused || IsGameOver) && !InHub) LoadHub();
            if (Input.GetKeyDown(Controls.DebugBossRoom)) GoToBossRoom();
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            ApplyTimeScale();
            PauseChanged?.Invoke(paused);
        }

        public void SetMenuOpen(bool open)
        {
            if (IsMenuOpen == open) return;
            IsMenuOpen = open;
            ApplyTimeScale();
        }

        public void TriggerGameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            ApplyTimeScale();
            GameOver?.Invoke();
        }

        private void ApplyTimeScale()
        {
            // 멈춤 상태가 바뀌면 진행 중인 히트스탑은 버린다 (복귀 시 timeScale을 덮어쓰지 않도록)
            if (HitFeel.Instance != null) HitFeel.Instance.Cancel();
            Time.timeScale = IsPaused || IsMenuOpen || IsGameOver ? 0f : 1f;
        }

        /// <summary>시작 화면으로 돌아간다 (진행 중인 판은 버려진다).</summary>
        public void GoToStartMenu()
        {
            PlayerSession.Reset(); // 시작 화면으로 나가면 이어지는 데이터는 비운다 (다음 게임 시작은 새 게임)
            Time.timeScale = 1f;
            SceneManager.LoadScene(StartSceneName);
        }

        /// <summary>사당에서 귀문 안(플레이 씬)으로 들어간다.</summary>
        public static void LoadRun()
        {
            PlayerSession.Capture(); // 체력·레벨·도력·무기를 가져간다
            Time.timeScale = 1f;
            SceneManager.LoadScene(RunSceneName);
        }

        /// <summary>(임시 테스트용) 보스방으로 바로 간다 — 새 판의 마지막 방(불가살이). 체력·레벨·도력은 지금 것 그대로, 체력이 0이면 가득.</summary>
        public static void GoToBossRoom()
        {
            RunState.BeginAtBoss();
            LoadRun();
        }

        /// <summary>사당(대기 씬)으로 간다.</summary>
        public static void LoadHub()
        {
            RunState.End(); // 판이 끝나고 사당으로 (이 판의 방 진행은 버린다)
            PlayerSession.Capture();
            Time.timeScale = 1f;
            SceneManager.LoadScene(HubSceneName);
        }

        public void Restart()
        {
            bool wasRun = RunState.Active;
            PlayerSession.Reset(); // 다시 시작 = 이 씬을 처음 상태로
            if (wasRun) RunState.Begin(); // 판 도중이면 첫 방부터 새로 시작
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
