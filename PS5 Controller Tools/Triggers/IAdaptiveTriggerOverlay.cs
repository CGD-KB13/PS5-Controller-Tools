
namespace PS5_Controller_Tools.Triggers
{
    public sealed class AdaptiveTriggerStateChangedEventArgs : EventArgs
    {
        public AdaptiveTriggerStateChangedEventArgs(AdaptiveTriggerState state)
        {
            State = state;
        }

        public AdaptiveTriggerState State { get; }
    }

    internal interface IAdaptiveTriggerOverlay
    {
        event EventHandler<AdaptiveTriggerStateChangedEventArgs>? TriggerStateChanged;

        AdaptiveTriggerState GetState();
        void ResetToOff();
    }
}
