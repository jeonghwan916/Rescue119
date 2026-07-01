using FireLink119.Player;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FireLink119.NPC
{
    public class NPCShoulderGrabTrigger : MonoBehaviour
    {
        [SerializeField] private NPCController _npcController;
        [SerializeField] private PlayerType _playerType;

        private XRBaseInteractable _interactable;
        
        [Header("Haptic Feedback")]
        [SerializeField, Range(0f, 1f)] private float _hapticAmplitude = 0.5f;
        [SerializeField] private float _hapticDuration = 0.08f;

        private void Awake()
        {
            if (_npcController == null)
            {
                _npcController = GetComponentInParent<NPCController>();
            }

            _interactable = GetComponent<XRBaseInteractable>();
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            PlayerIdentifier player = args.interactorObject.transform.GetComponentInParent<PlayerIdentifier>();
            if (player == null)
            {
                return;
            }

            if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(_hapticAmplitude, _hapticDuration);
            }

            Debug.Log($"어깨를 잡은 플레이어 : {player.PlayerType}");
            _npcController.StartFollowingPlayer(player.PlayerType);
        }
    }
}