using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 플레이어에 붙어 몸과 겹친 <see cref="Interactable"/>을 찾고, E(<see cref="Controls.Interact"/>)로 상호작용한다.
    /// 겹침은 실제 콜라이더 오버랩이다 (거리 계산 금지). 가장 가까운 대상 하나만 안내한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerInteractor : MonoBehaviour
    {
        private static readonly List<Interactable> Found = new();

        private BoxCollider2D _body;
        private PlayerParty _party;

        /// <summary>지금 겹쳐 있는 대상 (없으면 null).</summary>
        public Interactable Current { get; private set; }

        private void Awake()
        {
            _body = GetComponent<BoxCollider2D>();
            _party = GetComponent<PlayerParty>();
        }

        private void Update()
        {
            bool blocked = GameManager.InputBlocked || (_party != null && _party.IsDead);
            Current = blocked ? null : Find();
            Hud.SetPrompt(Current != null ? $"[{Controls.Interact}]  {Current.Prompt}" : null);
            if (Current != null && Input.GetKeyDown(Controls.Interact)) TryInteract();
        }

        private void OnDisable() => Hud.SetPrompt(null);

        /// <summary>겹친 대상이 있으면 상호작용한다.</summary>
        public bool TryInteract()
        {
            var target = Current != null ? Current : Find();
            if (target == null) return false;
            Sfx.Play(SfxId.UiConfirm, 0.8f);
            target.Interact();
            return true;
        }

        private Interactable Find()
        {
            Physics2D.SyncTransforms();
            Found.Clear();
            Interactable best = null;
            float bestDist = float.MaxValue;
            var bounds = _body.bounds;
            foreach (var col in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
            {
                var target = col.GetComponent<Interactable>();
                if (target == null || !target.isActiveAndEnabled) continue;
                float d = Mathf.Abs(col.bounds.center.x - bounds.center.x);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = target;
                }
            }
            return best;
        }
    }
}
