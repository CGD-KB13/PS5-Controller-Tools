
namespace PS5_Controller_Tools.Triggers
{
    internal sealed class DualSenseAdaptiveTriggerService : IDisposable
    {
        private readonly DualSenseHidTriggerControl _hidTriggerControl = new();

        private AdaptiveTriggerState _leftState = AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Left);
        private AdaptiveTriggerState _rightState = AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Right);
        private byte[]? _lastAppliedLeftEffect;
        private byte[]? _lastAppliedRightEffect;
        private string? _lastWriteError;
        private bool _isDisposed;

        public void ApplyCombined(
            AdaptiveTriggerState leftState,
            AdaptiveTriggerState rightState,
            bool controllerConnected)
        {
            ThrowIfDisposed();

            _leftState = leftState;
            _rightState = rightState;
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

            _leftState = AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Left);
            _rightState = AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Right);
            ResetAndReleaseSession();
        }

        public void Shutdown(bool controllerConnected)
        {
            if (_isDisposed)
                return;

            _leftState = AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Left);
            _rightState = AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Right);

            if (controllerConnected || _hidTriggerControl.IsConnected)
                ResetAndReleaseSession();
            else
                DisposeHidSession();
        }

        private void ApplyHardwareState(bool controllerConnected)
        {
            byte[] leftEffect = AdaptiveTriggerEffectBuilder.BuildEffectBytes(_leftState);
            byte[] rightEffect = AdaptiveTriggerEffectBuilder.BuildEffectBytes(_rightState);

            if (!controllerConnected)
            {
                if (_hidTriggerControl.IsConnected)
                    ResetAndReleaseSession();

                return;
            }

            if (_hidTriggerControl.IsConnected &&
                ByteArraysEqual(_lastAppliedLeftEffect, leftEffect) &&
                ByteArraysEqual(_lastAppliedRightEffect, rightEffect))
            {
                return;
            }

            if (_hidTriggerControl.TrySetEffects(leftEffect, rightEffect, out string error))
            {
                _lastAppliedLeftEffect = leftEffect;
                _lastAppliedRightEffect = rightEffect;
                _lastWriteError = null;
                return;
            }

            if (!string.Equals(_lastWriteError, error, StringComparison.Ordinal))
            {
                AppLogger.Warn(nameof(DualSenseAdaptiveTriggerService), error);
                _lastWriteError = error;
            }
        }

        private void ResetAndReleaseSession()
        {
            if (_hidTriggerControl.IsConnected && !_hidTriggerControl.TryReset(out string error))
            {
                if (!string.Equals(_lastWriteError, error, StringComparison.Ordinal))
                {
                    AppLogger.Warn(nameof(DualSenseAdaptiveTriggerService), error);
                    _lastWriteError = error;
                }
            }

            DisposeHidSession();
        }

        private void DisposeHidSession()
        {
            _hidTriggerControl.Dispose();
            _lastAppliedLeftEffect = null;
            _lastAppliedRightEffect = null;
        }

        private static bool ByteArraysEqual(byte[]? first, byte[]? second)
        {
            if (ReferenceEquals(first, second))
                return true;
            if (first == null || second == null)
                return false;
            return first.SequenceEqual(second);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DualSenseAdaptiveTriggerService));
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
