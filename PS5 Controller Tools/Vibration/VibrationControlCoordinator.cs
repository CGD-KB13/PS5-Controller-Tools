
namespace PS5_Controller_Tools.Vibration
{
    internal sealed class VibrationControlCoordinator : IDisposable
    {
        private readonly VibrationMotorOverlay _leftOverlay;
        private readonly VibrationMotorOverlay _rightOverlay;
        private readonly DualSenseVibrationService _vibrationService;
        private readonly Func<bool> _controllerConnectionProvider;
        private bool _isDisposed;

        public VibrationControlCoordinator(
            VibrationMotorOverlay leftOverlay,
            VibrationMotorOverlay rightOverlay,
            DualSenseVibrationService vibrationService,
            Func<bool> controllerConnectionProvider)
        {
            _leftOverlay = leftOverlay ?? throw new ArgumentNullException(nameof(leftOverlay));
            _rightOverlay = rightOverlay ?? throw new ArgumentNullException(nameof(rightOverlay));
            _vibrationService = vibrationService ?? throw new ArgumentNullException(nameof(vibrationService));
            _controllerConnectionProvider = controllerConnectionProvider ?? throw new ArgumentNullException(nameof(controllerConnectionProvider));

            _leftOverlay.MotorStateChanged += MotorOverlay_MotorStateChanged;
            _rightOverlay.MotorStateChanged += MotorOverlay_MotorStateChanged;
        }

        public void SynchronizeHardwareState()
        {
            ThrowIfDisposed();

            _vibrationService.ApplyCombined(
                _leftOverlay.GetState(),
                _rightOverlay.GetState(),
                _controllerConnectionProvider());
        }

        public void HandleControllerDisconnected()
        {
            if (_isDisposed)
                return;

            _vibrationService.HandleControllerDisconnected();
            ResetUi();
        }

        public void ResetUi()
        {
            ThrowIfDisposed();
            _leftOverlay.ResetToMinimum();
            _rightOverlay.ResetToMinimum();
        }

        private void MotorOverlay_MotorStateChanged(object? sender, VibrationMotorChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            _vibrationService.ApplyCombined(
                _leftOverlay.GetState(),
                _rightOverlay.GetState(),
                _controllerConnectionProvider());
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VibrationControlCoordinator));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _leftOverlay.MotorStateChanged -= MotorOverlay_MotorStateChanged;
            _rightOverlay.MotorStateChanged -= MotorOverlay_MotorStateChanged;
            _isDisposed = true;
        }
    }
}
