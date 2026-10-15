using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 사당의 스승 NPC (만신). 상호작용하면 스킬 트리 창을 연다 — 스킬 트리 내용은 아직 없고 창(틀)만 있다.
    /// 평소엔 대기 모션, 창이 열려 있는 동안엔 대화 모션. 항상 플레이어 쪽을 바라본다.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class ShrineNpc : MonoBehaviour
    {
        [SerializeField] private NpcArt art;
        [SerializeField] private ClipPlayer artPlayer;
        [SerializeField, Tooltip("NPC 이름 (대화창 머리말)")] private string displayName = "만신";

        private Interactable _interactable;
        private Transform _player;

        public string DisplayName => displayName;

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.SetPrompt($"{displayName}에게 말 걸기");
        }

        private void OnEnable() => _interactable.Interacted += Talk;
        private void OnDisable() => _interactable.Interacted -= Talk;

        private void Start()
        {
            var party = FindAnyObjectByType<PlayerParty>();
            if (party != null) _player = party.transform;
            if (artPlayer != null && art != null) artPlayer.Play(art.idle);
        }

        private void Update()
        {
            if (artPlayer == null || art == null) return;
            artPlayer.Play(SkillTreePanel.IsOpen ? art.talk : art.idle);
            if (_player != null) artPlayer.Flip(_player.position.x < transform.position.x); // 그림은 오른쪽을 본다
        }

        private void Talk() => SkillTreePanel.Open(displayName);
    }
}
