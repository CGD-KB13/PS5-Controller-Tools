using SDL2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PS5_Controller_Tools.Audio;
using PS5_Controller_Tools.Buttons;
using JoystickOverlayManagerEx = PS5_Controller_Tools.Joysticks.JoystickOverlayManager;
using TouchPadOverlayManagerEx = PS5_Controller_Tools.TouchPad.TouchPadOverlayManager;
using PS5_Controller_Tools.Triggers;
using PS5_Controller_Tools.Vibration;
using PS5_Controller_Tools.Lighting;
using PS5_Controller_Tools.About;

namespace PS5_Controller_Tools
{
    public partial class MainWindow : Window
    {
        private readonly List<OverlayButtonBinding> _overlayButtons = new();
        private readonly DualSenseAudioService _audioService = new();
        private readonly DualSenseVibrationService _vibrationService = new();
        private readonly DualSenseAdaptiveTriggerService _adaptiveTriggerService = new();
        private readonly DualSenseBatteryReader _batteryReader = new();
        private readonly DispatcherTimer _batteryRefreshTimer;
        private readonly DualSenseMicrophoneLevelMonitor _microphoneLevelMonitor = new();
        private readonly DispatcherTimer _microphoneUiTimer;
        private float _pendingMicrophoneLevel;
        private float _displayedMicrophoneLevel;
        private bool _isMicrophoneMuted;
        private AudioPlaybackController? _audioPlaybackController;
        private DebugMonitorWindow? _debugWindow;

