using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 사당 NPC(만신)에게 말을 걸면 열리는 스킬 트리 창. <b>지금은 창(틀)만 있다</b> — 스킬 노드·찍기는 아직 구현하지 않았다.
    /// 나중에 노드를 넣을 자리는 <see cref="ContentRoot"/>. 열려 있는 동안 게임은 멈춘다(GameManager.SetMenuOpen).
    /// 닫기: E · ESC · 닫기 버튼.
    /// </summary>
    public class SkillTreePanel : MonoBehaviour
    {
        private static SkillTreePanel _instance;

        public static bool IsOpen => _instance != null && _instance._canvas.enabled;

        /// <summary>스킬 노드를 올릴 영역 (지금은 안내 문구만 있다).</summary>
        public RectTransform ContentRoot { get; private set; }

        private Canvas _canvas;
        private Text _title;
        private Text _speech;
        private bool _openedThisFrame;

        public static SkillTreePanel Open(string npcName)
        {
            if (_instance == null)
            {
                var go = new GameObject("SkillTreePanel");
                _instance = go.AddComponent<SkillTreePanel>();
            }
            _instance.Show(npcName);
            return _instance;
        }

        public static void Close()
        {
            if (_instance != null) _instance.Hide();
        }

        private void Awake()
        {
            BuildCanvas();
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Show(string npcName)
        {
            _title.text = $"스킬 트리  <size=28><color=#CDB8FF>— {npcName}</color></size>";
            _speech.text = "\"도력을 모아 오너라. 길을 가르쳐 주마.\"";
            _canvas.enabled = true;
            _openedThisFrame = true;
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(true);
        }

        private void Hide()
        {
            if (!_canvas.enabled) return;
            _canvas.enabled = false;
            if (GameManager.Instance != null) GameManager.Instance.SetMenuOpen(false);
        }

        private void Update()
        {
            if (!_canvas.enabled) return;
            if (_openedThisFrame)
            {
                _openedThisFrame = false; // 열자마자 같은 입력으로 닫히지 않게
                return;
            }
            if (Input.GetKeyDown(Controls.Interact) || Input.GetKeyDown(Controls.Pause)) Hide();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            // 화면을 어둡게
            var dim = UiKit.Image("Dim", root, new Color(0.02f, 0.01f, 0.06f, 0.7f));
            UiKit.Stretch(dim.rectTransform);

            // 중앙 창 (테두리는 창보다 먼저 그려 뒤에 깔리게)
            var frame = UiKit.Image("Frame", root, new Color(0.62f, 0.5f, 0.95f, 0.7f)).rectTransform;
            UiKit.Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1188f, 728f));
            frame.pivot = new Vector2(0.5f, 0.5f);
            var panel = UiKit.Image("Panel", root, new Color(0.09f, 0.07f, 0.16f, 0.97f)).rectTransform;
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 720f));
            panel.pivot = new Vector2(0.5f, 0.5f);

            _title = UiKit.Text("Title", panel, 46, TextAnchor.MiddleLeft);
            _title.fontStyle = FontStyle.Bold;
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(1000f, 64f));

            _speech = UiKit.Text("Speech", panel, 28, TextAnchor.UpperLeft);
            _speech.color = new Color(0.85f, 0.82f, 0.95f);
            UiKit.Place(_speech.rectTransform, new Vector2(0f, 1f), new Vector2(44f, -104f), new Vector2(1090f, 44f));

            // 노드가 들어올 영역
            ContentRoot = UiKit.Image("Content", panel, new Color(1f, 1f, 1f, 0.05f)).rectTransform;
            UiKit.Place(ContentRoot, new Vector2(0f, 1f), new Vector2(40f, -164f), new Vector2(1100f, 440f));
            var note = UiKit.Text("Note", ContentRoot, 32, TextAnchor.MiddleCenter);
            note.color = new Color(1f, 1f, 1f, 0.55f);
            note.text = "스킬 트리는 아직 준비 중입니다\n<size=24>패링으로 모은 도력으로 스킬을 익히는 곳이 됩니다</size>";
            UiKit.Stretch(note.rectTransform);

            var close = UiKit.Button("Close", panel, "닫기  (E / ESC)", 26, new Color(0.34f, 0.26f, 0.55f));
            UiKit.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-40f, 28f), new Vector2(240f, 56f));
            close.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            close.onClick.AddListener(Hide);
        }
    }
}
