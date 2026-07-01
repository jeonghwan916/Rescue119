using Fusion;
using UnityEngine;

namespace FireLink119.Network
{
    public struct VrAvatarNetworkInput : INetworkInput
    {
        public Vector3 AvatarPosition;
        public Quaternion AvatarRotation;
        public Vector2 MoveBlend;
        public NetworkBool IsMoving;
        public NetworkBool IsSprinting;
    }
}
