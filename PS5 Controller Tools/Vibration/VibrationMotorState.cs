
namespace PS5_Controller_Tools.Vibration
{
    public readonly struct VibrationMotorState
    {
        public VibrationMotorState(
            VibrationMotorSide motorSide,
            string logicalName,
            bool isEnabled,
            double intensityPercent)
        {
            MotorSide = motorSide;
            LogicalName = string.IsNullOrWhiteSpace(logicalName)
                ? VibrationMotorNames.GetDefault(motorSide)
                : logicalName.Trim();
            IsEnabled = isEnabled;
            IntensityPercent = Math.Clamp(intensityPercent, AppConstants.Vibration.OverlaySliderMinValue, AppConstants.Vibration.OverlaySliderMaxValue);
        }

        public VibrationMotorSide MotorSide { get; }
        public string LogicalName { get; }
        public bool IsEnabled { get; }
        public double IntensityPercent { get; }

        public double IntensityNormalized => IsEnabled
            ? IntensityPercent / AppConstants.Vibration.OverlaySliderMaxValue
            : 0.0;

        public byte HardwareValue => (byte)Math.Clamp(
            (int)Math.Round(IntensityNormalized * AppConstants.Vibration.HardwareMotorMaxValue),
            0,
            AppConstants.Vibration.HardwareMotorMaxValue);

        public static VibrationMotorState Disabled(VibrationMotorSide motorSide)
        {
            return new VibrationMotorState(
                motorSide,
                VibrationMotorNames.GetDefault(motorSide),
                isEnabled: false,
                intensityPercent: AppConstants.Vibration.OverlaySliderMinValue);
        }
    }
}
