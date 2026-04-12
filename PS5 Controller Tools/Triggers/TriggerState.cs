
namespace PS5_Controller_Tools.Triggers
{
    internal readonly struct TriggerState
    {
        public TriggerState(double pressure)
        {
            Pressure = Math.Clamp(pressure, 0.0, 1.0);
        }

        public double Pressure { get; }
    }
}
