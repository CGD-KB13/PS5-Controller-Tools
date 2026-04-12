
namespace PS5_Controller_Tools.Vibration
{
    internal sealed class DualSenseVibrationService : IDisposable
    {
        private readonly DualSenseHidVibrationControl _hidVibrationControl = new();

        private VibrationMotorState _leftMotorState = VibrationMotorState.Disabled(VibrationMotorSide.Left);
        private VibrationMotorState _rightMotorState = VibrationMotorState.Disabled(VibrationMotorSide.Right);
        private byte? _lastAppliedLeftMotor;
        private byte? _lastAppliedRightMotor;
        private string? _lastWriteError;
        private bool _isDisposed;

        public void ApplyCombined(
            VibrationMotorState leftMotorState,
            VibrationMotorState rightMotorState,
            bool controllerConnected)
        {
            ThrowIfDisposed();

            _leftMotorState = leftMotorState;
            _rightMotorState = rightMotorState;
            ApplyHardwareState(controllerConnected);
        }

        public void SynchronizeCurrentState(bool controllerConnected)
        {
            ThrowIfDisposed();
            ApplyHardwareState(controllerConnected);
        }

        public void HandleControllerDisconnected()
        {
            if (_isDisposed)
                return;

            _leftMotorState = VibrationMotorState.Disabled(VibrationMotorSide.Left);
            _rightMotorState = VibrationMotorState.Disabled(VibrationMotorSide.Right);
            StopAndReleaseSession();
        }

        public void Shutdown(bool controllerConnected)
        {
            if (_isDisposed)
                return;

            _leftMotorState = VibrationMotorState.Disabled(VibrationMotorSide.Left);
            _rightMotorState = VibrationMotorState.Disabled(VibrationMotorSide.Right);

            if (controllerConnected || _hidVibrationControl.IsConnected)
                StopAndReleaseSession();
            else
                DisposeHidSession();
        }

        private void ApplyHardwareState(bool controllerConnected)
        {
            byte leftValue = _leftMotorState.HardwareValue;
            byte rightValue = _rightMotorState.HardwareValue;

            if (!controllerConnected)
            {
                if (_hidVibrationControl.IsConnected)
                    StopAndReleaseSession();

                return;
            }

            if (_lastAppliedLeftMotor == leftValue &&
                _lastAppliedRightMotor == rightValue &&
                _hidVibrationControl.IsConnected)
            {
                return;
            }

            if (_hidVibrationControl.TrySetMotorStrength(leftValue, rightValue, out string error))
            {
                _lastAppliedLeftMotor = leftValue;
                _lastAppliedRightMotor = rightValue;
                _lastWriteError = null;
                return;
            }

            if (!string.Equals(_lastWriteError, error, StringComparison.Ordinal))
            {
                AppLogger.Warn(nameof(DualSenseVibrationService), error);
                _lastWriteError = error;
            }
        }

        private void StopAndReleaseSession()
        {
            if (_hidVibrationControl.IsConnected && !_hidVibrationControl.TryStop(out string error))
            {
                if (!string.Equals(_lastWriteError, error, StringComparison.Ordinal))
                {
                    AppLogger.Warn(nameof(DualSenseVibrationService), error);
                    _lastWriteError = error;
                }
            }

            DisposeHidSession();
        }

        private void DisposeHidSession()
        {
            _hidVibrationControl.Dispose();
            _lastAppliedLeftMotor = null;
            _lastAppliedRightMotor = null;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DualSenseVibrationService));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Shutdown(controllerConnected: false);
            _isDisposed = true;
        }
    }
}
