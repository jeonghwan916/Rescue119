using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FireLink119.UI
{
    [DisallowMultipleComponent]
    public class CalibrationGuideWorldUi : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _distanceFromCamera = 1.4f;
        [SerializeField] private Vector3 _worldOffset = Vector3.zero;
        [SerializeField] private bool _useCameraYawOnly = true;
        [SerializeField] private int _placementStabilizationFrames = 8;
        [SerializeField] private float _pitchPlacementThreshold = 0.45f;
        [SerializeField] private bool _attachToCameraWhileVisible = true;

        [Header("Visual")]
        [SerializeField] private Vector2 _canvasSize = new Vector2(1.8f, 1f);
        [SerializeField] private Color _backgroundColor = Color.black;
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private float _fontSize = 0.08f;
        [SerializeField] private string _guideText = "정면을 바라보고\n양팔을 양옆으로 뻗어 주세요.";
        [SerializeField] private bool _showOnStart = true;

        [Header("Screen Mask")]
        [SerializeField] private bool _maskSceneWhileVisible = true;
        [SerializeField] private string _uiLayerName = "CalibrationUI";

        private Canvas _canvas;
        private TextMeshProUGUI _text;
        private Camera _camera;
        private CameraClearFlags _originalClearFlags;
        private Color _originalBackgroundColor;
        private int _originalCullingMask;
        private bool _hasOriginalCameraSettings;
        private int _remainingPlacementFrames;
        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;
        private Vector3 _originalLocalScale;
        private bool _hasOriginalTransform;
        private bool _isAttachedToCamera;

        private void Awake()
        {
            ResolveCamera();
            EnsureUi();
        }

        private void Start()
        {
            SetVisible(_showOnStart);
        }

        private void LateUpdate()
        {
            if (_remainingPlacementFrames <= 0)
            {
                return;
            }

            PlaceOnceInFrontOfCamera();
            _remainingPlacementFrames--;
        }

        public void SetVisible(bool isVisible)
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(isVisible);
            }

            if (isVisible)
            {
                BeginVisiblePlacement();
                ApplyCameraMask();
            }
            else
            {
                _remainingPlacementFrames = 0;
                RestoreOriginalParent();
                RestoreCameraMask();
            }
        }

        public void SetGuideText(string guideText)
        {
            _guideText = guideText;

            if (_text != null)
            {
                _text.text = _guideText;
            }
        }

        private void PlaceOnceInFrontOfCamera()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            Vector3 viewForward = _cameraTransform.forward;
            Vector3 placementForward = viewForward;
            Vector3 yawForward = Vector3.ProjectOnPlane(viewForward, Vector3.up);
            Vector3 up = Vector3.up;

            if (yawForward.sqrMagnitude < 0.0001f)
            {
                yawForward = Vector3.ProjectOnPlane(_cameraTransform.parent != null ? _cameraTransform.parent.forward : Vector3.forward, Vector3.up);
            }

            if (yawForward.sqrMagnitude < 0.0001f)
            {
                yawForward = Vector3.forward;
            }

            yawForward.Normalize();

            // 보정 UI는 기본적으로 정면 수평 위치에 두되, 사용자가 위/아래를 크게 보고 시작한 경우에는 시야 안에 들어오도록 실제 시선 방향을 사용한다.
            if (_useCameraYawOnly && Mathf.Abs(Vector3.Dot(viewForward.normalized, Vector3.up)) < _pitchPlacementThreshold)
            {
                placementForward = yawForward;
            }

            if (placementForward.sqrMagnitude < 0.0001f)
            {
                placementForward = yawForward;
            }

            placementForward.Normalize();

            transform.position = _cameraTransform.position + placementForward * _distanceFromCamera + _worldOffset;
            transform.rotation = Quaternion.LookRotation(yawForward, up);
        }

        private void ResolveCamera()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform == null)
            {
                _cameraTransform = FindSceneTransformByName("Main Camera");
            }

            if (_camera == null && _cameraTransform != null)
            {
                _camera = _cameraTransform.GetComponent<Camera>();
            }

            if (_canvas != null && _camera != null)
            {
                _canvas.worldCamera = _camera;
            }
        }

        private void BeginPlacementStabilization()
        {
            ResolveCamera();
            _remainingPlacementFrames = Mathf.Max(1, _placementStabilizationFrames);
            PlaceOnceInFrontOfCamera();
        }

        private void BeginVisiblePlacement()
        {
            ResolveCamera();

            if (_attachToCameraWhileVisible && _cameraTransform != null)
            {
                AttachToCamera();
                return;
            }

            BeginPlacementStabilization();
        }

        private void AttachToCamera()
        {
            CacheOriginalTransform();

            transform.SetParent(_cameraTransform, false);
            transform.localPosition = new Vector3(_worldOffset.x, _worldOffset.y, _distanceFromCamera + _worldOffset.z);
            transform.localRotation = Quaternion.identity;
            transform.localScale = _originalLocalScale;

            _remainingPlacementFrames = 0;
            _isAttachedToCamera = true;
        }

        private void CacheOriginalTransform()
        {
            if (_hasOriginalTransform)
            {
                return;
            }

            _originalParent = transform.parent;
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;
            _originalLocalScale = transform.localScale;
            _hasOriginalTransform = true;
        }

        private void RestoreOriginalParent()
        {
            if (!_isAttachedToCamera || !_hasOriginalTransform)
            {
                return;
            }

            transform.SetParent(_originalParent, false);
            transform.localPosition = _originalLocalPosition;
            transform.localRotation = _originalLocalRotation;
            transform.localScale = _originalLocalScale;
            _isAttachedToCamera = false;
        }

        private void EnsureUi()
        {
            if (_canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Calibration Guide Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = _canvasSize;

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
            _canvas.sortingOrder = 100;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 100f;

            GameObject backgroundObject = new GameObject("Black Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(canvasObject.transform, false);

            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            StretchToParent(backgroundRect, Vector2.zero);

            Image background = backgroundObject.GetComponent<Image>();
            background.color = _backgroundColor;
            background.raycastTarget = true;

            GameObject textObject = new GameObject("Guide Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            StretchToParent(textRect, new Vector2(0.12f, 0.12f));

            _text = textObject.GetComponent<TextMeshProUGUI>();
            _text.text = _guideText;
            _text.color = _textColor;
            _text.fontSize = _fontSize;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.raycastTarget = false;

            if (_font != null)
            {
                _text.font = _font;
            }

            AssignLayerRecursive(canvasObject, LayerMask.NameToLayer(_uiLayerName));
        }

        private void ApplyCameraMask()
        {
            if (!_maskSceneWhileVisible)
            {
                return;
            }

            ResolveCamera();

            if (_camera == null)
            {
                return;
            }

            int uiLayer = LayerMask.NameToLayer(_uiLayerName);
            if (uiLayer < 0)
            {
                Debug.LogWarning($"[CalibrationGuideWorldUi] Layer not found: {_uiLayerName}");
                return;
            }

            if (!_hasOriginalCameraSettings)
            {
                _originalClearFlags = _camera.clearFlags;
                _originalBackgroundColor = _camera.backgroundColor;
                _originalCullingMask = _camera.cullingMask;
                _hasOriginalCameraSettings = true;
            }

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.cullingMask = 1 << uiLayer;
        }

        private void RestoreCameraMask()
        {
            if (!_hasOriginalCameraSettings || _camera == null)
            {
                return;
            }

            _camera.clearFlags = _originalClearFlags;
            _camera.backgroundColor = _originalBackgroundColor;
            _camera.cullingMask = _originalCullingMask;
            _hasOriginalCameraSettings = false;
        }

        private void OnDisable()
        {
            RestoreOriginalParent();
            RestoreCameraMask();
        }

        private void OnDestroy()
        {
            RestoreOriginalParent();
            RestoreCameraMask();
        }

        private void AssignLayerRecursive(GameObject target, int layer)
        {
            if (target == null || layer < 0)
            {
                return;
            }

            target.layer = layer;

            foreach (Transform child in target.transform)
            {
                AssignLayerRecursive(child.gameObject, layer);
            }
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

        private void StretchToParent(RectTransform rectTransform, Vector2 padding)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = padding;
            rectTransform.offsetMax = -padding;
        }
    }
}
