
namespace PS5_Controller_Tools.Vibration
{
    public sealed class VibrationMotorChangedEventArgs : EventArgs
    {
        public VibrationMotorChangedEventArgs(VibrationMotorState state)
        {
            State = state;
        }

        public VibrationMotorState State { get; }
    }
}
