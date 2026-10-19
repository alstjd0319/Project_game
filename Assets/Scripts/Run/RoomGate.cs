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

        /// <summary>문을 만든다. 포탈 틀 그림(portalArt)이 있으면 방 종류별 틀, 없으면 gateSprite(아래 가운데 피벗), 그것도 없으면 색 네모.</summary>
        public static RoomGate Create(Vector3 groundPos, RoomType type, bool isExit, Sprite gateSprite, PortalArt portalArt = null)
        {
            var go = new GameObject(isExit ? "Gate_Exit" : $"Gate_{type}");
            go.transform.position = groundPos + new Vector3(0f, Height * 0.5f, 0f);
            go.transform.localScale = new Vector3(Width, Height, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var gate = go.AddComponent<RoomGate>();
            gate.IsExit = isExit;
            gate.Type = type;
            gate.Build(gateSprite, portalArt);
            return gate;
        }

        /// <summary>이 문에 쓴 포탈 틀 번호 (<see cref="PortalArt"/>, 틀이 없으면 −1).</summary>
        public int FrameIndex { get; private set; } = -1;
        public SpriteRenderer FrameRenderer { get; private set; }

        private void Build(Sprite gateSprite, PortalArt portalArt)
        {
            Color color = IsExit ? new Color(0.65f, 0.55f, 1f) : RoomInfo.Color(Type);
            Label = IsExit ? "사당으로" : RoomInfo.Label(Type);
            string hint = IsExit ? "귀문을 닫았다 · 사당으로 돌아간다" : RoomInfo.Hint(Type);
            GetComponent<Interactable>().SetPrompt(IsExit ? "사당으로 돌아가기" : $"{Label} 방으로 들어가기  ({hint})");

            // 문 안쪽 일렁이는 소용돌이·빛무리 (사당 포탈과 같은 연출, 방 종류 색을 섞는다 — 가까이 가면 밝아진다)
            var effects = gameObject.AddComponent<DoorEffects>();
            effects.Tint = color;
            effects.SwirlTintAmount = 0.5f;
            float labelY = 4.05f; // 바닥에서 안내 글자까지 (틀 그림이 있으면 틀 꼭대기 위)
            int index = PortalArt.IndexFor(Type, IsExit);
            var frame = portalArt != null ? portalArt.Frame(index) : null;
            if (frame != null)
            {
                // 방 종류별 포탈 틀 (1016 에셋): 틀 그대로의 색, 소용돌이는 틀 안쪽 뚫린 부분에 맞춘다
                FrameIndex = index;
                FrameRenderer = MakeSprite("Frame", frame, new Vector3(0f, -0.5f, 0f), new Vector2(1f / Width, 1f / Height), 9, Color.white);
                if (portalArt.TryHole(index, out var holeCenter, out var holeSize))
                {
                    effects.OpeningCenterHeight = holeCenter.y;
                    effects.OpeningSize = holeSize * 0.92f; // 틀 안쪽 가장자리에 살짝 덮이게
                    effects.HaloScale = 0.9f;
                }
                effects.UseGatePortal = true; // 에셋의 귀문 소용돌이를 틀 구멍 모양대로 가려 그린다
                effects.HoleMask = portalArt.HoleMask(index);
                effects.SwirlTintAmount = 1f;  // 회색 소용돌이에 방 종류 색을 그대로 곱한다
                labelY = frame.bounds.size.y + 0.4f;
            }
            // 문 그림 (바닥에 선다: 판정 네모 아래 끝 = 문 아래 끝)
            else if (gateSprite != null)
            {
                var art = MakeSprite("Door", gateSprite, new Vector3(0f, -0.5f, 0f), Vector2.one * 1.25f * new Vector2(1f / Width, 1f / Height), 9, Color.Lerp(Color.white, color, 0.45f));
            }
            else
            {
                MakeSprite("Door", GameAssets.Square, Vector3.zero, Vector2.one, 9, new Color(color.r, color.g, color.b, 0.35f));
            }
            // 위에 종류 글자
            // 위에 종류 글자 (틀 꼭대기 위). 위치는 문 가운데(판정 상자 가운데) 기준이라 바닥 기준 높이에서 Height/2를 뺀다
            MakeLabel(Label, color, new Vector3(0f, (labelY + 0.45f - Height * 0.5f) / Height, 0f), 0.085f, 24, true);
            MakeLabel(hint, new Color(1f, 1f, 1f, 0.85f), new Vector3(0f, (labelY - Height * 0.5f) / Height, 0f), 0.04f, 24, false);
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
            tm.fontStyle = bold ? UiKit.BoldStyle : FontStyle.Normal;
        }

        private void OnEnable() => GetComponent<Interactable>().Interacted += Enter;
        private void OnDisable() => GetComponent<Interactable>().Interacted -= Enter;

        public bool IsEntering { get; private set; }

        /// <summary>문으로 들어간다 (테스트도 이걸 부른다). 몸이 빛으로 흩어지는 연출 뒤 다음 방 (연출을 끄면 바로).</summary>
        public void Enter()
        {
            if (IsEntering) return;
            IsEntering = true;
            StartCoroutine(PortalTransition.Enter(() =>
            {
                if (IsExit) GameManager.LoadHub();
                else
                {
                    RunState.Enter(Type);
                    GameManager.LoadRun();
                }
            }));
        }
    }
}
