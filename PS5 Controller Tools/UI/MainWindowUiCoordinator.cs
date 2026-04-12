using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using PS5_Controller_Tools.Buttons;
using PS5_Controller_Tools.Joysticks;
using JoystickOverlayManagerEx = PS5_Controller_Tools.Joysticks.JoystickOverlayManager;
using TouchPadOverlayManagerEx = PS5_Controller_Tools.TouchPad.TouchPadOverlayManager;
using PS5_Controller_Tools.Triggers;

namespace PS5_Controller_Tools
{
    internal sealed class MainWindowUiCoordinator
    {
        private readonly TextBlock _statusText;
        private readonly SpeakerAudioOverlay _speakerAudioOverlay;
        private readonly SpeakerWaveVisualizerOverlay _speakerVisualizer;
        private readonly ButtonOverlayManager _touchPadOverlayManager;
        private readonly TriggerOverlayManager _triggerOverlayManager;
        private readonly JoystickOverlayManagerEx _joystickOverlayManager;
        private readonly TouchPadOverlayManagerEx _touchPadContactOverlayManager;
        private readonly IReadOnlyList<OverlayButtonBinding> _overlayButtons;

        private int _controllerStatusVersion;

        public MainWindowUiCoordinator(
            TextBlock statusText,
            SpeakerAudioOverlay speakerAudioOverlay,
            SpeakerWaveVisualizerOverlay speakerVisualizer,
            ButtonOverlayManager touchPadOverlayManager,
            TriggerOverlayManager triggerOverlayManager,
            JoystickOverlayManagerEx joystickOverlayManager,
            TouchPadOverlayManagerEx touchPadContactOverlayManager,
            IReadOnlyList<OverlayButtonBinding> overlayButtons)
        {
            _statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
            _speakerAudioOverlay = speakerAudioOverlay ?? throw new ArgumentNullException(nameof(speakerAudioOverlay));
            _speakerVisualizer = speakerVisualizer ?? throw new ArgumentNullException(nameof(speakerVisualizer));
            _touchPadOverlayManager = touchPadOverlayManager ?? throw new ArgumentNullException(nameof(touchPadOverlayManager));
            _triggerOverlayManager = triggerOverlayManager ?? throw new ArgumentNullException(nameof(triggerOverlayManager));
            _joystickOverlayManager = joystickOverlayManager ?? throw new ArgumentNullException(nameof(joystickOverlayManager));
            _touchPadContactOverlayManager = touchPadContactOverlayManager ?? throw new ArgumentNullException(nameof(touchPadContactOverlayManager));
            _overlayButtons = overlayButtons ?? throw new ArgumentNullException(nameof(overlayButtons));
        }

        public void ApplyControllerStatus(ControllerStatusChangedEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            Brush color = e.Status switch
            {
                ControllerRuntimeStatus.Connected => Brushes.Green,
                ControllerRuntimeStatus.Disconnected => Brushes.Orange,
                _ => Brushes.OrangeRed
            };

            int statusVersion = ++_controllerStatusVersion;

            if (e.Status == ControllerRuntimeStatus.Disconnected)
            {
                _ = HandleControllerDisconnectedAsync(statusVersion);
                return;
            }

            SetStatus(e.Message, color);
        }

        private async Task HandleControllerDisconnectedAsync(int statusVersion)
        {
            SetStatus(UiMessageCatalog.Controller.Disconnected, Brushes.Orange);

            await Task.Delay(2000);

            if (statusVersion != _controllerStatusVersion)
                return;

            SetStatus(UiMessageCatalog.Controller.NotDetected, Brushes.Red);
        }

        public void ApplyControllerSnapshot(ControllerStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            foreach (OverlayButtonBinding binding in _overlayButtons)
            {
                bool isPressed = snapshot.PressedButtons.Contains(binding.SdlButton);
                binding.Overlay.UpdatePressedState(isPressed);
            }

            _touchPadOverlayManager.Update(snapshot.IsTouchPadPressed);

            _triggerOverlayManager.Update(
                TriggerInputReader.ReadLeft(snapshot),
                TriggerInputReader.ReadRight(snapshot));

            _joystickOverlayManager.Update(
                new JoystickState(snapshot.LeftStickX, snapshot.LeftStickY),
                new JoystickState(snapshot.RightStickX, snapshot.RightStickY));

            _touchPadContactOverlayManager.Update(snapshot.ControllerHandle);
        }

        public void ApplyControllerDisconnected()
        {
            ResetControllerVisuals();
            _speakerVisualizer.Reset();
        }

        public void ApplyAudioStatus(DualSenseAudioStatusChangedEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            _speakerVisualizer.SetPlaybackState(e.State, e.Mode);

            Brush audioBrush = e.State switch
            {
                DualSenseAudioState.Playing => Brushes.Green,
                DualSenseAudioState.Paused => Brushes.DarkOrange,
                DualSenseAudioState.Stopped => Brushes.Red,
                _ => Brushes.DimGray
            };

            if (string.IsNullOrWhiteSpace(e.Message))
                _speakerAudioOverlay.ClearAudioStatusMessage();
            else
                _speakerAudioOverlay.SetAudioStatusMessage(e.Message, audioBrush);

            switch (e.State)
            {
                case DualSenseAudioState.Playing when e.Mode == AudioPlaybackMode.Wave:
                    _speakerAudioOverlay.SetPlaybackTrackName(e.TrackName);
                    _speakerAudioOverlay.SetPlaybackState(isPlaying: true, isPaused: false);
                    break;

                case DualSenseAudioState.Paused when e.Mode == AudioPlaybackMode.Wave:
                    _speakerAudioOverlay.SetPlaybackTrackName(e.TrackName);
                    _speakerAudioOverlay.SetPlaybackState(isPlaying: false, isPaused: true);
                    break;

                case DualSenseAudioState.Stopped when e.Mode == AudioPlaybackMode.None:
                    _speakerAudioOverlay.SetPlaybackState(isPlaying: false, isPaused: false);
                    _speakerAudioOverlay.ResetPlaybackProgress();
                    _speakerVisualizer.Reset();
                    break;
            }
        }

        public string GetAudioStatusMessage()
        {
            return _speakerAudioOverlay.CurrentAudioStatusMessage;
        }

        public void UpdateAudioLevel(float level)
        {
            _speakerVisualizer.UpdateLevel(level);
        }

        public void UpdatePlaybackProgress(TimeSpan position, TimeSpan duration)
        {
            _speakerAudioOverlay.UpdatePlaybackProgress(position, duration);
        }
        public void UpdateAudioFrame(AudioVisualizerFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            _speakerVisualizer.UpdateFrame(frame);
        }

        public void ResetControllerVisuals()
        {
            foreach (OverlayButtonBinding binding in _overlayButtons)
                binding.Overlay.Reset();

            _touchPadOverlayManager.Reset();
            _triggerOverlayManager.Reset();
            _joystickOverlayManager.Reset();
            _touchPadContactOverlayManager.Reset();
        }

        public void SetStatus(string message, Brush brush)
        {
            _statusText.Text = message;
            _statusText.Foreground = brush;
        }
    }
}
