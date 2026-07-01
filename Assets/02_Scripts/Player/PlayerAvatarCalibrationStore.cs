namespace FireLink119.Player
{
    public static class PlayerAvatarCalibrationStore
    {
        public static bool HasCalibration => _data.IsCalibrated;
        public static PlayerAvatarCalibrationData Data => _data;

        private static PlayerAvatarCalibrationData _data;

        public static void Save(PlayerAvatarCalibrationData data)
        {
            _data = data;
        }

        public static void Clear()
        {
            _data = default;
        }
    }
}