        private DualSenseControllerRuntime? _controllerRuntime;
        private JoystickOverlayManagerEx? _joystickOverlayManager;
        private TouchPadOverlayManagerEx? _touchPadOverlayManager;
        private MainWindowUiCoordinator? _uiCoordinator;
        private DualSenseSnifferWindow? _snifferWindow;
        private VibrationControlCoordinator? _vibrationControlCoordinator;
        private AdaptiveTriggerControlCoordinator? _adaptiveTriggerControlCoordinator;
        private readonly DualSenseCommunicationSniffer _communicationSniffer = new();
        private byte _lastPlayerLedMask = 0xFF;
        private bool _isClosing;
        private bool _startupSoundPending = true;
        private DateTime _startupSoundDeadlineUtc = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            _batteryRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AppConstants.Controller.BatteryRefreshIntervalSeconds)
            };
            _batteryRefreshTimer.Tick += BatteryRefreshTimer_Tick;

            _microphoneUiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _microphoneUiTimer.Tick += MicrophoneUiTimer_Tick;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AppLogger.Info(nameof(MainWindow), "Chargement de la fenêtre principale.");

            _audioPlaybackController = new AudioPlaybackController(_audioService);

            _joystickOverlayManager = new JoystickOverlayManagerEx(
                LeftStickCanvas, LeftStickDot, LeftStickXText, LeftStickYText,
                RightStickCanvas, RightStickDot, RightStickXText, RightStickYText);

            _touchPadOverlayManager = new TouchPadOverlayManagerEx(TouchPadCanvas, TouchPadDot);

            ConfigureAnimatedButtons();

            _uiCoordinator = new MainWindowUiCoordinator(
                StatusText,
                SpeakerAudioOverlayControl,
                SpeakerVisualizer,
                new ButtonOverlayManager(BtnTouchPadOverlay),
                new TriggerOverlayManager(BtnL2Overlay, BtnR2Overlay),
                _joystickOverlayManager,
                _touchPadOverlayManager,
                _overlayButtons);

            _audioService.StatusChanged += AudioService_StatusChanged;
            _audioService.AudioLevelChanged += AudioService_AudioLevelChanged;
            _audioService.PlaybackProgressChanged += AudioService_PlaybackProgressChanged;

            _communicationSniffer.HidLineCaptured += CommunicationSniffer_HidLineCaptured;
            _communicationSniffer.SdlLineCaptured += CommunicationSniffer_SdlLineCaptured;
            _communicationSniffer.StatusChanged += CommunicationSniffer_StatusChanged;

            _microphoneLevelMonitor.LevelChanged += MicrophoneLevelMonitor_LevelChanged;

            ConfigureSpeakerOverlay();
            ConfigureVibrationControls();
            ConfigureAdaptiveTriggerControls();
            ConfigureMicrophoneOverlay();

            _microphoneUiTimer.Start();

            _startupSoundPending = true;
            _startupSoundDeadlineUtc = DateTime.UtcNow.AddSeconds(3);

            InitializeControllerRuntime();
            UpdateControllerDependentMenuState(IsControllerConnected());
            TryPlayStartupSoundIfReady();
        }

        private void InitializeControllerRuntime()
        {
            DisposeControllerRuntime();

            _controllerRuntime = new DualSenseControllerRuntime(
                _overlayButtons.Select(binding => binding.SdlButton));

            _controllerRuntime.StatusChanged += ControllerRuntime_StatusChanged;
            _controllerRuntime.StateUpdated += ControllerRuntime_StateUpdated;
            _controllerRuntime.ControllerDisconnected += ControllerRuntime_ControllerDisconnected;
            _controllerRuntime.PlayerLedsChanged += ControllerRuntime_PlayerLedsChanged;
            _controllerRuntime.LightBarColorChanged += ControllerRuntime_LightBarColorChanged;

            UpdatePlayerLedOverlay(AppConstants.Hid.PlayerLedsOff);
            UpdateTouchPadLightBarOverlay(Colors.Transparent);
            UpdateMicMuteLedOverlay(false);
            UpdateControllerDependentMenuState(false);
            ResetMicrophoneOverlay();

            if (!_controllerRuntime.Start(out string? errorMessage))
            {
                _uiCoordinator?.SetStatus(errorMessage ?? UiMessageCatalog.Controller.SdlError, Brushes.OrangeRed);

                MessageBox.Show(
                    errorMessage ?? "Erreur SDL inconnue.",
                    "Erreur SDL",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ConfigureSpeakerOverlay()
        {
            if (SpeakerAudioOverlayControl == null)
                return;

            SpeakerAudioOverlayControl.VolumeChanged -= SpeakerAudioOverlayControl_VolumeChanged;
            SpeakerAudioOverlayControl.PlayRequested -= SpeakerAudioOverlayControl_PlayRequested;
            SpeakerAudioOverlayControl.PauseRequested -= SpeakerAudioOverlayControl_PauseRequested;
            SpeakerAudioOverlayControl.StopRequested -= SpeakerAudioOverlayControl_StopRequested;

            SpeakerAudioOverlayControl.Volume = _audioPlaybackController?.OverlayVolume ?? _audioService.OverlayVolume;
            SpeakerAudioOverlayControl.SetPlaybackState(isPlaying: false, isPaused: false);
            SpeakerAudioOverlayControl.ClearAudioStatusMessage();
            SpeakerVisualizer.Reset();

            SpeakerAudioOverlayControl.VolumeChanged += SpeakerAudioOverlayControl_VolumeChanged;
            SpeakerAudioOverlayControl.PlayRequested += SpeakerAudioOverlayControl_PlayRequested;
            SpeakerAudioOverlayControl.PauseRequested += SpeakerAudioOverlayControl_PauseRequested;
            SpeakerAudioOverlayControl.StopRequested += SpeakerAudioOverlayControl_StopRequested;
        }

        private void ConfigureMicrophoneOverlay()
        {
            ResetMicrophoneOverlay();
        }

        private void ConfigureAnimatedButtons()
        {
            _overlayButtons.Clear();
            _overlayButtons.AddRange(OverlayButtonBindingsFactory.Create(this));
        }

        private void ConfigureVibrationControls()
        {
            _vibrationControlCoordinator?.Dispose();
            _vibrationControlCoordinator = new VibrationControlCoordinator(
                controllerVibratorL,
                controllerVibratorR,
                _vibrationService,
                IsControllerConnected);
        }

        private void ConfigureAdaptiveTriggerControls()
        {
            _adaptiveTriggerControlCoordinator?.Dispose();
            _adaptiveTriggerControlCoordinator = new AdaptiveTriggerControlCoordinator(
                triggerForceFeedbackL,
                triggerForceFeedbackR,
                _adaptiveTriggerService,
                IsControllerConnected);
        }

        private void UpdateControllerDependentMenuState(bool isConnected)
        {
            TestSoundMenuItem.IsEnabled = isConnected;
            ColorToolBoxMenuItem.IsEnabled = isConnected;

            Player1MenuItem.IsEnabled = isConnected;
            Player2MenuItem.IsEnabled = isConnected;
            Player3MenuItem.IsEnabled = isConnected;
            Player4MenuItem.IsEnabled = isConnected;

            if (!isConnected)
            {
                Player1MenuItem.IsChecked = false;
                Player2MenuItem.IsChecked = false;
                Player3MenuItem.IsChecked = false;
                Player4MenuItem.IsChecked = false;
            }
        }

        private void ControllerRuntime_StatusChanged(object? sender, ControllerStatusChangedEventArgs e)
        {
            if (_isClosing)
                return;

            _uiCoordinator?.ApplyControllerStatus(e);
            UpdateControllerDependentMenuState(e.Status == ControllerRuntimeStatus.Connected);

            if (e.Status == ControllerRuntimeStatus.Connected)
            {
                _vibrationControlCoordinator?.SynchronizeHardwareState();
                _adaptiveTriggerControlCoordinator?.SynchronizeHardwareState();
                SynchronizePlayerMenuFromRuntime();
                RefreshControllerInfoPanel();
                TryStartSnifferIfPossible();
                TryPlayStartupSoundIfReady();
                StartMicrophoneMonitorIfPossible();
            }
        }

        private void TryPlayStartupSoundIfReady()
        {
            if (_isClosing || !_startupSoundPending)
                return;

            if (DateTime.UtcNow > _startupSoundDeadlineUtc)
            {
                _startupSoundPending = false;
                return;
            }

            if (!IsControllerConnected())
                return;

            _startupSoundPending = false;
            _ = PlayStartupSoundAsync();
        }

        private async Task PlayStartupSoundAsync()
        {
            bool success = await _audioService.PlayWaveAsync(
                controllerConnected: true,
                resourcePath: AppConstants.Assets.StartupWaveResourcePath);

            if (!success)
            {
                AppLogger.Warn(nameof(MainWindow), "Lecture automatique du son de démarrage impossible.");
            }
        }

        private void ControllerRuntime_StateUpdated(object? sender, ControllerStateSnapshot snapshot)
        {
            if (_isClosing)
                return;

            _uiCoordinator?.ApplyControllerSnapshot(snapshot);
            _isMicrophoneMuted = snapshot.IsMicMuted;
            UpdateMicMuteLedOverlay(snapshot.IsMicMuted);
            _debugWindow?.Update(snapshot);
            _communicationSniffer.CaptureSnapshot(snapshot);

            if (IsControllerConnected())
            {
                StartMicrophoneMonitorIfPossible();
            }
        }

        private void ControllerRuntime_ControllerDisconnected(object? sender, EventArgs e)
        {
            _uiCoordinator?.ApplyControllerDisconnected();
            _audioService.HandleControllerDisconnected();
            _vibrationControlCoordinator?.HandleControllerDisconnected();
            _adaptiveTriggerControlCoordinator?.HandleControllerDisconnected();
            _debugWindow?.Clear();

            _communicationSniffer.Stop();
            _microphoneLevelMonitor.Stop();

            if (_snifferWindow != null)
            {
                _snifferWindow.AppendHid("=== HID indisponible : manette deconnectee ===");
                _snifferWindow.AppendHid("=== En attente de connexion d'une manette DualSense... ===");
                _snifferWindow.AppendSdl("=== Manette deconnectee ===");
                _snifferWindow.AppendSdl("=== En attente de donnees SDL... ===");
            }

            UpdateControllerDependentMenuState(false);
            UpdatePlayerLedOverlay(AppConstants.Hid.PlayerLedsOff);
            UpdateTouchPadLightBarOverlay(Colors.Transparent);
            UpdateMicMuteLedOverlay(false);
            HideControllerInfoPanel();
            ResetMicrophoneOverlay();
        }

        private void UpdateMicMuteLedOverlay(bool isMuted)
        {
            MicMuteLedOverlay.SetLed(Colors.Orange, isMuted);
        }

        private void StartMicrophoneMonitorIfPossible()
        {
            if (_isClosing || !IsControllerConnected() || _microphoneLevelMonitor.IsRunning)
                return;

            if (!_microphoneLevelMonitor.Start(out string? errorMessage))
            {
                AppLogger.Warn(nameof(MainWindow), errorMessage ?? "Impossible de démarrer le monitoring micro.");
            }
        }

        private void MicrophoneLevelMonitor_LevelChanged(object? sender, float level)
        {
            _pendingMicrophoneLevel = Math.Clamp(level, 0f, 1f);
        }

        private void MicrophoneUiTimer_Tick(object? sender, EventArgs e)
        {
            float target = _pendingMicrophoneLevel;

            if (target > _displayedMicrophoneLevel)
            {
                _displayedMicrophoneLevel += (target - _displayedMicrophoneLevel) * 0.55f;
            }
            else
            {
                _displayedMicrophoneLevel += (target - _displayedMicrophoneLevel) * 0.18f;
            }

            _displayedMicrophoneLevel = Math.Clamp(_displayedMicrophoneLevel, 0f, 1f);

            bool isMicrophoneActive = !_isMicrophoneMuted && IsControllerConnected();
            MicrophoneLevelControl.SetActive(isMicrophoneActive);
            MicrophoneLevelControl.SetMeterLevel(isMicrophoneActive ? _displayedMicrophoneLevel : 0f);
        }

        private void ResetMicrophoneOverlay()
        {
            _pendingMicrophoneLevel = 0f;
            _displayedMicrophoneLevel = 0f;
            _isMicrophoneMuted = false;

            MicrophoneLevelControl.SetActive(false);
            MicrophoneLevelControl.SetMeterLevel(0f);
        }

        private void SynchronizePlayerMenuFromRuntime()
        {
            if (_controllerRuntime == null)
                return;

            SelectPlayerSlot(_controllerRuntime.PlayerSlot, applyToHardware: false);
        }

        private void SelectPlayerSlot(int playerSlot, bool applyToHardware)
        {
            if (!IsControllerConnected())
            {
                Player1MenuItem.IsChecked = false;
                Player2MenuItem.IsChecked = false;
                Player3MenuItem.IsChecked = false;
                Player4MenuItem.IsChecked = false;
                return;
            }

            Player1MenuItem.IsChecked = playerSlot == 1;
            Player2MenuItem.IsChecked = playerSlot == 2;
            Player3MenuItem.IsChecked = playerSlot == 3;
            Player4MenuItem.IsChecked = playerSlot == 4;

            if (applyToHardware && _controllerRuntime != null && _controllerRuntime.IsControllerConnected)
            {
                _controllerRuntime.SetPlayerSlot(playerSlot);
            }

            UpdateCurrentPlayerLabel(playerSlot);
        }

        private void BatteryRefreshTimer_Tick(object? sender, EventArgs e)
        {
            RefreshControllerInfoPanel();
        }

        private void RefreshControllerInfoPanel()
        {
            if (!IsControllerConnected())
            {
                HideControllerInfoPanel();
                return;
            }

            ControllerInfoPanel.Visibility = Visibility.Visible;
            UpdateCurrentPlayerLabel(_controllerRuntime?.PlayerSlot ?? 1);
            RefreshBatteryDisplay();

            if (!_batteryRefreshTimer.IsEnabled)
            {
                _batteryRefreshTimer.Start();
            }
        }

        private void HideControllerInfoPanel()
        {
            _batteryRefreshTimer.Stop();
            ControllerInfoPanel.Visibility = Visibility.Collapsed;
            BatteryPercentText.Text = "-- %";
            BatteryFillRect.Width = 0;
            CurrentPlayerText.Text = "Player : --";
        }

        private void RefreshBatteryDisplay()
        {
            if (!_batteryReader.TryReadBatteryInfo(out DualSenseBatteryInfo batteryInfo))
            {
                BatteryPercentText.Text = "-- %";
                BatteryFillRect.Width = 0;
                BatteryFillRect.Fill = Brushes.LightGray;
                return;
            }

            BatteryPercentText.Text = $"{batteryInfo.Percentage} %";
            BatteryFillRect.Width = 16.0 * batteryInfo.Percentage / 100.0;
            BatteryFillRect.Fill = ResolveBatteryBrush(batteryInfo);
            BatteryChargingIcon.Visibility = batteryInfo.IsCharging
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private Brush ResolveBatteryBrush(DualSenseBatteryInfo batteryInfo)
        {
            if (batteryInfo.IsCharging)
                return Brushes.DodgerBlue;

            if (batteryInfo.Percentage <= 20)
                return Brushes.OrangeRed;

            if (batteryInfo.Percentage <= 50)
                return Brushes.Goldenrod;

            return Brushes.SeaGreen;
        }

        private void UpdateCurrentPlayerLabel(int playerSlot)
        {
            CurrentPlayerText.Text = $"Player : {playerSlot}";
        }

        private void ControllerRuntime_PlayerLedsChanged(object? sender, byte ledMask)
        {
            if (_isClosing)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isClosing)
                    return;

                UpdatePlayerLedOverlay(ledMask);
            }));
        }

        private void ControllerRuntime_LightBarColorChanged(Color color)
        {
            if (_isClosing)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isClosing)
                    return;

                UpdateTouchPadLightBarOverlay(color);
            }));
        }

        private void UpdateTouchPadLightBarOverlay(Color color)
        {
            bool isEnabled = color.A > 0 && (color.R > 0 || color.G > 0 || color.B > 0);

            TouchPadLightBarLeftOverlay.SetLight(color, isEnabled);
            TouchPadLightBarRightOverlay.SetLight(color, isEnabled);
        }

        private void UpdatePlayerLedOverlay(byte ledMask)
        {
            byte mask = (byte)(ledMask & 0x1F);
            bool shouldRestartAnimation = _lastPlayerLedMask != mask;

            UpdatePlayerLedIndicator(PlayerLedUi1, (mask & 0x01) != 0);
            UpdatePlayerLedIndicator(PlayerLedUi2, (mask & 0x02) != 0);
            UpdatePlayerLedIndicator(PlayerLedUi3, (mask & 0x04) != 0);
            UpdatePlayerLedIndicator(PlayerLedUi4, (mask & 0x08) != 0);
            UpdatePlayerLedIndicator(PlayerLedUi5, (mask & 0x10) != 0);

            if (shouldRestartAnimation)
            {
                RestartPlayerLedAnimations();
                _lastPlayerLedMask = mask;
            }
        }

        private void RestartPlayerLedAnimations()
        {
            PlayerLedUi1.RestartAnimation();
            PlayerLedUi2.RestartAnimation();
            PlayerLedUi3.RestartAnimation();
            PlayerLedUi4.RestartAnimation();
            PlayerLedUi5.RestartAnimation();
        }

        private void UpdatePlayerLedIndicator(BreathingPlayerLedOverlay indicator, bool isOn)
        {
            indicator.SetLed(Colors.White, isOn);
        }

        private void DebugMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleDebugWindow(DebugMenuItem.IsChecked);
        }

        private void ToggleDebugWindow(bool shouldOpen)
        {
            if (shouldOpen)
            {
                if (_debugWindow != null)
                {
                    _debugWindow.Activate();
                    return;
                }

                _debugWindow = new DebugMonitorWindow
                {
                    Owner = this
                };
                _debugWindow.Closed += DebugWindow_Closed;
                _debugWindow.Show();
                return;
            }

            CloseDebugWindow();
        }

        private void DebugWindow_Closed(object? sender, EventArgs e)
        {
            if (_debugWindow != null)
                _debugWindow.Closed -= DebugWindow_Closed;

            _debugWindow = null;
            DebugMenuItem.IsChecked = false;
        }

        private void CloseDebugWindow()
        {
            if (_debugWindow == null)
                return;

            Window window = _debugWindow;
            _debugWindow = null;
            window.Closed -= DebugWindow_Closed;
            window.Close();
            DebugMenuItem.IsChecked = false;
        }

        private void CommunicationSniffer_HidLineCaptured(object? sender, string line)
        {
            if (_isClosing)
                return;

            _snifferWindow?.AppendHid(line);
        }

        private void CommunicationSniffer_SdlLineCaptured(object? sender, string line)
        {
            if (_isClosing)
                return;

            _snifferWindow?.AppendSdl(line);
        }

        private void CommunicationSniffer_StatusChanged(object? sender, string message)
        {
            if (_isClosing)
                return;

            AppLogger.Info(nameof(MainWindow), $"Sniffer: {message}");
        }

        private void AudioService_StatusChanged(object? sender, DualSenseAudioStatusChangedEventArgs e)
        {
            if (_isClosing)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isClosing)
                    return;

                _uiCoordinator?.ApplyAudioStatus(e);
            }));
        }

        private void AudioService_AudioLevelChanged(object? sender, AudioVisualizerFrame frame)
        {
            if (_isClosing)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isClosing)
                    return;

                _uiCoordinator?.UpdateAudioFrame(frame);
            }));
        }

        private void AudioService_PlaybackProgressChanged(object? sender, PlaybackProgressChangedEventArgs e)
        {
            if (_isClosing)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isClosing)
                    return;

                _uiCoordinator?.UpdatePlaybackProgress(e.Position, e.Duration);
            }));
        }

        private void Quitter_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void APropos_Click(object sender, RoutedEventArgs e)
        {
            var window = new AboutWindow(AboutWindowContent.CreateDefault())
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void Player1MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectPlayerSlot(1, applyToHardware: true);
        }

        private void Player2MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectPlayerSlot(2, applyToHardware: true);
        }

        private void Player3MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectPlayerSlot(3, applyToHardware: true);
        }

        private void Player4MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectPlayerSlot(4, applyToHardware: true);
        }

        private void OpenColorToolBox(object sender, RoutedEventArgs e)
        {
            var window = new ColorToolBox.ColorToolBoxWindow
            {
                Owner = this,
                SelectedColor = _controllerRuntime?.CurrentLightBarColor ?? Colors.Blue
            };

            window.SelectedColorChanged += ColorToolBoxWindow_SelectedColorChanged;
            window.Closed += ColorToolBoxWindow_Closed;
            window.ShowDialog();
        }

        private void ColorToolBoxWindow_SelectedColorChanged(object? sender, Color color)
        {
            _controllerRuntime?.SetLightBarColor(color);
        }

        private void ColorToolBoxWindow_Closed(object? sender, EventArgs e)
        {
            if (sender is not ColorToolBox.ColorToolBoxWindow window)
                return;

            window.SelectedColorChanged -= ColorToolBoxWindow_SelectedColorChanged;
            window.Closed -= ColorToolBoxWindow_Closed;
        }

        private void SnifferMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleSnifferWindow(SnifferMenuItem.IsChecked);
        }

        private void TryStartSnifferIfPossible()
        {
            if (_snifferWindow == null || _communicationSniffer.IsRunning)
                return;

            if (!IsControllerConnected())
            {
                _snifferWindow.AppendHid("=== En attente de connexion d'une manette DualSense... ===");
                _snifferWindow.AppendSdl("=== En attente de donnees SDL... ===");
                return;
            }

            if (!_communicationSniffer.Start(out string errorMessage))
            {
                _snifferWindow.AppendHid("=== Interface HID DualSense indisponible pour le moment ===");
                _snifferWindow.AppendHid($"=== Detail : {errorMessage} ===");
                _snifferWindow.AppendSdl("=== En attente de donnees SDL... ===");
            }
        }

        private void ToggleSnifferWindow(bool shouldOpen)
        {
            if (shouldOpen)
            {
                if (_snifferWindow != null)
                {
                    _snifferWindow.Activate();
                    return;
                }

                _snifferWindow = new DualSenseSnifferWindow
                {
                    Owner = this
                };
                _snifferWindow.Closed += SnifferWindow_Closed;
                _snifferWindow.Show();
                _snifferWindow.ClearOutput();

                TryStartSnifferIfPossible();
                return;
            }

            CloseSnifferWindow();
        }

        private void SnifferWindow_Closed(object? sender, EventArgs e)
        {
            _communicationSniffer.Stop();

            if (_snifferWindow != null)
                _snifferWindow.Closed -= SnifferWindow_Closed;

            _snifferWindow = null;
            SnifferMenuItem.IsChecked = false;
        }

        private void CloseSnifferWindow()
        {
            if (_snifferWindow == null)
                return;

            _communicationSniffer.Stop();

            Window window = _snifferWindow;
            _snifferWindow = null;
            window.Closed -= SnifferWindow_Closed;
            window.Close();
            SnifferMenuItem.IsChecked = false;
        }

        private async void TestInternalSpeaker_Click(object sender, RoutedEventArgs e)
        {
            await PlayMenuBeepSequenceAsync();
        }

        private void SpeakerAudioOverlayControl_VolumeChanged(object? sender, double volume)
        {
            _audioService.SetOverlayVolume(volume, IsControllerConnected());
        }

        private async void SpeakerAudioOverlayControl_PlayRequested(object? sender, EventArgs e)
        {
            await PlayWaveTestAsync();
        }

        private void SpeakerAudioOverlayControl_PauseRequested(object? sender, EventArgs e)
        {
            _audioService.Pause();
        }

        private void SpeakerAudioOverlayControl_StopRequested(object? sender, EventArgs e)
        {
            _audioService.Stop(
                controllerConnected: IsControllerConnected(),
                restoreRouting: true,
                statusMessage: UiMessageCatalog.Audio.NoPlayback);
        }

        private async Task PlayMenuBeepSequenceAsync()
        {
            if (!IsControllerConnected())
            {
                MessageBox.Show(
                    UiMessageCatalog.Audio.UsbRequired,
                    "DualSense",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool success = await _audioService.PlayIndependentBeepSequenceAsync(controllerConnected: true);

            if (!success && !_isClosing)
            {
                MessageBox.Show(
                    _uiCoordinator?.GetAudioStatusMessage() ?? UiMessageCatalog.Audio.GenericError,
                    "Test audio DualSense",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task PlayWaveTestAsync()
        {
            if (!IsControllerConnected())
            {
                MessageBox.Show(
                    UiMessageCatalog.Audio.UsbRequired,
                    "DualSense",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool success = await _audioService.PlayWaveAsync(controllerConnected: true);

            if (!success && !_isClosing)
            {
                MessageBox.Show(
                    _uiCoordinator?.GetAudioStatusMessage() ?? UiMessageCatalog.Audio.GenericError,
                    "Test audio DualSense",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool IsControllerConnected()
        {
            return _controllerRuntime != null && _controllerRuntime.IsControllerConnected;
        }

        private void DisposeControllerRuntime()
        {
            if (_controllerRuntime == null)
                return;

            _controllerRuntime.StatusChanged -= ControllerRuntime_StatusChanged;
            _controllerRuntime.StateUpdated -= ControllerRuntime_StateUpdated;
            _controllerRuntime.ControllerDisconnected -= ControllerRuntime_ControllerDisconnected;
            _controllerRuntime.PlayerLedsChanged -= ControllerRuntime_PlayerLedsChanged;
            _controllerRuntime.LightBarColorChanged -= ControllerRuntime_LightBarColorChanged;
            _controllerRuntime.Dispose();
            _controllerRuntime = null;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            AppLogger.Info(nameof(MainWindow), "Fermeture de la fenêtre principale.");

            CloseDebugWindow();
            CloseSnifferWindow();

            SpeakerAudioOverlayControl.VolumeChanged -= SpeakerAudioOverlayControl_VolumeChanged;
            SpeakerAudioOverlayControl.PlayRequested -= SpeakerAudioOverlayControl_PlayRequested;
            SpeakerAudioOverlayControl.PauseRequested -= SpeakerAudioOverlayControl_PauseRequested;
            SpeakerAudioOverlayControl.StopRequested -= SpeakerAudioOverlayControl_StopRequested;

            _audioService.StatusChanged -= AudioService_StatusChanged;
            _audioService.AudioLevelChanged -= AudioService_AudioLevelChanged;
            _audioService.PlaybackProgressChanged -= AudioService_PlaybackProgressChanged;
            _audioService.Dispose();

            _vibrationControlCoordinator?.Dispose();
            _vibrationControlCoordinator = null;
            _vibrationService.Dispose();

            _adaptiveTriggerControlCoordinator?.Dispose();
            _adaptiveTriggerControlCoordinator = null;
            _adaptiveTriggerService.Dispose();

            _communicationSniffer.HidLineCaptured -= CommunicationSniffer_HidLineCaptured;
            _communicationSniffer.SdlLineCaptured -= CommunicationSniffer_SdlLineCaptured;
            _communicationSniffer.StatusChanged -= CommunicationSniffer_StatusChanged;
            _communicationSniffer.Dispose();

            _microphoneUiTimer.Stop();
            _microphoneUiTimer.Tick -= MicrophoneUiTimer_Tick;
            _microphoneLevelMonitor.LevelChanged -= MicrophoneLevelMonitor_LevelChanged;
            _microphoneLevelMonitor.Stop();
            _microphoneLevelMonitor.Dispose();

            _batteryRefreshTimer.Tick -= BatteryRefreshTimer_Tick;
            _batteryRefreshTimer.Stop();
            _batteryReader.Dispose();

            DisposeControllerRuntime();

            base.OnClosed(e);
            Environment.Exit(0);

            return;
        }
    }

    internal sealed class OverlayButtonBinding
    {
        public SDL.SDL_GameControllerButton SdlButton { get; }
        public RippleButtonOverlay Overlay { get; }

        public OverlayButtonBinding(
            SDL.SDL_GameControllerButton sdlButton,
            RippleButtonOverlay overlay)
        {
            SdlButton = sdlButton;
            Overlay = overlay;
        }
    }
}
