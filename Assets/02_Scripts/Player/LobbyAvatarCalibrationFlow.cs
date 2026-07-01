using FireLink119.UI;
using UnityEngine;
using UnityEngine.Events;

namespace FireLink119.Player
{
    [DisallowMultipleComponent]
    public class LobbyAvatarCalibrationFlow : MonoBehaviour
    {
        private enum ControllerDirectionAxis
        {
            Forward,
            Right,
            Up
        }

        [Header("References")]
        [SerializeField] private CalibrationGuideWorldUi _guideUi;
        [SerializeField] private Transform _cameraOffset;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Transform _avatarRoot;
        [SerializeField] private Transform _avatarEyeReference;
        [SerializeField] private Transform _leftHandBone;
        [SerializeField] private Transform _rightHandBone;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;
        [SerializeField] private Transform _xrOriginRoot;
        [SerializeField] private GameObject _locomotionRoot;

        [Header("Calibration")]
        [SerializeField] private bool _startOnAwake = true;
        [SerializeField] private float _targetEyeHeight = 1.65f;
        [SerializeField] private float _maxHeightOffset = 0.8f;
        [SerializeField] private float _maxCameraAlignmentDistance = 1.2f;
        [SerializeField] private float _requiredStableSeconds = 2f;
        [SerializeField] private float _stablePositionThreshold = 0.08f;
        [SerializeField] private float _unstableDecaySpeed = 2f;
        [SerializeField] private bool _logCalibrationState = true;
        [SerializeField] private float _debugLogInterval = 1f;

        [Header("Pose Gate")]
        [SerializeField] private bool _requireOppositeControllerDirections = true;
        [SerializeField] private ControllerDirectionAxis _controllerDirectionAxis = ControllerDirectionAxis.Forward;
        [SerializeField] private float _oppositeDirectionDotThreshold = -0.45f;

        [Header("Movement Lock")]
        [SerializeField] private bool _lockMovementDuringCalibration = true;
        [SerializeField] private bool _lockPhysicalHeadPosition = true;

        [Header("Guide Text")]
        [SerializeField] private string _readyText = "\uc815\uba74\uc744 \ubc14\ub77c\ubcf4\uace0\n\uc591\ud314\uc744 \uc591\uc606\uc73c\ub85c \ubee7\uc5b4 \uc8fc\uc138\uc694.";
        [SerializeField] private string _poseText = "\ucee8\ud2b8\ub864\ub7ec\ub97c \uc11c\ub85c \ubc18\ub300 \ubc29\ud5a5\uc73c\ub85c \ud5a5\ud558\uac8c\n\uc591\ud314\uc744 \ubc8c\ub824 \uc8fc\uc138\uc694.";
        [SerializeField] private string _holdText = "\uc790\uc138\ub97c \uc720\uc9c0\ud574 \uc8fc\uc138\uc694.";
        [SerializeField] private string _completeText = "\ubcf4\uc815\uc774 \uc644\ub8cc\ub418\uc5c8\uc2b5\ub2c8\ub2e4.";

        [Header("Events")]
        [SerializeField] private UnityEvent _onCalibrationStarted;
        [SerializeField] private UnityEvent _onCalibrationCompleted;

        private Vector3 _baseCameraOffsetLocalPosition;
        private bool _hasBaseCameraOffsetLocalPosition;
        private bool _isCalibrating;
        private bool _hasPreviousSample;
        private Vector3 _previousHeadPosition;
        private Vector3 _previousLeftHandPosition;
        private Vector3 _previousRightHandPosition;
        private float _stableSeconds;
        private float _appliedHeightOffset;
        private float _debugLogTimer;
        private bool _warnedMissingReferences;
        private bool _hasMovementLock;
        private bool _locomotionRootWasActive;
        private Vector3 _lockedHeadWorldPosition;

        private void Awake()
        {
            ResolveReferences();
            CaptureBaseCameraOffset();
        }

        private void Start()
        {
            if (_startOnAwake)
            {
                BeginCalibration();
            }
        }

        private void Update()
        {
            if (!_isCalibrating)
            {
                return;
            }

            UpdateCalibration();
        }

