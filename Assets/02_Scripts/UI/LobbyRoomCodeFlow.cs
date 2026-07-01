using FireLink119.Network;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;

namespace FireLink119.UI
{
    public class LobbyRoomCodeFlow : MonoBehaviour
    {
        [SerializeField] private string _hostRoomCodePadName = "Host Room Num";
        [SerializeField] private string _clientRoomCodePadName = "Client Room Num";
        [SerializeField] private int _maxDigits = 4;

        private GameObject _hostRoomCodePad;
        private GameObject _clientRoomCodePad;
        private GameObject _activeRoomCodePad;
        private TMP_InputField _activeInputField;
        private XRKeyboard _activeKeyboard;
        private XRKeyboardDisplay _activeKeyboardDisplay;
        private FusionRoomConnector _connector;
        private LobbyRoomRole _activeRole;
        private bool _hasActiveRole;
        private bool _isSanitizingInput;
        private int _currentCodeLength;
        private int _codeLengthBeforeLastChange;

        private void Awake()
        {
            _hostRoomCodePad = FindRoomCodePad(_hostRoomCodePadName, LobbyRoomRole.Host);
            _clientRoomCodePad = FindRoomCodePad(_clientRoomCodePadName, LobbyRoomRole.Client);
            _connector = ResolveConnector();

            // 로비는 역할 선택 화면에서 시작하므로, 선택 전에는 두 넘패드를 모두 숨긴 상태로 맞춘다.
            SetPadActive(_hostRoomCodePad, false);
            SetPadActive(_clientRoomCodePad, false);
        }

        private void OnDisable()
        {
            // 씬 전환이나 오브젝트 비활성 시 이전 넘패드 이벤트가 남아 다음 선택에 섞이지 않게 정리한다.
            ClearActivePadBindings();
        }

        public void ShowRoomCodePad(LobbyRoomRole role)
        {
            // Host/Client 버튼에서 전달된 역할에 맞는 숨겨진 넘패드만 켜고 입력 이벤트를 연결한다.
            GameObject targetPad = role == LobbyRoomRole.Host ? _hostRoomCodePad : _clientRoomCodePad;
            if (targetPad == null)
            {
                string roleName = role == LobbyRoomRole.Host ? "Host" : "Client";
                Debug.LogWarning($"[LobbyRoomCodeFlow] {roleName} room code pad not found.");
                return;
            }

            ClearActivePadBindings();
            SetPadActive(_hostRoomCodePad, false);
            SetPadActive(_clientRoomCodePad, false);

            _activeRole = role;
            _hasActiveRole = true;
            _activeRoomCodePad = targetPad;
            SetPadActive(_activeRoomCodePad, true);

            ConfigureActivePad();
        }

        private FusionRoomConnector ResolveConnector()
        {
            // 네트워크 시작 설정은 Inspector 참조가 필요하므로, 같은 오브젝트에 미리 붙은 컴포넌트만 사용한다.
            FusionRoomConnector connector = GetComponent<FusionRoomConnector>();
            if (connector == null)
            {
                Debug.LogError("[LobbyRoomCodeFlow] FusionRoomConnector is required on the same GameObject.");
            }

            return connector;
        }

