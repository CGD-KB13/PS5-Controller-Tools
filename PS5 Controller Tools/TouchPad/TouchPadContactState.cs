using System;

namespace PS5_Controller_Tools.TouchPad
{
    internal readonly struct TouchPadContactState
    {
        public static readonly TouchPadContactState Inactive = new(false, 0.0, 0.0, 0.0, -1, -1);

        public TouchPadContactState(bool isActive, double x, double y, double pressure, int touchPadIndex, int fingerIndex)
        {
            IsActive = isActive;
            X = Math.Clamp(x, 0.0, 1.0);
            Y = Math.Clamp(y, 0.0, 1.0);
            Pressure = Math.Clamp(pressure, 0.0, 1.0);
            TouchPadIndex = touchPadIndex;
            FingerIndex = fingerIndex;
        }

        public bool IsActive { get; }
        public double X { get; }
        public double Y { get; }
        public double Pressure { get; }
        public int TouchPadIndex { get; }
        public int FingerIndex { get; }
    }
}
