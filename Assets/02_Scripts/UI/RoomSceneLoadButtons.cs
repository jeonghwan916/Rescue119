using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FireLink119.UI
{
    // 로비의 Host/Client 버튼은 일반 Unity UI Button이 아니라 XR 상호작용 버튼이다.
    // 그래서 클릭 처리를 Button.onClick이 아닌 XRSimpleInteractable.selectEntered 이벤트에 연결한다.
    public class RoomSceneLoadButtons : MonoBehaviour
    {
        // 현재 단계에서는 Host/Client 역할 분기나 Photon 접속을 하지 않고, 두 버튼 모두 대기실 씬으로만 이동한다.
        // 나중에 Photon 로직을 붙일 때도 씬 이름은 Inspector에서 바꿀 수 있게 문자열로 분리해 둔다.
        [SerializeField] private string _roomSceneName = "RoomScene";

        // 프리팹 연결을 끊은 뒤에도 기존 씬 오브젝트 이름을 기준으로 버튼을 찾기 위해 이름을 SerializeField로 둔다.
        // Inspector 참조를 직접 물리는 방식보다 안정성은 낮지만, 현재처럼 UI 구조를 점검하는 단계에서는 연결 작업을 줄일 수 있다.
        [SerializeField] private string _hostButtonName = "Host Button";
        [SerializeField] private string _clientButtonName = "Client Button";

        // 이벤트 등록과 해제를 같은 인스턴스에 대해 수행해야 하므로 Awake에서 찾은 참조를 필드에 보관한다.
        private XRSimpleInteractable _hostButton;
        private XRSimpleInteractable _clientButton;

        // OnEnable보다 먼저 실행되는 Awake에서 버튼 참조를 준비한다.
        // 이렇게 해야 오브젝트가 활성화될 때 이벤트 등록 단계에서 null 참조로 실패하지 않는다.
        private void Awake()
        {
            _hostButton = FindButton(_hostButtonName);
            _clientButton = FindButton(_clientButtonName);
        }

        // 오브젝트가 활성화될 때 XR 버튼 선택 이벤트에 씬 이동 함수를 연결한다.
        // Unity 생명주기상 비활성화/재활성화가 반복될 수 있으므로 OnEnable에서 등록하고 OnDisable에서 해제하는 구조를 사용한다.
        private void OnEnable()
        {
            AddListener(_hostButton);
            AddListener(_clientButton);
        }

        // 오브젝트가 꺼질 때 등록했던 이벤트를 해제한다.
        // 해제하지 않으면 씬 이동 후에도 이전 리스너가 남거나, 재활성화 시 같은 함수가 중복 등록될 수 있다.
        private void OnDisable()
        {
            RemoveListener(_hostButton);
            RemoveListener(_clientButton);
        }

        // 현재 씬에서 이름으로 버튼 오브젝트를 찾고, XR 선택 이벤트를 받을 수 있는 컴포넌트를 꺼낸다.
        // 버튼이 없거나 XRSimpleInteractable이 빠진 경우에는 경고만 남기고 null을 반환해서 나머지 버튼은 계속 동작하게 한다.
        private XRSimpleInteractable FindButton(string buttonName)
        {
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

        // 찾은 XR 버튼에 selectEntered 리스너를 추가한다.
        // null 검사를 먼저 하는 이유는 버튼 하나가 빠져도 다른 버튼까지 막히지 않게 하기 위해서다.
        private void AddListener(XRSimpleInteractable interactable)
        {
            if (interactable == null)
            {
                return;
            }

            interactable.selectEntered.AddListener(OnRoomButtonSelected);
        }

        // OnEnable에서 등록한 것과 같은 리스너를 제거한다.
        // Unity 이벤트는 같은 대상과 메서드 조합을 제거해야 하므로 AddListener와 같은 필드 참조를 사용한다.
        private void RemoveListener(XRSimpleInteractable interactable)
        {
            if (interactable == null)
            {
                return;
            }

            interactable.selectEntered.RemoveListener(OnRoomButtonSelected);
        }

        // XR 버튼이 선택되면 대기실 씬을 로드한다.
        // 현재 목표는 "로비에서 Host/Client 선택 후 RoomScene으로 이동"까지만이므로, 전달받은 SelectEnterEventArgs는 아직 사용하지 않는다.
        private void OnRoomButtonSelected(SelectEnterEventArgs args)
        {
            SceneManager.LoadScene(_roomSceneName);
        }
    }
}
