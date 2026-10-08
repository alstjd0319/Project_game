using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 레벨업 → 증강 3택 흐름. 레벨업마다 선택 대기가 1개 쌓이고, 선택 창이 열리면 게임이 멈춘다.
    /// 여러 레벨이 한 번에 오르면 선택 창이 연달아 열린다.
    /// </summary>
    [RequireComponent(typeof(PlayerLevel), typeof(RunModifiers))]
    public class AugmentManager : MonoBehaviour
    {
        [SerializeField] private int choiceCount = 3;
        [SerializeField, Tooltip("레벨업 연출 후 선택 창이 뜨기까지 (실시간 초)")] private float openDelay = 0.45f;

        public bool IsChoosing { get; private set; }
        public int PendingChoices { get; private set; }
        public IReadOnlyList<AugmentDefinition> Options => _options;
        public IReadOnlyList<AugmentDefinition> Acquired => _acquired;

        public event Action<IReadOnlyList<AugmentDefinition>> ChoiceOpened;
        public event Action<AugmentDefinition> AugmentAcquired;
        public event Action ChoiceClosed;
        /// <summary>획득한 증강을 전부 제거함 (튜닝 패널 연습용).</summary>
        public event Action AugmentsCleared;

        private readonly List<AugmentDefinition> _options = new();
        private readonly List<AugmentDefinition> _acquired = new();
        private PlayerLevel _level;
        private RunModifiers _mods;
        private Coroutine _openRoutine;

        private void Awake()
        {
            _level = GetComponent<PlayerLevel>();
            _mods = GetComponent<RunModifiers>();
        }

        private void OnEnable() => _level.LeveledUp += OnLeveledUp;
        private void OnDisable() => _level.LeveledUp -= OnLeveledUp;

        public int StacksOf(AugmentDefinition def)
        {
            int n = 0;
            foreach (var a in _acquired) if (a == def) n++;
            return n;
        }

        private void OnLeveledUp(int level)
        {
            PendingChoices++;

            Sfx.Play(SfxId.LevelUp);
            Hud.Popup($"LEVEL UP!  Lv.{level}", new Color(0.45f, 0.9f, 1f), true);
            Fx.Ring(transform.position, new Color(0.45f, 0.9f, 1f), 0.8f, 5f, 0.2f, 0.45f);
            Fx.Sparks(transform.position, new Color(0.45f, 0.9f, 1f), 16, 8f, 0.14f, 0.5f);

            if (!IsChoosing && _openRoutine == null) _openRoutine = StartCoroutine(OpenAfterDelay());
        }

        private IEnumerator OpenAfterDelay()
        {
            // 레벨업 연출이 보이도록 잠깐 기다렸다가 연다. 일시정지 중이면 풀릴 때까지 대기.
            yield return new WaitForSecondsRealtime(openDelay);
            while (GameManager.Instance != null && GameManager.Instance.IsPaused) yield return null;
            _openRoutine = null;
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
            OpenNext();
        }

        private void OpenNext()
        {
            RollOptions();
            if (_options.Count == 0)
            {
                // 더 줄 증강이 없음
                PendingChoices = 0;
                SetMenu(false);
                return;
            }

            IsChoosing = true;
            SetMenu(true);
            ChoiceOpened?.Invoke(_options);
        }

        /// <summary>후보 중 하나를 고른다. 대기 중인 선택이 남아 있으면 바로 다음 창을 연다.</summary>
        public bool Choose(int index)
        {
            if (!IsChoosing || index < 0 || index >= _options.Count) return false;

            var def = _options[index];
            PendingChoices = Mathf.Max(0, PendingChoices - 1);
            IsChoosing = false;
            _options.Clear();

            Grant(def);
            ChoiceClosed?.Invoke();

            if (PendingChoices > 0) OpenNext();
            else SetMenu(false);
            return true;
        }

        /// <summary>증강을 바로 얻는다 (선택 창을 거치지 않음 — 테스트·디버그용, 선택 확정도 이걸 쓴다).</summary>
        public void Grant(AugmentDefinition def)
        {
            _acquired.Add(def);
            if (def.Effect != null) _mods.AddStack(def.Effect);
            AugmentAcquired?.Invoke(def);
        }

        /// <summary>최대 중첩 전이면 바로 얻는다 (튜닝 패널에서 골라 받기).</summary>
        public bool TryGrant(AugmentDefinition def)
        {
            if (StacksOf(def) >= def.MaxStacks) return false;
            Grant(def);
            return true;
        }

        /// <summary>획득한 증강을 전부 없앤다 (튜닝 패널 연습용 — 다른 조합을 바로 시험하기 위해).</summary>
        public void ClearAll()
        {
            _acquired.Clear();
            _mods.RemoveAll();
            AugmentsCleared?.Invoke();
        }

        private void RollOptions()
        {
            _options.Clear();
            var pool = new List<AugmentDefinition>();
            foreach (var def in AugmentCatalog.All)
                if (StacksOf(def) < def.MaxStacks) pool.Add(def);

            for (int i = 0; i < choiceCount && pool.Count > 0; i++)
            {
                int pick = UnityEngine.Random.Range(0, pool.Count);
                _options.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        private static void SetMenu(bool open)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(open);
        }
    }
}
