using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 시작 화면 메뉴. 지금은 "게임 시작" · "종료하기" 두 개뿐이지만, 메뉴는 <see cref="Entries"/> 목록에서 버튼을 만들기 때문에
    /// 나중에 기능(이어하기·사당·설정 등)을 추가할 땐 <see cref="AddEntry"/> 한 줄이면 된다 (자리가 모자라면 목록이 아래로 늘어난다).
    /// 조작: ↑/↓ 선택, Enter/Space 확인, 마우스 올리기·클릭. UI는 코드로 만든다 (Hud와 같은 방식).
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        public const string GameSceneName = GameManager.HubSceneName; // 게임 시작 → 사당 (거기서 포탈로 플레이 씬)

        /// <summary>메뉴 항목 하나. 비활성(enabled=false)은 회색으로 보이고 선택되지 않는다 — 아직 없는 기능 자리 표시용.</summary>
        public sealed class Entry
        {
            public string Label;
            public Action OnSelected;
            public bool Enabled = true;
            public string Hint; // 오른쪽 아래 설명 줄 (선택 시 표시)

            internal Button Button;
            internal Text Text;
            internal Image Image;
        }

        public static StartMenu Instance { get; private set; }

        /// <summary>종료 동작. 테스트에서 갈아끼워 실제로 끄지 않고 호출 여부만 본다.</summary>
        public static Action QuitHandler = DefaultQuit;

        [SerializeField, Tooltip("메뉴 옆에서 서 있는 주인공 그림 (비우면 안 보임)")] private CharacterArt heroArt;

        private static readonly Color Purple = new(0.62f, 0.38f, 0.95f);      // 귀문 빛은 보라색
        private static readonly Color Idle = new(0.07f, 0.06f, 0.12f, 0.78f);
        private static readonly Color Selected = new(0.30f, 0.16f, 0.55f, 0.92f);
        private static readonly Color Disabled = new(0.07f, 0.06f, 0.12f, 0.4f);

        private const float ButtonWidth = 460f, ButtonHeight = 76f, ButtonGap = 18f;

        private readonly List<Entry> _entries = new();
        private RectTransform _root;
        private RectTransform _list;
        private Text _hint;
        private int _selected;
        private bool _starting;

        public IReadOnlyList<Entry> Entries => _entries;
        public int SelectedIndex => _selected;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            BuildCanvas();

            AddEntry("게임 시작", StartGame, hint: "귀문 안으로 들어간다");
            AddEntry("종료하기", () => QuitHandler(), hint: "게임을 끈다");
            // 이후 기능은 여기에 한 줄씩 추가: AddEntry("이어하기", ...), AddEntry("설정", ...), AddEntry("사당", ...)
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>메뉴 항목을 맨 아래에 추가한다.</summary>
        public Entry AddEntry(string label, Action onSelected, bool enabled = true, string hint = null)
        {
            var entry = new Entry { Label = label, OnSelected = onSelected, Enabled = enabled, Hint = hint };
            _entries.Add(entry);

            var button = UiKit.Button($"Entry_{_entries.Count}", _list, label, 36, Idle);
            var rt = (RectTransform)button.transform;
            int i = _entries.Count - 1;
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, -i * (ButtonHeight + ButtonGap)), new Vector2(ButtonWidth, ButtonHeight));
            entry.Button = button;
            entry.Image = button.GetComponent<Image>();
            entry.Text = button.GetComponentInChildren<Text>();
            entry.Text.alignment = TextAnchor.MiddleLeft;
            entry.Text.rectTransform.offsetMin = new Vector2(34f, 0f);

            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Select(_entries.IndexOf(entry)));
            trigger.triggers.Add(enter);
            button.onClick.AddListener(() => Activate(_entries.IndexOf(entry)));

            if (_entries.Count == 1) _selected = FirstEnabledFrom(0, 1);
            Refresh();
            return entry;
        }

        public void MoveSelection(int delta)
        {
            if (_entries.Count == 0) return;
            int next = FirstEnabledFrom(_selected + delta, delta);
            if (next != _selected) Select(next);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _entries.Count || !_entries[index].Enabled || index == _selected) return;
            _selected = index;
            Sfx.Play(SfxId.UiMove, 0.6f);
            Refresh();
        }

        /// <summary>항목을 실행한다 (비활성 항목이나 이미 시작 중이면 무시).</summary>
        public void Activate(int index)
        {
            if (_starting || index < 0 || index >= _entries.Count || !_entries[index].Enabled) return;
            Sfx.Play(SfxId.UiConfirm, 0.8f);
            _entries[index].OnSelected?.Invoke();
        }

        public void StartGame()
        {
            if (_starting) return;
            _starting = true;
            PlayerSession.Reset(); // 게임 시작 = 새 게임 (이전에 이어받던 체력·레벨 등을 비운다)
            SceneManager.LoadScene(GameSceneName);
        }

        private static void DefaultQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(Controls.Jump))
                Activate(_selected);
        }

        /// <summary>from부터 dir 방향으로 처음 만나는 활성 항목 (끝에서 멈춘다 — 돌아가지 않음).</summary>
        private int FirstEnabledFrom(int from, int dir)
        {
            dir = dir >= 0 ? 1 : -1;
            for (int i = from; i >= 0 && i < _entries.Count; i += dir)
                if (_entries[i].Enabled) return i;
            return Mathf.Clamp(_selected, 0, Mathf.Max(0, _entries.Count - 1));
        }

        private void Refresh()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                bool sel = i == _selected;
                e.Image.color = !e.Enabled ? Disabled : sel ? Selected : Idle;
                e.Text.color = !e.Enabled ? new Color(1f, 1f, 1f, 0.3f) : sel ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                e.Text.text = sel && e.Enabled ? $"◈  {e.Label}" : $"      {e.Label}";
            }
            if (_hint != null)
                _hint.text = _selected < _entries.Count ? _entries[_selected].Hint ?? "" : "";
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;
            EnsureEventSystem();

            // 왼쪽을 어둡게 — 글자가 배경 그림에 묻히지 않게. 가장자리가 뚝 끊기지 않게 3겹으로 점점 옅어진다.
            foreach (var (width, alpha) in new[] { (0.46f, 0.22f), (0.38f, 0.22f), (0.28f, 0.22f) })
            {
                var shade = UiKit.Image("LeftShade", _root, new Color(0.02f, 0.02f, 0.05f, alpha));
                shade.rectTransform.anchorMin = Vector2.zero;
                shade.rectTransform.anchorMax = new Vector2(width, 1f);
                shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            }
            // 아래쪽 설명 글자는 밝은 전경 위라 따로 한 번 더
            var bottom = UiKit.Image("BottomShade", _root, new Color(0.02f, 0.02f, 0.05f, 0.6f));
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(0.42f, 0f);
            bottom.rectTransform.offsetMin = Vector2.zero;
            bottom.rectTransform.offsetMax = new Vector2(0f, 170f);

            // 제목 (귀문 보라빛)
            var title = UiKit.Text("Title", _root, 150, TextAnchor.UpperLeft);
            title.text = "귀문";
            title.color = Color.Lerp(Purple, Color.white, 0.35f);
            title.fontStyle = FontStyle.Bold;
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(120f, -110f), new Vector2(700f, 190f));

            var subtitle = UiKit.Text("Subtitle", _root, 36, TextAnchor.UpperLeft);
            subtitle.text = "패링 로그라이크 (가제)";
            subtitle.color = new Color(1f, 1f, 1f, 0.8f);
            UiKit.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(128f, -300f), new Vector2(700f, 48f));

            var line = UiKit.Image("TitleLine", _root, Purple);
            UiKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(128f, -356f), new Vector2(420f, 4f));

            // 메뉴 목록
            _list = new GameObject("Entries", typeof(RectTransform)).GetComponent<RectTransform>();
            _list.SetParent(_root, false);
            UiKit.Place(_list, new Vector2(0f, 1f), new Vector2(128f, -420f), new Vector2(ButtonWidth, 600f));

            // 아래: 선택 항목 설명 + 조작 안내
            _hint = UiKit.Text("Hint", _root, 26, TextAnchor.LowerLeft);
            _hint.color = new Color(1f, 1f, 1f, 0.7f);
            UiKit.Place(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(128f, 90f), new Vector2(700f, 36f));
            var keys = UiKit.Text("Keys", _root, 22, TextAnchor.LowerLeft, outline: false);
            keys.color = new Color(1f, 1f, 1f, 0.45f);
            keys.text = "↑ / ↓ 선택   Enter 확인";
            UiKit.Place(keys.rectTransform, new Vector2(0f, 0f), new Vector2(128f, 48f), new Vector2(700f, 30f));

            // 주인공 (오른쪽, 대기 모션) — 카메라 자식이라 배경이 흘러도 제자리
            if (heroArt != null && Camera.main != null)
            {
                var hero = new GameObject("MenuHero");
                hero.transform.SetParent(Camera.main.transform, false);
                hero.transform.localPosition = new Vector3(4.2f, -3.5f, 5f);
                var sr = hero.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 12;
                sr.flipX = true; // 왼쪽(메뉴 쪽)을 본다
                hero.AddComponent<MenuHero>().Init(heroArt.idle);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }

    /// <summary>메뉴 화면 주인공: 대기 그림을 반복 재생한다.</summary>
    public class MenuHero : MonoBehaviour
    {
        private SpriteClip _clip;
        private SpriteRenderer _sr;
        private float _t;

        public void Init(SpriteClip clip)
        {
            _clip = clip;
            _sr = GetComponent<SpriteRenderer>();
            if (clip != null && !clip.IsEmpty) _sr.sprite = clip.frames[0];
        }

        private void Update()
        {
            if (_clip == null || _clip.IsEmpty) return;
            _t += Time.unscaledDeltaTime;
            _sr.sprite = _clip.frames[(int)(_t * _clip.fps) % _clip.frames.Length];
        }
    }

    /// <summary>메뉴 배경이 천천히 흐르도록 카메라를 옆으로 민다 (시차 스크롤이 카메라를 따라간다).</summary>
    public class MenuCameraPan : MonoBehaviour
    {
        [SerializeField] private float speed = 0.8f;

        private void Update() => transform.position += Vector3.right * (speed * Time.unscaledDeltaTime);
    }
}
