
namespace PS5_Controller_Tools.Triggers
{
    internal sealed class AdaptiveTriggerControlCoordinator : IDisposable
    {
        private readonly IAdaptiveTriggerOverlay _leftOverlay;
        private readonly IAdaptiveTriggerOverlay _rightOverlay;
        private readonly DualSenseAdaptiveTriggerService _adaptiveTriggerService;
        private readonly Func<bool> _controllerConnectionProvider;
        private bool _isDisposed;

        public AdaptiveTriggerControlCoordinator(
            IAdaptiveTriggerOverlay leftOverlay,
            IAdaptiveTriggerOverlay rightOverlay,
            DualSenseAdaptiveTriggerService adaptiveTriggerService,
            Func<bool> controllerConnectionProvider)
        {
            _leftOverlay = leftOverlay ?? throw new ArgumentNullException(nameof(leftOverlay));
            _rightOverlay = rightOverlay ?? throw new ArgumentNullException(nameof(rightOverlay));
            _adaptiveTriggerService = adaptiveTriggerService ?? throw new ArgumentNullException(nameof(adaptiveTriggerService));
            _controllerConnectionProvider = controllerConnectionProvider ?? throw new ArgumentNullException(nameof(controllerConnectionProvider));

            _leftOverlay.TriggerStateChanged += TriggerOverlay_TriggerStateChanged;
            _rightOverlay.TriggerStateChanged += TriggerOverlay_TriggerStateChanged;
        }

        public void SynchronizeHardwareState()
        {
            ThrowIfDisposed();

            _adaptiveTriggerService.ApplyCombined(
                _leftOverlay.GetState(),
                _rightOverlay.GetState(),
                _controllerConnectionProvider());
        }

        public void HandleControllerDisconnected()
        {
            if (_isDisposed)
                return;

            _adaptiveTriggerService.HandleControllerDisconnected();
            ResetUi();
        }

        public void ResetUi()
        {
            ThrowIfDisposed();
            _leftOverlay.ResetToOff();
            _rightOverlay.ResetToOff();
        }

        private void TriggerOverlay_TriggerStateChanged(object? sender, AdaptiveTriggerStateChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            _adaptiveTriggerService.ApplyCombined(
                _leftOverlay.GetState(),
                _rightOverlay.GetState(),
                _controllerConnectionProvider());
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AdaptiveTriggerControlCoordinator));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _leftOverlay.TriggerStateChanged -= TriggerOverlay_TriggerStateChanged;
            _rightOverlay.TriggerStateChanged -= TriggerOverlay_TriggerStateChanged;
            _isDisposed = true;
        }
    }
}