        private void ConfigureActivePad()
        {
            // 활성화된 넘패드 내부의 TMP 입력 필드와 XR Keyboard 이벤트를 현재 역할 흐름에 맞게 연결한다.
            _activeInputField = _activeRoomCodePad.GetComponentInChildren<TMP_InputField>(true);
            if (_activeInputField == null)
            {
                Debug.LogWarning($"[LobbyRoomCodeFlow] TMP_InputField not found under {_activeRoomCodePad.name}.");
                return;
            }

            _activeInputField.characterLimit = _maxDigits;
            _activeInputField.text = string.Empty;
            _activeInputField.onValueChanged.AddListener(OnRoomCodeChanged);
            _currentCodeLength = 0;
            _codeLengthBeforeLastChange = 0;

            _activeKeyboardDisplay = _activeRoomCodePad.GetComponentInChildren<XRKeyboardDisplay>(true);
            _activeKeyboard = ResolveKeyboard(_activeRoomCodePad, _activeKeyboardDisplay);
            if (_activeKeyboardDisplay != null)
            {
                // 샘플 키보드의 입력/제출 동작은 유지하고, 여기서는 입력 제한과 네트워크 전달만 추가한다.
                _activeKeyboardDisplay.inputField = _activeInputField;
                if (_activeKeyboard != null)
                {
                    _activeKeyboardDisplay.useSceneKeyboard = true;
                    _activeKeyboardDisplay.keyboard = _activeKeyboard;
                }

                _activeKeyboardDisplay.monitorInputFieldCharacterLimit = true;
                _activeKeyboardDisplay.clearTextOnSubmit = false;
                _activeKeyboardDisplay.onTextSubmitted.AddListener(OnRoomCodeSubmitted);
            }
            else
            {
                Debug.LogWarning($"[LobbyRoomCodeFlow] XRKeyboardDisplay not found under {_activeRoomCodePad.name}.");
            }

            if (_activeKeyboard != null)
            {
                _activeKeyboard.onKeyPressed.AddListener(OnKeyboardKeyPressed);
            }
            else
            {
                Debug.LogWarning($"[LobbyRoomCodeFlow] XRKeyboard not found under {_activeRoomCodePad.name}.");
            }
        }

        private void ClearActivePadBindings()
        {
            // 역할을 다시 선택할 때 이전 패드의 이벤트가 중복 호출되지 않도록 현재 연결만 해제한다.
            if (_activeInputField != null)
            {
                _activeInputField.onValueChanged.RemoveListener(OnRoomCodeChanged);
            }

            if (_activeKeyboardDisplay != null)
            {
                _activeKeyboardDisplay.onTextSubmitted.RemoveListener(OnRoomCodeSubmitted);
            }

            if (_activeKeyboard != null)
            {
                _activeKeyboard.onKeyPressed.RemoveListener(OnKeyboardKeyPressed);
            }

            _activeInputField = null;
            _activeKeyboard = null;
            _activeKeyboardDisplay = null;
            _activeRoomCodePad = null;
            _hasActiveRole = false;
        }

        private void OnRoomCodeChanged(string value)
        {
            // XR 키보드 설정이 누락되거나 붙여넣기가 들어와도 4자리 숫자 규칙을 코드에서 다시 보장한다.
            if (_isSanitizingInput)
            {
                return;
            }

            _codeLengthBeforeLastChange = _currentCodeLength;
            string sanitizedCode = SanitizeRoomCode(value);
            _currentCodeLength = sanitizedCode.Length;

            if (value == sanitizedCode)
            {
                return;
            }

            _isSanitizingInput = true;
            _activeInputField.text = sanitizedCode;
            _isSanitizingInput = false;
        }

        private void OnRoomCodeSubmitted(string submittedCode)
        {
            // Enter 버튼은 최종 확정 액션이므로, 4자리 입력이 완성된 경우에만 Fusion 접속으로 넘긴다.
            if (!_hasActiveRole)
            {
                return;
            }

            string roomCode = SanitizeRoomCode(submittedCode);
            if (roomCode.Length != _maxDigits)
            {
                Debug.LogWarning($"[LobbyRoomCodeFlow] Room code must be {_maxDigits} digits.");
                return;
            }

            if (_connector == null)
            {
                Debug.LogError("[LobbyRoomCodeFlow] Cannot start room because FusionRoomConnector is not assigned.");
                return;
            }

            _connector.StartRoom(_activeRole, roomCode);
        }

        private void OnKeyboardKeyPressed(KeyboardKeyEventArgs args)
        {
            // 백스페이스는 입력 취소 UX에도 쓰기 때문에, 일반 숫자 키와 분리해서 처리한다.
            if (args == null || !IsBackspaceKey(args.key))
            {
                return;
            }

            HandleBackspaceAfterKeyPress();
        }

        private void HandleBackspaceAfterKeyPress()
        {
            // 마지막 숫자를 지운 직후에는 닫지 않고, 이미 빈 상태에서 한 번 더 누른 경우에만 역할 선택으로 돌아간다.
            if (_activeInputField == null || !string.IsNullOrEmpty(_activeInputField.text))
            {
                return;
            }

            // If the last press only deleted the final digit, keep the pad open; the next empty backspace cancels selection.
            if (_codeLengthBeforeLastChange > 0)
            {
                _codeLengthBeforeLastChange = 0;
                _currentCodeLength = 0;
                return;
            }

            HideActivePad();
        }

