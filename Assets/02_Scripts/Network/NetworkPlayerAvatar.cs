using Fusion;
using FireLink119.Player;
using UnityEngine;

namespace FireLink119.Network
{
    public class NetworkPlayerAvatar : NetworkBehaviour
    {
        [SerializeField] private Transform _avatarRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerAvatarHandTargets _handTargets;
        [SerializeField] private bool _hideForInputAuthority = true;
        [SerializeField] private float _parameterDampTime = 0.1f;

        [Header("Animator Parameters")]
        [SerializeField] private string _isMovingParameter = "IsMoving";
        [SerializeField] private string _isSprintingParameter = "IsSprinting";
        [SerializeField] private string _moveXParameter = "MoveX";
        [SerializeField] private string _moveYParameter = "MoveY";

        [Networked] private Vector3 AvatarPosition { get; set; }
        [Networked] private Quaternion AvatarRotation { get; set; }
        [Networked] private Vector3 LeftHandLocalPosition { get; set; }
        [Networked] private Quaternion LeftHandLocalRotation { get; set; }
        [Networked] private Vector3 RightHandLocalPosition { get; set; }
        [Networked] private Quaternion RightHandLocalRotation { get; set; }
        [Networked] private Vector2 MoveBlend { get; set; }
        [Networked] private NetworkBool IsMoving { get; set; }
        [Networked] private NetworkBool IsSprinting { get; set; }

        private bool _hasIsMovingParameter;
        private bool _hasIsSprintingParameter;
        private bool _hasMoveXParameter;
        private bool _hasMoveYParameter;

        private void Awake()
        {
            if (_avatarRoot == null)
            {
                _avatarRoot = transform;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_handTargets == null)
            {
                _handTargets = GetComponent<PlayerAvatarHandTargets>();
            }

            if (_handTargets == null)
            {
                _handTargets = GetComponentInChildren<PlayerAvatarHandTargets>();
            }

            if (_handTargets == null)
            {
                // Network avatars only need named IK targets inside the prefab; the helper resolves them at runtime.
                _handTargets = gameObject.AddComponent<PlayerAvatarHandTargets>();
            }

            CacheAnimatorParameters();
        }

        public override void Spawned()
        {
            CacheAnimatorParameters();

            if (HasStateAuthority)
            {
                AvatarPosition = _avatarRoot.position;
                AvatarRotation = _avatarRoot.rotation;
            }

            ApplyInputAuthorityVisibility();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (!GetInput(out VrAvatarNetworkInput input))
            {
                return;
            }

            AvatarPosition = input.AvatarPosition;
            AvatarRotation = input.AvatarRotation;
            LeftHandLocalPosition = input.LeftHandLocalPosition;
            LeftHandLocalRotation = input.LeftHandLocalRotation;
            RightHandLocalPosition = input.RightHandLocalPosition;
            RightHandLocalRotation = input.RightHandLocalRotation;
            MoveBlend = input.MoveBlend;
            IsMoving = input.IsMoving;
            IsSprinting = input.IsSprinting;
        }

        public override void Render()
        {
            ApplyNetworkPose();
            ApplyHandTargets();
            ApplyAnimatorParameters();
        }

        private void ApplyNetworkPose()
        {
            if (_avatarRoot == null)
            {
                return;
            }

            _avatarRoot.SetPositionAndRotation(AvatarPosition, AvatarRotation);
        }

        private void ApplyHandTargets()
        {
            if (_handTargets == null)
            {
                return;
            }

            _handTargets.ApplyLocalHandTargets(
                LeftHandLocalPosition,
                LeftHandLocalRotation,
                RightHandLocalPosition,
                RightHandLocalRotation);
        }

        private void ApplyAnimatorParameters()
        {
            if (_animator == null)
            {
                return;
            }

            if (_hasIsMovingParameter)
            {
                _animator.SetBool(_isMovingParameter, IsMoving);
            }

            if (_hasIsSprintingParameter)
            {
                _animator.SetBool(_isSprintingParameter, IsSprinting);
            }

            if (_hasMoveXParameter)
            {
                _animator.SetFloat(_moveXParameter, MoveBlend.x, _parameterDampTime, Time.deltaTime);
            }

            if (_hasMoveYParameter)
            {
                _animator.SetFloat(_moveYParameter, MoveBlend.y, _parameterDampTime, Time.deltaTime);
            }
        }

        private void ApplyInputAuthorityVisibility()
        {
            if (!_hideForInputAuthority || !HasInputAuthority)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private void CacheAnimatorParameters()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return;
            }

            _hasIsMovingParameter = HasAnimatorParameter(_isMovingParameter, AnimatorControllerParameterType.Bool);
            _hasIsSprintingParameter = HasAnimatorParameter(_isSprintingParameter, AnimatorControllerParameterType.Bool);
            _hasMoveXParameter = HasAnimatorParameter(_moveXParameter, AnimatorControllerParameterType.Float);
            _hasMoveYParameter = HasAnimatorParameter(_moveYParameter, AnimatorControllerParameterType.Float);
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName && parameters[i].type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