        private void OnDisable()
        {
            EndMovementLock();
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
            BeginMovementLock();

            if (_guideUi != null)
            {
                _guideUi.SetGuideText(_readyText);
                _guideUi.SetVisible(true);
            }

            _onCalibrationStarted?.Invoke();

            Debug.Log("[LobbyAvatarCalibrationFlow] Calibration started.");
        }

        public void CompleteCalibrationNow()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            PlayerAvatarCalibrationData data = BuildCalibrationData();
            PlayerAvatarCalibrationStore.Save(data);
            ApplyCalibration(data);

            _isCalibrating = false;
            _stableSeconds = 0f;
            EndMovementLock();

            if (_guideUi != null)
            {
                _guideUi.SetGuideText(_completeText);
                _guideUi.SetVisible(false);
            }

            _onCalibrationCompleted?.Invoke();

            Debug.Log($"[LobbyAvatarCalibrationFlow] Calibration completed. MeasuredEyeHeight: {data.MeasuredEyeHeight:F2}, HeightOffset: {data.HeightOffset:F2}");
        }

        private void UpdateCalibration()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            UpdateMovementLock();

            Vector3 headPosition = _cameraTransform.position;
            Vector3 leftHandPosition = _leftHandTarget.position;
            Vector3 rightHandPosition = _rightHandTarget.position;

            if (!HasRequiredControllerPose())
            {
                ResetStableTimer(headPosition, leftHandPosition, rightHandPosition);
                UpdateGuideTextWithProgress(_poseText);
                LogCalibrationState("Controllers are not facing opposite directions.", headPosition, leftHandPosition, rightHandPosition);
                return;
            }

            if (!_hasPreviousSample)
            {
                ResetStableTimer(headPosition, leftHandPosition, rightHandPosition);
                _hasPreviousSample = true;
                UpdateGuideTextWithProgress(_holdText);
                return;
            }

            float headDelta = Vector3.Distance(_previousHeadPosition, headPosition);
            float leftHandDelta = Vector3.Distance(_previousLeftHandPosition, leftHandPosition);
            float rightHandDelta = Vector3.Distance(_previousRightHandPosition, rightHandPosition);

            bool isStable =
                headDelta <= _stablePositionThreshold &&
                leftHandDelta <= _stablePositionThreshold &&
                rightHandDelta <= _stablePositionThreshold;

            _previousHeadPosition = headPosition;
            _previousLeftHandPosition = leftHandPosition;
            _previousRightHandPosition = rightHandPosition;

            if (!isStable)
            {
                _stableSeconds = Mathf.Max(0f, _stableSeconds - Time.deltaTime * _unstableDecaySpeed);
                UpdateGuideTextWithProgress(_holdText);
                LogCalibrationState($"Pose is moving. Head: {headDelta:F3}, Left: {leftHandDelta:F3}, Right: {rightHandDelta:F3}", headPosition, leftHandPosition, rightHandPosition);
                return;
            }

            _stableSeconds += Time.deltaTime;
            UpdateGuideTextWithProgress(_holdText);