        private void HideActivePad()
        {
            // 입력 취소 시에는 현재 패드만 끄고 버튼 선택 상태로 돌아가도록 이벤트와 입력값을 함께 정리한다.
            GameObject padToHide = _activeRoomCodePad;
            if (_activeInputField != null)
            {
                _activeInputField.text = string.Empty;
            }

            ClearActivePadBindings();
            SetPadActive(padToHide, false);
        }

        private XRKeyboard ResolveKeyboard(GameObject roomCodePad, XRKeyboardDisplay keyboardDisplay)
        {
            // 커스텀 패드가 샘플 프리팹 구조와 달라도, Display 참조와 자식/부모 Keyboard를 순서대로 찾아 연결한다.
            if (keyboardDisplay != null && keyboardDisplay.keyboard != null)
            {
                return keyboardDisplay.keyboard;
            }

            XRKeyboard childKeyboard = roomCodePad.GetComponentInChildren<XRKeyboard>(true);
            if (childKeyboard != null)
            {
                return childKeyboard;
            }

            Transform current = roomCodePad.transform.parent;
            while (current != null)
            {
                XRKeyboard parentKeyboard = current.GetComponent<XRKeyboard>();
                if (parentKeyboard != null)
                {
                    return parentKeyboard;
                }

                current = current.parent;
            }

            return null;
        }

        private GameObject FindRoomCodePad(string configuredName, LobbyRoomRole role)
        {
            // 비활성 오브젝트는 GameObject.Find로 찾을 수 없어서, 이름 우선 후 역할 기반 fallback을 둔다.
            GameObject exactMatch = FindSceneObjectByName(configuredName);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            return FindSceneObjectByRole(role);
        }

        private GameObject FindSceneObjectByName(string objectName)
        {
            // Resources.FindObjectsOfTypeAll을 사용해 현재 씬의 비활성 넘패드까지 검색한다.
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject candidate = transforms[i].gameObject;
                if (!candidate.scene.IsValid())
                {
                    continue;
                }

                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private GameObject FindSceneObjectByRole(LobbyRoomRole role)
        {
            // Inspector 이름이 조금 달라도 Host/Client와 Num이 포함된 오브젝트를 찾아 기본 동작을 살린다.
            string roleName = role == LobbyRoomRole.Host ? "Host" : "Client";
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject candidate = transforms[i].gameObject;
                if (!candidate.scene.IsValid())
                {
                    continue;
                }

                string candidateName = candidate.name;
                bool matchesRole = candidateName.Contains(roleName);
                bool looksLikeNumberPad = candidateName.Contains("Room Num") || candidateName.Contains("Num");

                if (matchesRole && looksLikeNumberPad)
                {
                    return candidate;
                }
            }

            return null;
        }

        private string SanitizeRoomCode(string value)
        {
            // 입력값은 Photon 방 이름으로 쓰이므로 숫자만 남기고 최대 자리수에서 잘라낸다.
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int length = Mathf.Min(value.Length, _maxDigits);
            char[] digits = new char[_maxDigits];
            int digitCount = 0;

            for (int i = 0; i < value.Length && digitCount < length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    continue;
                }

                digits[digitCount] = value[i];
                digitCount++;
            }

            return new string(digits, 0, digitCount);
        }

        private bool IsBackspaceKey(XRKeyboardKey key)
        {
            // 샘플 프리팹마다 Backspace를 KeyCode 또는 문자로 표현할 수 있어 두 경우를 모두 허용한다.
            if (key == null)
            {
                return false;
            }

            return key.keyCode == KeyCode.Backspace || key.character == "\b";
        }

        private void SetPadActive(GameObject pad, bool isActive)
        {
            // null 방어를 한 곳에 모아 패드가 아직 없을 때도 선택 버튼 흐름이 끊기지 않게 한다.
            if (pad != null)
            {
                pad.SetActive(isActive);
            }
        }
    }
}
