using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParryRL
{
    /// <summary>일시정지 / 메뉴(증강 선택 등) / 게임오버 / 재시작.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

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
            if (Input.GetKeyDown(Controls.Pause) && !IsGameOver && !IsMenuOpen) SetPaused(!IsPaused);
            if (Input.GetKeyDown(Controls.Restart) && (IsPaused || IsGameOver)) Restart();
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

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
