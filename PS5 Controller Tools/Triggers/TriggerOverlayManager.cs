
namespace PS5_Controller_Tools.Triggers
{
    internal sealed class TriggerOverlayManager
    {
        private readonly TriggerPressureOverlay _leftOverlay;
        private readonly TriggerPressureOverlay _rightOverlay;

        public TriggerOverlayManager(TriggerPressureOverlay leftOverlay, TriggerPressureOverlay rightOverlay)
        {
            _leftOverlay = leftOverlay ?? throw new ArgumentNullException(nameof(leftOverlay));
            _rightOverlay = rightOverlay ?? throw new ArgumentNullException(nameof(rightOverlay));
        }

        public void Update(TriggerState leftState, TriggerState rightState)
        {
            _leftOverlay.SetPressure(leftState.Pressure);
            _rightOverlay.SetPressure(rightState.Pressure);
        }

        public void Reset()
        {
            _leftOverlay.Reset();
            _rightOverlay.Reset();
        }
    }
}
