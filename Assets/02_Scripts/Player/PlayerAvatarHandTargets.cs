using UnityEngine;

namespace FireLink119.Player
{
    public class PlayerAvatarHandTargets : MonoBehaviour
    {
        [SerializeField] private Transform _avatarRoot;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;

        public Transform AvatarRoot => _avatarRoot != null ? _avatarRoot : transform;
        public Transform LeftHandTarget => _leftHandTarget;
        public Transform RightHandTarget => _rightHandTarget;

        private void Awake()
        {
            ResolveMissingReferences();
        }

        private void OnValidate()
        {
            ResolveMissingReferences();
        }

        public bool TryGetLocalHandTargets(
            out Vector3 leftHandPosition,
            out Quaternion leftHandRotation,
            out Vector3 rightHandPosition,
            out Quaternion rightHandRotation)
        {
            ResolveMissingReferences();

            if (_avatarRoot == null || _leftHandTarget == null || _rightHandTarget == null)
            {
                leftHandPosition = Vector3.zero;
                leftHandRotation = Quaternion.identity;
                rightHandPosition = Vector3.zero;
                rightHandRotation = Quaternion.identity;
                return false;
            }

            leftHandPosition = _avatarRoot.InverseTransformPoint(_leftHandTarget.position);
            leftHandRotation = Quaternion.Inverse(_avatarRoot.rotation) * _leftHandTarget.rotation;
            rightHandPosition = _avatarRoot.InverseTransformPoint(_rightHandTarget.position);
            rightHandRotation = Quaternion.Inverse(_avatarRoot.rotation) * _rightHandTarget.rotation;
            return true;
        }

        public void ApplyLocalHandTargets(
            Vector3 leftHandPosition,
            Quaternion leftHandRotation,
            Vector3 rightHandPosition,
            Quaternion rightHandRotation)
        {
            ResolveMissingReferences();

            if (_avatarRoot == null || _leftHandTarget == null || _rightHandTarget == null)
            {
                return;
            }

            _leftHandTarget.SetPositionAndRotation(
                _avatarRoot.TransformPoint(leftHandPosition),
                _avatarRoot.rotation * leftHandRotation);

            _rightHandTarget.SetPositionAndRotation(
                _avatarRoot.TransformPoint(rightHandPosition),
                _avatarRoot.rotation * rightHandRotation);
        }

        private void ResolveMissingReferences()
        {
            if (_avatarRoot == null)
            {
                _avatarRoot = transform;
            }

            if (_leftHandTarget == null)
            {
                _leftHandTarget = FindRelatedTransform("LeftHandTarget");
            }

            if (_rightHandTarget == null)
            {
                _rightHandTarget = FindRelatedTransform("RightHandTarget");
            }
        }

        private Transform FindRelatedTransform(string targetName)
        {
            Transform localChild = FindChildRecursive(transform, targetName);

            if (localChild != null)
            {
                return localChild;
            }

            Transform root = transform.root;
            return root != null ? FindChildRecursive(root, targetName) : null;
        }

        private Transform FindChildRecursive(Transform root, string targetName)
        {
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
