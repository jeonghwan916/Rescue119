using FireLink119.Network;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FireLink119.UI
{
    public class RoomSceneLoadButtons : MonoBehaviour
    {
        // 현재 로비 버튼은 샘플 오브젝트를 복사해 쓰는 구조라서, 프리팹 연결을 건드리지 않도록 이름으로 찾는다.
        [SerializeField] private string _hostButtonName = "Host Button";
        [SerializeField] private string _clientButtonName = "Client Button";

        private XRSimpleInteractable _hostButton;
        private XRSimpleInteractable _clientButton;
        private LobbyRoomCodeFlow _roomCodeFlow;

        private void Awake()
        {
            _hostButton = FindButton(_hostButtonName);
            _clientButton = FindButton(_clientButtonName);

            // 이 스크립트는 선택 버튼만 담당하므로, 코드 입력 흐름은 별도 컴포넌트에 위임한다.
            _roomCodeFlow = GetComponent<LobbyRoomCodeFlow>();
            if (_roomCodeFlow == null)
            {
                _roomCodeFlow = gameObject.AddComponent<LobbyRoomCodeFlow>();
            }
        }

        private void OnEnable()
        {
            // XR 버튼은 Unity UI Button.onClick이 아니라 selectEntered로 선택 입력을 받는다.
            if (_hostButton != null)
            {
                _hostButton.selectEntered.AddListener(OnHostButtonSelected);
            }

            if (_clientButton != null)
            {
                _clientButton.selectEntered.AddListener(OnClientButtonSelected);
            }
        }

        private void OnDisable()
        {
            // 로비 오브젝트가 꺼졌다 켜질 때 같은 이벤트가 중복 등록되지 않도록 해제한다.
            if (_hostButton != null)
            {
                _hostButton.selectEntered.RemoveListener(OnHostButtonSelected);
            }

            if (_clientButton != null)
            {
                _clientButton.selectEntered.RemoveListener(OnClientButtonSelected);
            }
        }

        private XRSimpleInteractable FindButton(string buttonName)
        {
            // 버튼 참조가 비어 있어도 현재 씬 구성만 맞으면 동작하도록 이름 기반 자동 연결을 유지한다.
            GameObject buttonObject = GameObject.Find(buttonName);
            if (buttonObject == null)
            {
                Debug.LogWarning($"[RoomSceneLoadButtons] Button not found: {buttonName}");
                return null;
            }

            if (!buttonObject.TryGetComponent(out XRSimpleInteractable interactable))
            {
                Debug.LogWarning($"[RoomSceneLoadButtons] XRSimpleInteractable not found: {buttonName}");
                return null;
            }

            return interactable;
        }

        private void OnHostButtonSelected(SelectEnterEventArgs args)
        {
            // 호스트 버튼은 바로 네트워크를 시작하지 않고, 먼저 호스트용 코드 입력 패드를 연다.
            _roomCodeFlow.ShowRoomCodePad(LobbyRoomRole.Host);
        }

        private void OnClientButtonSelected(SelectEnterEventArgs args)
        {
            // 클라이언트도 같은 입력 흐름을 쓰되, 제출 시 Client 모드로 접속하도록 역할만 넘긴다.
            _roomCodeFlow.ShowRoomCodePad(LobbyRoomRole.Client);
        }
    }
}
