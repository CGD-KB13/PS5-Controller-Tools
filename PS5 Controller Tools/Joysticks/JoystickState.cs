using System;

namespace PS5_Controller_Tools.Joysticks
{
    internal readonly struct JoystickState
    {
        public JoystickState(double x, double y)
        {
            X = Math.Clamp(x, -1.0, 1.0);
            Y = Math.Clamp(y, -1.0, 1.0);
        }

        public double X { get; }
        public double Y { get; }
    }
}
