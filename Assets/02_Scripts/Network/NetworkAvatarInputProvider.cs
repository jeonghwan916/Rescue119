using FireLink119.Player;
using Fusion;
using UnityEngine;

namespace FireLink119.Network
{
    public class NetworkAvatarInputProvider : NetworkRunnerCallbacksBehaviour
    {
        private PlayerAvatarLocomotionAnimator _cachedAnimator;
        private PlayerAvatarHandTargets _cachedHandTargets;
        private bool _warnedMissingLocalAvatar;
        private bool _warnedMissingHandTargets;

        public override void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (!TryGetLocalAvatar(out PlayerAvatarLocomotionAnimator animator))
            {
                return;
            }

            Transform avatarRoot = animator.AvatarRoot;
            TryGetLocalHandTargets(animator, out PlayerAvatarHandTargets handTargets);

            VrAvatarNetworkInput avatarInput = new VrAvatarNetworkInput
            {
                AvatarPosition = avatarRoot.position,
                AvatarRotation = avatarRoot.rotation,
                MoveBlend = animator.CurrentMoveBlend,
                IsMoving = animator.IsMoving,
                IsSprinting = animator.IsSprinting
            };

            if (handTargets != null && handTargets.TryGetLocalHandTargets(
                out Vector3 leftHandPosition,
                out Quaternion leftHandRotation,
                out Vector3 rightHandPosition,
                out Quaternion rightHandRotation))
            {
                avatarInput.LeftHandLocalPosition = leftHandPosition;
                avatarInput.LeftHandLocalRotation = leftHandRotation;
                avatarInput.RightHandLocalPosition = rightHandPosition;
                avatarInput.RightHandLocalRotation = rightHandRotation;
            }

            input.Set(avatarInput);
        }

        private bool TryGetLocalAvatar(out PlayerAvatarLocomotionAnimator animator)
        {
            if (_cachedAnimator != null && _cachedAnimator.isActiveAndEnabled)
            {
                animator = _cachedAnimator;
                return true;
            }

            _cachedAnimator = FindFirstObjectByType<PlayerAvatarLocomotionAnimator>();
            if (_cachedAnimator != null)
            {
                _warnedMissingLocalAvatar = false;
                animator = _cachedAnimator;
                return true;
            }

            if (!_warnedMissingLocalAvatar)
            {
                Debug.LogWarning("[NetworkAvatarInputProvider] Local PlayerAvatarLocomotionAnimator was not found in the loaded scene.");
                _warnedMissingLocalAvatar = true;
            }

            animator = null;
            return false;
        }

        private bool TryGetLocalHandTargets(PlayerAvatarLocomotionAnimator animator, out PlayerAvatarHandTargets handTargets)
        {
            if (_cachedHandTargets != null && _cachedHandTargets.isActiveAndEnabled)
            {
                handTargets = _cachedHandTargets;
                return true;
            }

            _cachedHandTargets = animator.GetComponent<PlayerAvatarHandTargets>();

            if (_cachedHandTargets == null)
            {
                _cachedHandTargets = animator.GetComponentInChildren<PlayerAvatarHandTargets>();
            }

            if (_cachedHandTargets == null)
            {
                _cachedHandTargets = animator.GetComponentInParent<PlayerAvatarHandTargets>();
            }

            if (_cachedHandTargets == null)
            {
                // Keep scene setup simple: if the local avatar has correctly named targets, this runtime helper can resolve them.
                _cachedHandTargets = animator.gameObject.AddComponent<PlayerAvatarHandTargets>();
            }

            if (_cachedHandTargets != null)
            {
                _warnedMissingHandTargets = false;
                handTargets = _cachedHandTargets;
                return true;
            }

            if (!_warnedMissingHandTargets)
            {
                Debug.LogWarning("[NetworkAvatarInputProvider] Local PlayerAvatarHandTargets was not found. Hand IK targets will not be sent.");
                _warnedMissingHandTargets = true;
            }

            handTargets = null;
            return false;
        }
    }
}