            if (_stableSeconds >= _requiredStableSeconds)
            {
                CompleteCalibrationNow();
            }
        }

        private PlayerAvatarCalibrationData BuildCalibrationData()
        {
            CaptureBaseCameraOffset();

            Vector3 cameraAlignmentDelta = GetCameraAlignmentDelta();
            float measuredEyeHeight = _cameraTransform.position.y;
            float targetEyeHeight = _avatarEyeReference != null ? _avatarEyeReference.position.y : _targetEyeHeight;

            PlayerAvatarCalibrationData data = PlayerAvatarCalibrationData.CreateDefault(targetEyeHeight);
            data.IsCalibrated = true;
            data.MeasuredEyeHeight = measuredEyeHeight;
            data.HeightOffset = cameraAlignmentDelta.y;
            data.CameraOffsetWorldDelta = cameraAlignmentDelta;

            return data;
        }

        private void ApplyCalibration(PlayerAvatarCalibrationData data)
        {
            if (!data.IsCalibrated)
            {
                return;
            }

            if (_cameraOffset != null)
            {
                // 사용자 실제 HMD 높이를 그대로 쓰지 않고, 가상 카메라가 아바타의 눈 기준점에 오도록 XR 카메라 기준점만 보정한다.
                _cameraOffset.position += data.CameraOffsetWorldDelta;
                _appliedHeightOffset += data.CameraOffsetWorldDelta.y;
            }
        }

        private bool HasRequiredControllerPose()
        {
            if (!_requireOppositeControllerDirections)
            {
                return true;
            }

            Vector3 leftDirection = GetControllerDirection(_leftHandTarget);
            Vector3 rightDirection = GetControllerDirection(_rightHandTarget);

            if (leftDirection.sqrMagnitude < 0.0001f || rightDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float directionDot = Vector3.Dot(leftDirection.normalized, rightDirection.normalized);
            return directionDot <= _oppositeDirectionDotThreshold;
        }

        private Vector3 GetControllerDirection(Transform controllerTransform)
        {
            if (controllerTransform == null)
            {
                return Vector3.zero;
            }

            switch (_controllerDirectionAxis)
            {
                case ControllerDirectionAxis.Right:
                    return controllerTransform.right;
                case ControllerDirectionAxis.Up:
                    return controllerTransform.up;
                default:
                    return controllerTransform.forward;
            }
        }

        private void ResetStableTimer(Vector3 headPosition, Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            _previousHeadPosition = headPosition;
            _previousLeftHandPosition = leftHandPosition;
            _previousRightHandPosition = rightHandPosition;
            _stableSeconds = 0f;
        }

        private void UpdateGuideText(string guideText)
        {
            if (_guideUi != null)
            {
                _guideUi.SetGuideText(guideText);
            }
        }

        private void UpdateGuideTextWithProgress(string guideText)
        {
            float progress = Mathf.Clamp01(_stableSeconds / _requiredStableSeconds) * 100f;
            UpdateGuideText($"{guideText}\n{progress:0}%");
        }

        private void LogCalibrationState(string reason, Vector3 headPosition, Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            if (!_logCalibrationState)
            {
                return;
            }

            _debugLogTimer += Time.deltaTime;
            if (_debugLogTimer < _debugLogInterval)
            {
                return;
            }

            _debugLogTimer = 0f;

            float controllerDirectionDot = GetControllerDirectionDot();

            Debug.Log($"[LobbyAvatarCalibrationFlow] Waiting calibration. {reason} Stable: {_stableSeconds:F2}/{_requiredStableSeconds:F2}, ControllerDirectionDot: {controllerDirectionDot:F2}, HeadY: {headPosition.y:F2}");
        }

        private float GetControllerDirectionDot()
        {
            Vector3 leftDirection = GetControllerDirection(_leftHandTarget);
            Vector3 rightDirection = GetControllerDirection(_rightHandTarget);

            if (leftDirection.sqrMagnitude < 0.0001f || rightDirection.sqrMagnitude < 0.0001f)
            {
                return 1f;
            }

            return Vector3.Dot(leftDirection.normalized, rightDirection.normalized);
        }

        private bool HasRequiredReferences()
        {
            ResolveReferences();

            bool hasReferences =
                _guideUi != null &&
                _cameraOffset != null &&
                _cameraTransform != null &&
                _avatarEyeReference != null &&
                _leftHandTarget != null &&
                _rightHandTarget != null;

            if (!hasReferences && !_warnedMissingReferences)
            {
                Debug.LogWarning($"[LobbyAvatarCalibrationFlow] Calibration missing references: {BuildMissingReferenceList()}");
                _warnedMissingReferences = true;
            }

            return hasReferences;
        }

        private void ResolveReferences()
        {
            if (_guideUi == null)
            {
                _guideUi = GetComponent<CalibrationGuideWorldUi>();
            }

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform == null)
            {
                _cameraTransform = FindSceneTransformByName("Main Camera");
            }

            if (_cameraOffset == null && _cameraTransform != null)
            {
                _cameraOffset = _cameraTransform.parent;
            }

            if (_cameraOffset == null)
            {
                _cameraOffset = FindSceneTransformByName("Camera Offset");
            }

            if (_xrOriginRoot == null && _cameraOffset != null)
            {
                _xrOriginRoot = _cameraOffset.parent;
            }

            if (_xrOriginRoot == null)
            {
                _xrOriginRoot = FindSceneTransformByName("XR Origin (XR Rig)");
            }

            if (_locomotionRoot == null)
            {
                Transform locomotion = FindSceneTransformByName("Locomotion");
                _locomotionRoot = locomotion != null ? locomotion.gameObject : null;
            }

            PlayerAvatarLocomotionAnimator locomotionAnimator = FindFirstObjectByType<PlayerAvatarLocomotionAnimator>();
            if (_avatarRoot == null && locomotionAnimator != null)
            {
                _avatarRoot = locomotionAnimator.AvatarRoot;
            }

            if (_avatarRoot == null)
            {
                _avatarRoot = FindSceneTransformByName("Meshy_AI_Character_output");
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

            if (_leftHandTarget == null)
            {
                _leftHandTarget = FindSceneTransformByName("LeftHandTarget");
            }

            if (_rightHandTarget == null)
            {
                _rightHandTarget = FindSceneTransformByName("RightHandTarget");
            }

            if (_avatarRoot != null)
            {
                _avatarEyeReference ??= FindChildRecursive(_avatarRoot, "headfront");
                _avatarEyeReference ??= FindChildRecursive(_avatarRoot, "Head");
                _leftHandBone ??= FindChildRecursive(_avatarRoot, "LeftHand");
                _rightHandBone ??= FindChildRecursive(_avatarRoot, "RightHand");
            }
        }

        private Vector3 GetCameraAlignmentDelta()
        {
            if (_cameraTransform == null || _avatarEyeReference == null)
            {
                return Vector3.zero;
            }

            Vector3 delta = _avatarEyeReference.position - _cameraTransform.position;

            if (_maxCameraAlignmentDistance > 0f && delta.magnitude > _maxCameraAlignmentDistance)
            {
                delta = delta.normalized * _maxCameraAlignmentDistance;
            }

            delta.y = Mathf.Clamp(delta.y, -_maxHeightOffset, _maxHeightOffset);
            return delta;
        }

        private void BeginMovementLock()
        {
            if (!_lockMovementDuringCalibration)
            {
                return;
            }

            ResolveReferences();

            _hasMovementLock = true;
            _lockedHeadWorldPosition = _cameraTransform != null ? _cameraTransform.position : Vector3.zero;

            if (_locomotionRoot != null)
            {
                _locomotionRootWasActive = _locomotionRoot.activeSelf;
                _locomotionRoot.SetActive(false);
            }
        }

        private void EndMovementLock()
        {
            if (!_hasMovementLock)
            {
                return;
            }

            if (_locomotionRoot != null)
            {
                _locomotionRoot.SetActive(_locomotionRootWasActive);
            }

            _hasMovementLock = false;
        }

        private void UpdateMovementLock()
        {
            if (!_hasMovementLock || !_lockPhysicalHeadPosition || _cameraOffset == null || _cameraTransform == null)
            {
                return;
            }

            Vector3 positionDelta = _lockedHeadWorldPosition - _cameraTransform.position;
            positionDelta.y = 0f;

            if (positionDelta.sqrMagnitude < 0.000001f)
            {
                return;
            }

            _cameraOffset.position += positionDelta;
        }

        private string BuildMissingReferenceList()
        {
            string missing = string.Empty;

            missing = AppendMissingReference(missing, _guideUi == null, "Guide UI");
            missing = AppendMissingReference(missing, _cameraOffset == null, "Camera Offset");
            missing = AppendMissingReference(missing, _cameraTransform == null, "Main Camera");
            missing = AppendMissingReference(missing, _avatarEyeReference == null, "Avatar Eye Reference");
            missing = AppendMissingReference(missing, _leftHandTarget == null, "LeftHandTarget");
            missing = AppendMissingReference(missing, _rightHandTarget == null, "RightHandTarget");

            return string.IsNullOrEmpty(missing) ? "None" : missing;
        }

        private string AppendMissingReference(string current, bool isMissing, string referenceName)
        {
            if (!isMissing)
            {
                return current;
            }

            if (!string.IsNullOrEmpty(current))
            {
                current += ", ";
            }

            return current + referenceName;
        }

        private Transform FindSceneTransformByName(string targetName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject candidate = transforms[i].gameObject;

                if (!candidate.scene.IsValid())
                {
                    continue;
                }

                if (candidate.name == targetName)
                {
                    return transforms[i];
                }
            }

            return null;
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
