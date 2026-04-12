
namespace PS5_Controller_Tools.Vibration
{
    internal static class VibrationMotorNames
    {
        public const string ControllerVibratorL = "controllerVibratorL";
        public const string ControllerVibratorR = "controllerVibratorR";

        public static string GetDefault(VibrationMotorSide side)
        {
            return side == VibrationMotorSide.Left
                ? ControllerVibratorL
                : ControllerVibratorR;
        }

        public static VibrationMotorSide ResolveSide(string logicalName)
        {
            if (string.Equals(logicalName, ControllerVibratorR, StringComparison.Ordinal))
                return VibrationMotorSide.Right;

            return VibrationMotorSide.Left;
        }
    }
}
