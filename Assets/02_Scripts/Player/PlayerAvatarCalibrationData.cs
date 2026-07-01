using System;
using UnityEngine;

namespace FireLink119.Player
{
    [Serializable]
    public struct PlayerAvatarCalibrationData
    {
        public bool IsCalibrated;
        public float MeasuredEyeHeight;
        public float TargetEyeHeight;
        public float HeightOffset;
        public Vector3 CameraOffsetWorldDelta;
        public Vector3 LeftHandTargetLocalPosition;
        public Quaternion LeftHandTargetLocalRotation;
        public Vector3 RightHandTargetLocalPosition;
        public Quaternion RightHandTargetLocalRotation;

        public static PlayerAvatarCalibrationData CreateDefault(float targetEyeHeight)
        {
            return new PlayerAvatarCalibrationData
            {
                IsCalibrated = false,
                MeasuredEyeHeight = 0f,
                TargetEyeHeight = targetEyeHeight,
                HeightOffset = 0f,
                CameraOffsetWorldDelta = Vector3.zero,
                LeftHandTargetLocalPosition = Vector3.zero,
                LeftHandTargetLocalRotation = Quaternion.identity,
                RightHandTargetLocalPosition = Vector3.zero,
                RightHandTargetLocalRotation = Quaternion.identity
            };
        }
    }
}
