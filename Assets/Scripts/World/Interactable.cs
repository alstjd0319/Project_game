using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 상호작용 대상(포탈·NPC 등). 트리거 콜라이더(기본 크기 1×1 × localScale)에 플레이어 몸이 겹친 채 E를 누르면 <see cref="Interacted"/>가 불린다.
    /// 겹침 판정과 입력은 <see cref="PlayerInteractor"/>가 맡는다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Interactable : MonoBehaviour
    {
        [SerializeField, Tooltip("가까이 가면 화면에 뜨는 안내 (키 이름은 자동으로 앞에 붙는다)")] private string prompt = "상호작용";

        public string Prompt => prompt;
        public event Action Interacted;

        public void SetPrompt(string text) => prompt = text;

        /// <summary>E를 눌렀을 때 (테스트는 PlayerInteractor.TryInteract를 부른다).</summary>
        public void Interact() => Interacted?.Invoke();
    }
}
