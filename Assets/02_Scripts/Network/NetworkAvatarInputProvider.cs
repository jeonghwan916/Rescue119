using FireLink119.Player;
using Fusion;
using UnityEngine;

namespace FireLink119.Network
{
    public class NetworkAvatarInputProvider : NetworkRunnerCallbacksBehaviour
    {
        private PlayerAvatarLocomotionAnimator _cachedAnimator;
        private bool _warnedMissingLocalAvatar;

        public override void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (!TryGetLocalAvatar(out PlayerAvatarLocomotionAnimator animator))
            {
                return;
            }

            Transform avatarRoot = animator.AvatarRoot;
            VrAvatarNetworkInput avatarInput = new VrAvatarNetworkInput
            {
                AvatarPosition = avatarRoot.position,
                AvatarRotation = avatarRoot.rotation,
                MoveBlend = animator.CurrentMoveBlend,
                IsMoving = animator.IsMoving,
                IsSprinting = animator.IsSprinting
            };

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
    }
}
