using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace FireLink119.Player
{
    [DisallowMultipleComponent]
    public class PlayerAvatarCalibrationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _cameraOffset;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Transform _avatarRoot;
        [SerializeField] private Transform _avatarHead;
        [SerializeField] private Transform _leftHandBone;
        [SerializeField] private Transform _rightHandBone;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;

        [Header("Calibration")]
        [SerializeField] private bool _calibrateOnStart = true;
        [SerializeField] private bool _applySavedCalibrationOnStart = true;
        [SerializeField] private bool _autoCaptureWhenStable = true;
        [SerializeField] private float _targetEyeHeight = 1.65f;
        [SerializeField] private float _maxHeightOffset = 0.8f;
        [SerializeField] private float _requiredStableSeconds = 2f;
        [SerializeField] private float _stablePositionThreshold = 0.03f;
        [SerializeField] private float _minimumHandSpread = 0.55f;
        [SerializeField] private bool _calibrateHandTargetOffsets = true;

        [Header("Recenter")]
        [SerializeField] private InputActionReference _recenterActionReference;
        [SerializeField] private float _recenterHoldSeconds = 1.2f;

        [Header("Optional UI")]
        [SerializeField] private GameObject _calibrationGuideRoot;
        [SerializeField] private bool _createGuideUiIfMissing = true;
        [SerializeField] private Vector3 _guideLocalPosition = new Vector3(0f, 0f, 1.2f);
        [SerializeField] private Vector2 _guideCanvasSize = new Vector2(1800f, 1000f);
        [SerializeField] private float _guideCanvasScale = 0.001f;
        [SerializeField] private float _guideFontSize = 64f;
        [SerializeField] private string _guideReadyText = "정면을 바라보고\n양팔을 양옆으로 뻗어 주세요.";
        [SerializeField] private string _guideHoldText = "자세를 유지해 주세요.";
        [SerializeField] private string _guideSpreadText = "양팔을 더 넓게 뻗어 주세요.";
        [SerializeField] private GameObject[] _lobbyRootsToHideDuringCalibration;
        [SerializeField] private UnityEvent _onCalibrationStarted;
        [SerializeField] private UnityEvent _onCalibrationCompleted;

        private InputAction _recenterAction;
        private TextMeshProUGUI _guideText;
        private Vector3 _baseCameraOffsetLocalPosition;
        private bool _hasBaseCameraOffsetLocalPosition;
        private bool _isCalibrating;
        private bool _hasPreviousSample;
        private Vector3 _previousHeadPosition;
        private Vector3 _previousLeftHandPosition;
        private Vector3 _previousRightHandPosition;
        private float _stableSeconds;
        private float _appliedHeightOffset;
        private float _recenterHeldSeconds;
        private bool _warnedMissingRequiredReferences;
        private bool _enabledRecenterActionHere;

        private void Awake()
        {
            ResolveMissingReferences();
            EnsureCalibrationGuideUi();
        }

        private void OnEnable()
        {
            ResolveRecenterAction();

            if (_recenterAction != null && !_recenterAction.enabled)
            {
                _recenterAction.Enable();
                _enabledRecenterActionHere = true;
            }
        }

        private void Start()
        {
            ResolveMissingReferences();
            EnsureCalibrationGuideUi();
            CaptureBaseCameraOffset();

            if (_applySavedCalibrationOnStart && PlayerAvatarCalibrationStore.HasCalibration)
            {
                ApplyCalibration(PlayerAvatarCalibrationStore.Data);
                SetCalibrationUiActive(false);
                return;
            }

            if (_calibrateOnStart)
            {
                BeginCalibration();
            }
        }

        private void OnDisable()
        {
            if (_recenterAction != null && _enabledRecenterActionHere)
            {
                _recenterAction.Disable();
            }

            _enabledRecenterActionHere = false;
        }

        private void Update()
        {
            UpdateCalibrationStability();
            UpdateRecenterHold();
        }

        public void BeginCalibration()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            _isCalibrating = true;
            _hasPreviousSample = false;
            _stableSeconds = 0f;
            UpdateGuideText(_guideReadyText);
            SetCalibrationUiActive(true);
            _onCalibrationStarted?.Invoke();
        }

        public void CaptureCalibrationNow()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            PlayerAvatarCalibrationData data = BuildCalibrationData(captureHandTargetOffsets: true);
            PlayerAvatarCalibrationStore.Save(data);
            ApplyCalibration(data);

            _isCalibrating = false;
            _stableSeconds = 0f;
            SetCalibrationUiActive(false);
            _onCalibrationCompleted?.Invoke();

            Debug.Log($"[PlayerAvatarCalibrationController] Calibration completed. MeasuredEyeHeight: {data.MeasuredEyeHeight:F2}, HeightOffset: {data.HeightOffset:F2}");
        }

        public void RecenterViewToCurrentPose()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            PlayerAvatarCalibrationData data = PlayerAvatarCalibrationStore.HasCalibration
                ? PlayerAvatarCalibrationStore.Data
                : PlayerAvatarCalibrationData.CreateDefault(_targetEyeHeight);

            PlayerAvatarCalibrationData updatedData = BuildCalibrationData(captureHandTargetOffsets: false);
            updatedData.LeftHandTargetLocalPosition = data.LeftHandTargetLocalPosition;
            updatedData.LeftHandTargetLocalRotation = data.LeftHandTargetLocalRotation;
            updatedData.RightHandTargetLocalPosition = data.RightHandTargetLocalPosition;
            updatedData.RightHandTargetLocalRotation = data.RightHandTargetLocalRotation;

            PlayerAvatarCalibrationStore.Save(updatedData);
            ApplyCalibration(updatedData);

            Debug.Log($"[PlayerAvatarCalibrationController] View recentered. MeasuredEyeHeight: {updatedData.MeasuredEyeHeight:F2}, HeightOffset: {updatedData.HeightOffset:F2}");
        }

        private void UpdateCalibrationStability()
        {
            if (!_isCalibrating || !_autoCaptureWhenStable)
            {
                return;
            }

            if (!HasRequiredReferences())
            {
                return;
            }

            Vector3 headPosition = _cameraTransform.position;
            Vector3 leftHandPosition = _leftHandTarget.position;
            Vector3 rightHandPosition = _rightHandTarget.position;

            if (!IsPoseWideEnough(leftHandPosition, rightHandPosition))
            {
                UpdateGuideText(_guideSpreadText);
                ResetStableTimer(headPosition, leftHandPosition, rightHandPosition);
                return;
            }

            if (!_hasPreviousSample)
            {
                ResetStableTimer(headPosition, leftHandPosition, rightHandPosition);
                _hasPreviousSample = true;
                return;
            }

            bool isStable =
                Vector3.Distance(_previousHeadPosition, headPosition) <= _stablePositionThreshold &&
                Vector3.Distance(_previousLeftHandPosition, leftHandPosition) <= _stablePositionThreshold &&
                Vector3.Distance(_previousRightHandPosition, rightHandPosition) <= _stablePositionThreshold;

            _previousHeadPosition = headPosition;
            _previousLeftHandPosition = leftHandPosition;
            _previousRightHandPosition = rightHandPosition;

            if (!isStable)
            {
                _stableSeconds = 0f;
                UpdateGuideText(_guideHoldText);
                return;
            }

            _stableSeconds += Time.deltaTime;
            UpdateGuideText($"{_guideHoldText}\n{Mathf.Clamp01(_stableSeconds / _requiredStableSeconds) * 100f:0}%");
            if (_stableSeconds >= _requiredStableSeconds)
            {
                CaptureCalibrationNow();
            }
        }

        private void UpdateRecenterHold()
        {
            if (_recenterAction == null)
            {
                return;
            }

            if (_recenterAction.IsPressed())
            {
                _recenterHeldSeconds += Time.deltaTime;
                if (_recenterHeldSeconds >= _recenterHoldSeconds)
                {
                    _recenterHeldSeconds = 0f;
                    RecenterViewToCurrentPose();
                }

                return;
            }

            _recenterHeldSeconds = 0f;
        }

        private PlayerAvatarCalibrationData BuildCalibrationData(bool captureHandTargetOffsets)
        {
            CaptureBaseCameraOffset();

            float measuredEyeHeight = _cameraTransform.position.y - _appliedHeightOffset;
            float heightOffset = Mathf.Clamp(_targetEyeHeight - measuredEyeHeight, -_maxHeightOffset, _maxHeightOffset);

            PlayerAvatarCalibrationData data = PlayerAvatarCalibrationData.CreateDefault(_targetEyeHeight);
            data.IsCalibrated = true;
            data.MeasuredEyeHeight = measuredEyeHeight;
            data.HeightOffset = heightOffset;

            CaptureCurrentHandTargetOffsets(ref data, captureHandTargetOffsets);
            return data;
        }

        private void CaptureCurrentHandTargetOffsets(ref PlayerAvatarCalibrationData data, bool captureHandTargetOffsets)
        {
            if (!_calibrateHandTargetOffsets || !captureHandTargetOffsets)
            {
                data.LeftHandTargetLocalPosition = _leftHandTarget != null ? _leftHandTarget.localPosition : Vector3.zero;
                data.LeftHandTargetLocalRotation = _leftHandTarget != null ? _leftHandTarget.localRotation : Quaternion.identity;
                data.RightHandTargetLocalPosition = _rightHandTarget != null ? _rightHandTarget.localPosition : Vector3.zero;
                data.RightHandTargetLocalRotation = _rightHandTarget != null ? _rightHandTarget.localRotation : Quaternion.identity;
                return;
            }

            CaptureTargetOffsetFromBone(_leftHandTarget, _leftHandBone, out data.LeftHandTargetLocalPosition, out data.LeftHandTargetLocalRotation);
            CaptureTargetOffsetFromBone(_rightHandTarget, _rightHandBone, out data.RightHandTargetLocalPosition, out data.RightHandTargetLocalRotation);
        }

        private void CaptureTargetOffsetFromBone(
            Transform handTarget,
            Transform handBone,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            if (handTarget == null || handTarget.parent == null || handBone == null)
            {
                localPosition = handTarget != null ? handTarget.localPosition : Vector3.zero;
                localRotation = handTarget != null ? handTarget.localRotation : Quaternion.identity;
                return;
            }

            // 손 타겟은 컨트롤러의 자식이므로, 보정 순간의 손 본 위치를 부모 기준 로컬 오프셋으로 저장한다.
            localPosition = handTarget.parent.InverseTransformPoint(handBone.position);
            localRotation = Quaternion.Inverse(handTarget.parent.rotation) * handBone.rotation;
        }

        private void ApplyCalibration(PlayerAvatarCalibrationData data)
        {
            if (!data.IsCalibrated)
            {
                return;
            }

            CaptureBaseCameraOffset();

            if (_cameraOffset != null)
            {
                Vector3 cameraOffsetPosition = _baseCameraOffsetLocalPosition;
                cameraOffsetPosition.y += data.HeightOffset;
                _cameraOffset.localPosition = cameraOffsetPosition;
                _appliedHeightOffset = data.HeightOffset;
            }

            ApplyHandTargetOffset(_leftHandTarget, data.LeftHandTargetLocalPosition, data.LeftHandTargetLocalRotation);
            ApplyHandTargetOffset(_rightHandTarget, data.RightHandTargetLocalPosition, data.RightHandTargetLocalRotation);
        }

        private void ApplyHandTargetOffset(Transform handTarget, Vector3 localPosition, Quaternion localRotation)
        {
            if (handTarget == null)
            {
                return;
            }

            handTarget.localPosition = localPosition;
            handTarget.localRotation = localRotation;
        }

        private bool IsPoseWideEnough(Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            Vector3 handDelta = rightHandPosition - leftHandPosition;
            handDelta.y = 0f;
            return handDelta.magnitude >= _minimumHandSpread;
        }

        private void ResetStableTimer(Vector3 headPosition, Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            _previousHeadPosition = headPosition;
            _previousLeftHandPosition = leftHandPosition;
            _previousRightHandPosition = rightHandPosition;
            _stableSeconds = 0f;
        }

        private bool HasRequiredReferences()
        {
            ResolveMissingReferences();

            bool hasReferences =
                _cameraOffset != null &&
                _cameraTransform != null &&
                _leftHandTarget != null &&
                _rightHandTarget != null;

            if (!hasReferences && !_warnedMissingRequiredReferences)
            {
                Debug.LogWarning("[PlayerAvatarCalibrationController] Camera Offset, Main Camera, LeftHandTarget, and RightHandTarget are required.");
                _warnedMissingRequiredReferences = true;
            }

            return hasReferences;
        }

        private void ResolveMissingReferences()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraOffset == null && _cameraTransform != null)
            {
                _cameraOffset = _cameraTransform.parent;
            }

            PlayerAvatarLocomotionAnimator locomotionAnimator = FindFirstObjectByType<PlayerAvatarLocomotionAnimator>();
            if (_avatarRoot == null && locomotionAnimator != null)
            {
                _avatarRoot = locomotionAnimator.AvatarRoot;
            }

            PlayerAvatarHandTargets handTargets = _avatarRoot != null ? _avatarRoot.GetComponent<PlayerAvatarHandTargets>() : null;
            if (handTargets == null && _avatarRoot != null)
            {
                handTargets = _avatarRoot.GetComponentInChildren<PlayerAvatarHandTargets>();
            }

            if (handTargets == null)
            {
                handTargets = FindFirstObjectByType<PlayerAvatarHandTargets>();
            }

            if (handTargets != null)
            {
                _leftHandTarget ??= handTargets.LeftHandTarget;
                _rightHandTarget ??= handTargets.RightHandTarget;
            }

            if (_avatarRoot != null)
            {
                _avatarHead ??= FindChildRecursive(_avatarRoot, "Head");
                _leftHandBone ??= FindChildRecursive(_avatarRoot, "LeftHand");
                _rightHandBone ??= FindChildRecursive(_avatarRoot, "RightHand");
            }
        }

        private void ResolveRecenterAction()
        {
            _recenterAction = _recenterActionReference != null ? _recenterActionReference.action : null;
        }

        private void CaptureBaseCameraOffset()
        {
            if (_hasBaseCameraOffsetLocalPosition || _cameraOffset == null)
            {
                return;
            }

            _baseCameraOffsetLocalPosition = _cameraOffset.localPosition;
            _hasBaseCameraOffsetLocalPosition = true;
        }

        private void SetCalibrationUiActive(bool isCalibrating)
        {
            if (_calibrationGuideRoot == null && isCalibrating)
            {
                EnsureCalibrationGuideUi();
            }

            if (_calibrationGuideRoot != null)
            {
                _calibrationGuideRoot.SetActive(isCalibrating);
            }

            if (_lobbyRootsToHideDuringCalibration == null)
            {
                return;
            }

            for (int i = 0; i < _lobbyRootsToHideDuringCalibration.Length; i++)
            {
                if (_lobbyRootsToHideDuringCalibration[i] != null)
                {
                    _lobbyRootsToHideDuringCalibration[i].SetActive(!isCalibrating);
                }
            }
        }

        private void EnsureCalibrationGuideUi()
        {
            if (_calibrationGuideRoot != null)
            {
                _guideText = _calibrationGuideRoot.GetComponentInChildren<TextMeshProUGUI>(true);
                return;
            }

            if (!_createGuideUiIfMissing || _cameraTransform == null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Calibration Guide Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Transform canvasTransform = canvasObject.transform;
            canvasTransform.SetParent(_cameraTransform, false);
            canvasTransform.localPosition = _guideLocalPosition;
            canvasTransform.localRotation = Quaternion.identity;
            canvasTransform.localScale = Vector3.one * _guideCanvasScale;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = _guideCanvasSize;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cameraTransform.GetComponent<Camera>();
            canvas.sortingOrder = 100;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 10f;

            GameObject backgroundObject = new GameObject("Black Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(canvasTransform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            StretchToParent(backgroundRect, Vector2.zero);

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = Color.black;
            backgroundImage.raycastTarget = true;

            GameObject textObject = new GameObject("Guide Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasTransform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            StretchToParent(textRect, new Vector2(120f, 120f));

            _guideText = textObject.GetComponent<TextMeshProUGUI>();
            _guideText.text = _guideReadyText;
            _guideText.fontSize = _guideFontSize;
            _guideText.alignment = TextAlignmentOptions.Center;
            _guideText.color = Color.white;
            _guideText.textWrappingMode = TextWrappingModes.Normal;
            _guideText.raycastTarget = false;

            _calibrationGuideRoot = canvasObject;
            _calibrationGuideRoot.SetActive(false);
        }

        private void UpdateGuideText(string guideText)
        {
            if (_guideText != null)
            {
                _guideText.text = guideText;
            }
        }

        private void StretchToParent(RectTransform rectTransform, Vector2 padding)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = padding;
            rectTransform.offsetMax = -padding;
        }

        private Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform result = FindChildRecursive(child, targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
