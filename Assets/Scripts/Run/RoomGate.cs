using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 방을 깬 뒤 열리는 문. 문마다 다음 방 종류가 색·글자로 표시돼 있고(스컬 방식), 문 앞에서 E를 누르면 그 방으로 간다.
    /// 마지막 방을 깬 뒤에는 사당으로 나가는 문이 열린다. 판정은 상호작용 트리거 하나뿐(그림은 판정이 아니다).
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class RoomGate : MonoBehaviour
    {
        public const float Width = 2.6f, Height = 3.2f;

        public bool IsExit { get; private set; }
        public RoomType Type { get; private set; }
        public string Label { get; private set; }

        private SpriteRenderer _halo;
        private float _time;

        /// <summary>문을 만든다. gateSprite는 아래 가운데 피벗(바닥에 선다), 없으면 색 네모로 대신 그린다.</summary>
        public static RoomGate Create(Vector3 groundPos, RoomType type, bool isExit, Sprite gateSprite)
        {
            var go = new GameObject(isExit ? "Gate_Exit" : $"Gate_{type}");
            go.transform.position = groundPos + new Vector3(0f, Height * 0.5f, 0f);
            go.transform.localScale = new Vector3(Width, Height, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var gate = go.AddComponent<RoomGate>();
            gate.IsExit = isExit;
            gate.Type = type;
            gate.Build(gateSprite);
            return gate;
        }

        private void Build(Sprite gateSprite)
        {
            Color color = IsExit ? new Color(0.65f, 0.55f, 1f) : RoomInfo.Color(Type);
            Label = IsExit ? "사당으로" : RoomInfo.Label(Type);
            string hint = IsExit ? "귀문을 닫았다 · 사당으로 돌아간다" : RoomInfo.Hint(Type);
            GetComponent<Interactable>().SetPrompt(IsExit ? "사당으로 돌아가기" : $"{Label} 방으로 들어가기  ({hint})");

            // 문 뒤 빛무리 (종류 색)
            _halo = MakeSprite("Halo", FxSprites.Glow, new Vector3(0f, 0.05f, 0f), new Vector2(4.6f / Width, 4.8f / Height), 8, new Color(color.r, color.g, color.b, 0.5f));
            // 문 그림 (바닥에 선다: 판정 네모 아래 끝 = 문 아래 끝)
            if (gateSprite != null)
            {
                var art = MakeSprite("Door", gateSprite, new Vector3(0f, -0.5f, 0f), Vector2.one * 1.25f * new Vector2(1f / Width, 1f / Height), 9, Color.Lerp(Color.white, color, 0.45f));
            }
            else
            {
                MakeSprite("Door", GameAssets.Square, Vector3.zero, Vector2.one, 9, new Color(color.r, color.g, color.b, 0.35f));
            }
            // 위에 종류 글자
            MakeLabel(Label, color, new Vector3(0f, 2.9f / Height, 0f), 0.085f, 24, true);
            MakeLabel(hint, new Color(1f, 1f, 1f, 0.85f), new Vector3(0f, 2.45f / Height, 0f), 0.04f, 24, false);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 localPos, Vector2 localScale, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private void MakeLabel(string text, Color color, Vector3 localPos, float worldSize, int order, bool bold)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(1f / Width, 1f / Height, 1f); // 부모의 늘림을 상쇄해 글자 비율 유지
            var tm = go.AddComponent<TextMesh>();
            tm.font = UiKit.Font;
            tm.GetComponent<MeshRenderer>().sharedMaterial = UiKit.Font.material;
            tm.GetComponent<MeshRenderer>().sortingOrder = order;
            tm.text = text;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = worldSize;
            tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        }

        private void Update()
        {
            if (_halo == null) return;
            _time += Time.deltaTime;
            var c = _halo.color;
            c.a = 0.4f + 0.15f * Mathf.Sin(_time * 2.2f);
            _halo.color = c;
        }

        private void OnEnable() => GetComponent<Interactable>().Interacted += Enter;
        private void OnDisable() => GetComponent<Interactable>().Interacted -= Enter;

        /// <summary>문으로 들어간다 (테스트도 이걸 부른다).</summary>
        public void Enter()
        {
            Hud.ScreenFlash(new Color(0.65f, 0.45f, 1f, 0.8f));
            if (IsExit) GameManager.LoadHub();
            else
            {
                RunState.Enter(Type);
                GameManager.LoadRun();
            }
        }
    }
}
