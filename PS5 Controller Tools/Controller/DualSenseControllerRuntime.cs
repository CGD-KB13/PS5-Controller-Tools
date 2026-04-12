using SDL2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using PS5_Controller_Tools.Lighting;

namespace PS5_Controller_Tools
{
    internal sealed class DualSenseControllerRuntime : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly SDL.SDL_GameControllerButton[] _trackedButtons;
        private readonly DualSenseLightService _lightService = new();

        private bool _sdlInitialized;
        private bool _isDisposed;
        private bool _micMuted;
        private bool _wasMicPressed;
        private int _playerSlot = 1;
        private bool _isPlayerSequenceRunning; 
        private bool _isLightBarSequenceRunning;

        private IntPtr _controller = IntPtr.Zero;
        private IntPtr _joystick = IntPtr.Zero;
        private ControllerRuntimeStatus? _lastRaisedStatus;
        private string? _lastRaisedMessage;
        private DateTime _nextReconnectScanUtc = DateTime.MinValue;
        private (byte R, byte G, byte B) _currentLightBarColor = (0, 0, 255);
        public Color CurrentLightBarColor =>
            Color.FromRgb(_currentLightBarColor.R, _currentLightBarColor.G, _currentLightBarColor.B);

        public event EventHandler<ControllerStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<ControllerStateSnapshot>? StateUpdated;
        public event EventHandler? ControllerDisconnected;
        public event EventHandler<byte>? PlayerLedsChanged;
        public event Action<Color>? LightBarColorChanged;

        public bool IsSdlInitialized => _sdlInitialized;
        public bool IsControllerConnected => _controller != IntPtr.Zero;
        public IntPtr ControllerHandle => _controller;
        public IntPtr JoystickHandle => _joystick;
        public int PlayerSlot => _playerSlot;

        public DualSenseControllerRuntime(IEnumerable<SDL.SDL_GameControllerButton> trackedButtons)
        {
            _trackedButtons = trackedButtons?.Distinct().ToArray()
                ?? Array.Empty<SDL.SDL_GameControllerButton>();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AppConstants.Controller.PollingIntervalMs)
            };
            _timer.Tick += Timer_Tick;
        }

        public bool Start(out string? errorMessage)
        {
            ThrowIfDisposed();

            errorMessage = null;

            if (!InitializeSdl(out errorMessage))
            {
                RaiseStatus(ControllerRuntimeStatus.Error, errorMessage ?? UiMessageCatalog.Controller.SdlError);
                return false;
            }

            DetectController();

            _timer.Start();
            AppLogger.Info(nameof(DualSenseControllerRuntime), "Runtime SDL demarre.");
            return true;
        }

        public void SetPlayerSlot(int playerSlot)
        {
            ThrowIfDisposed();

            _playerSlot = NormalizePlayerSlot(playerSlot);

            if (!IsControllerConnected || _isPlayerSequenceRunning)
                return;

            ApplyCurrentPlayerLeds();
        }

        public void SetLightBarColor(Color color)
        {
            ThrowIfDisposed();

            _currentLightBarColor = (color.R, color.G, color.B);
            NotifyLightBarColorChanged(color.R, color.G, color.B);

            if (!IsControllerConnected)
                return;

            _lightService.SetLightBar(color.R, color.G, color.B);
        }

        private void NotifyLightBarColorChanged(byte red, byte green, byte blue)
        {
            LightBarColorChanged?.Invoke(Color.FromRgb(red, green, blue));
        }

        private int NormalizePlayerSlot(int slot)
        {
            return slot switch
            {
                < 1 => 1,
                > 4 => 4,
                _ => slot
            };
        }
        private void SetPlayerLeds(byte playerLeds)
        {
            _lightService.SetPlayerLeds(playerLeds);
            PlayerLedsChanged?.Invoke(this, (byte)(playerLeds & 0x1F));
        }

        private void ApplyCurrentPlayerLeds()
        {
            byte playerLeds = _playerSlot switch
            {
                1 => AppConstants.Hid.Player1Leds,
                2 => AppConstants.Hid.Player2Leds,
                3 => AppConstants.Hid.Player3Leds,
                4 => AppConstants.Hid.Player4Leds,
                _ => AppConstants.Hid.Player1Leds
            };

            SetPlayerLeds(playerLeds);

            if (_micMuted)
            {
                _lightService.SetMicrophoneLed(true);
            }
        }

        private async void PlayPlayerConnectionSequence()
        {
            if (_isPlayerSequenceRunning || _isDisposed || !IsControllerConnected)
                return;

            _isPlayerSequenceRunning = true;

            try
            {
                SetPlayerLeds(AppConstants.Hid.Player1Leds);
                await Task.Delay(AppConstants.Controller.PlayerSequenceDelayMs);
                if (_isDisposed || !IsControllerConnected) return;

                SetPlayerLeds(AppConstants.Hid.Player2Leds);
                await Task.Delay(AppConstants.Controller.PlayerSequenceDelayMs);
                if (_isDisposed || !IsControllerConnected) return;

                SetPlayerLeds(AppConstants.Hid.Player3Leds);
                await Task.Delay(AppConstants.Controller.PlayerSequenceDelayMs);
                if (_isDisposed || !IsControllerConnected) return;

                SetPlayerLeds(AppConstants.Hid.Player4Leds);
                await Task.Delay(AppConstants.Controller.PlayerSequenceDelayMs);
                if (_isDisposed || !IsControllerConnected) return;

                _playerSlot = 1;
                SetPlayerLeds(AppConstants.Hid.Player1Leds);
            }
            finally
            {
                _isPlayerSequenceRunning = false;
            }
        }

        private async Task PlayKnightRiderSequence(int delayMs = 200, int repeatCount = 3)
        {
            // 5 LED individuelles :
            // 0x01, 0x02, 0x04, 0x08, 0x10
            // puis retour : 0x08, 0x04, 0x02
            byte[] frames =
            {
                0x01,
                0x02,
                0x04,
                0x08,
                0x10,
                0x08,
                0x04,
                0x02
            };

            for (int loop = 0; loop < repeatCount; loop++)
            {
                foreach (byte frame in frames)
                {
                    if (_isDisposed || !IsControllerConnected)
                        return;

                    _lightService.SetPlayerLeds(frame);
                    await Task.Delay(delayMs);
                }
            }

            // état final : Player 1
            _playerSlot = 1;
            _lightService.SetPlayerLeds(AppConstants.Hid.Player1Leds);
        }

        private async void PlayLightBarConnectionSequence()
        {
            if (_isLightBarSequenceRunning || _isDisposed || !IsControllerConnected)
                return;

            _isLightBarSequenceRunning = true;

            try
            {
                (byte R, byte G, byte B)[] sequence =
                {
                    (0,   0, 255), // Bleu
                    (0, 255,   0), // Vert
                    (255, 0,   0), // Rouge
                    (0, 255,   0), // Vert
                    (0,   0, 255), // Bleu
                    (0, 255,   0), // Vert
                    (255, 0,   0), // Rouge
                };

                foreach (var color in sequence)
                {
                    if (_isDisposed || !IsControllerConnected)
                        return;

                    _lightService.SetLightBar(color.R, color.G, color.B);
                    NotifyLightBarColorChanged(color.R, color.G, color.B);
                    await Task.Delay(AppConstants.Controller.LightBarSequenceDelayMs);
                }

                _lightService.SetLightBar(
                    _currentLightBarColor.R,
                    _currentLightBarColor.G,
                    _currentLightBarColor.B
                );
                NotifyLightBarColorChanged(
                    _currentLightBarColor.R,
                    _currentLightBarColor.G,
                    _currentLightBarColor.B
                );
            }
            finally
            {
                _isLightBarSequenceRunning = false;
            }
        }

        public void Stop()
        {
            _timer.Stop();
            _lightService.Reset();
            CloseCurrentController();
            ResetTransientState();

            if (_sdlInitialized)
            {
                SDL.SDL_Quit();
                _sdlInitialized = false;
                AppLogger.Info(nameof(DualSenseControllerRuntime), "Runtime SDL arrete.");
            }
        }

        private bool InitializeSdl(out string? errorMessage)
        {
            errorMessage = null;

            if (_sdlInitialized)
                return true;

            try
            {
                int result = SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
                if (result != 0)
                {
                    errorMessage = SDL.SDL_GetError();
                    AppLogger.Warn(nameof(DualSenseControllerRuntime), $"Initialisation SDL echouee : {errorMessage}");
                    return false;
                }

                _sdlInitialized = true;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                errorMessage = ex.Message;
                AppLogger.Error(nameof(DualSenseControllerRuntime), "Bibliotheque SDL introuvable.", ex);
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.ToString();
                AppLogger.Error(nameof(DualSenseControllerRuntime), "Erreur inattendue pendant l'initialisation SDL.", ex);
                return false;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_sdlInitialized)
                return;

            SDL.SDL_PumpEvents();

            if (_controller == IntPtr.Zero)
            {
                if (DateTime.UtcNow < _nextReconnectScanUtc)
                    return;

                _nextReconnectScanUtc = DateTime.UtcNow.AddMilliseconds(AppConstants.Controller.ReconnectScanIntervalMs);
                DetectController();
                return;
            }

            if (SDL.SDL_GameControllerGetAttached(_controller) == SDL.SDL_bool.SDL_FALSE)
            {
                AppLogger.Warn(nameof(DualSenseControllerRuntime), "Manette deconnectee.");
                RaiseStatus(ControllerRuntimeStatus.Disconnected, UiMessageCatalog.Controller.Disconnected);
                CloseCurrentController();
                ResetTransientState();
                _nextReconnectScanUtc = DateTime.UtcNow;
                ControllerDisconnected?.Invoke(this, EventArgs.Empty);
                return;
            }

            StateUpdated?.Invoke(this, BuildSnapshot());
        }

        private void DetectController()
        {
            if (!_sdlInitialized)
            {
                RaiseStatus(ControllerRuntimeStatus.Error, UiMessageCatalog.Controller.SdlNotInitialized);
                return;
            }

            CloseCurrentController();

            int joystickCount = SDL.SDL_NumJoysticks();
            for (int i = 0; i < joystickCount; i++)
            {
                if (SDL.SDL_IsGameController(i) != SDL.SDL_bool.SDL_TRUE)
                    continue;

                IntPtr controller = SDL.SDL_GameControllerOpen(i);
                if (controller == IntPtr.Zero)
                    continue;

                IntPtr joystick = SDL.SDL_GameControllerGetJoystick(controller);
                if (joystick == IntPtr.Zero)
                {
                    SDL.SDL_GameControllerClose(controller);
                    continue;
                }

                _controller = controller;
                _joystick = joystick;
                _playerSlot = NormalizePlayerSlot(i + 1);
                _nextReconnectScanUtc = DateTime.MinValue;

                PlayPlayerConnectionSequence();
                //_ = PlayKnightRiderSequence();
                PlayLightBarConnectionSequence();

                string controllerName = SDL.SDL_GameControllerName(controller) ?? "Manette inconnue";
                AppLogger.Info(nameof(DualSenseControllerRuntime), $"Manette detectee sur l'index SDL {i} : {controllerName}");
                string deviceName = SDL.SDL_GameControllerName(_controller);
                RaiseStatus(ControllerRuntimeStatus.Connected, UiMessageCatalog.Controller.Connected(deviceName));
                return;
            }

            RaiseStatus(ControllerRuntimeStatus.Disconnected, UiMessageCatalog.Controller.NotDetected);
        }

        private ControllerStateSnapshot BuildSnapshot()
        {
            var pressedButtons = new HashSet<SDL.SDL_GameControllerButton>();
            foreach (SDL.SDL_GameControllerButton button in _trackedButtons)
            {
                if (SDL.SDL_GameControllerGetButton(_controller, button) == 1)
                    pressedButtons.Add(button);
            }

            bool isJoystickAttached =
                _joystick != IntPtr.Zero &&
                SDL.SDL_JoystickGetAttached(_joystick) == SDL.SDL_bool.SDL_TRUE;

            bool isTouchPadPressed =
                isJoystickAttached &&
                SDL.SDL_JoystickGetButton(_joystick, AppConstants.Controller.TouchPadButtonIndex) == 1;

            bool isMicPressed =
                isJoystickAttached &&
                SDL.SDL_JoystickGetButton(_joystick, AppConstants.Controller.MicButtonIndex) == 1;

            if (isMicPressed && !_wasMicPressed)
            {
                _micMuted = !_micMuted;
                _lightService.SetMicrophoneLed(_micMuted);
            }

            _wasMicPressed = isMicPressed;

            short leftTriggerRaw = SDL.SDL_GameControllerGetAxis(
                _controller,
                SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT);

            short rightTriggerRaw = SDL.SDL_GameControllerGetAxis(
                _controller,
                SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT);

            short leftXRaw = SDL.SDL_GameControllerGetAxis(
                _controller,
                SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);

            short leftYRaw = SDL.SDL_GameControllerGetAxis(
                _controller,
                SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);

            short rightXRaw = SDL.SDL_GameControllerGetAxis(
                _controller,
                SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);

            short rightYRaw = SDL.SDL_GameControllerGetAxis(
                _controller,
                SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);

            return new ControllerStateSnapshot(
                _controller,
                _joystick,
                pressedButtons,
                isTouchPadPressed,
                _micMuted,
                NormalizeTrigger(leftTriggerRaw),
                NormalizeTrigger(rightTriggerRaw),
                NormalizeStickAxis(leftXRaw),
                NormalizeStickAxis(leftYRaw),
                NormalizeStickAxis(rightXRaw),
                NormalizeStickAxis(rightYRaw));
        }

        private static double NormalizeTrigger(short rawValue)
        {
            double normalized;

            if (rawValue < AppConstants.Controller.TriggerSignedDetectionThreshold)
                normalized = (rawValue + 32768.0) / 65535.0;
            else
                normalized = rawValue / 32767.0;

            if (normalized < AppConstants.Controller.TriggerDeadZone)
                return 0.0;

            normalized = (normalized - AppConstants.Controller.TriggerDeadZone) /
                         (1.0 - AppConstants.Controller.TriggerDeadZone);

            if (normalized < 0.0)
                return 0.0;

            if (normalized > 1.0)
                return 1.0;

            return normalized;
        }

        private static double NormalizeStickAxis(short rawValue)
        {
            double value = rawValue == short.MinValue
                ? -1.0
                : rawValue / 32767.0;

            value = Math.Clamp(value, -1.0, 1.0);

            double abs = Math.Abs(value);
            if (abs < AppConstants.Controller.StickDeadZone)
                return 0.0;

            double adjusted = (abs - AppConstants.Controller.StickDeadZone) /
                              (1.0 - AppConstants.Controller.StickDeadZone);
            return Math.Sign(value) * adjusted;
        }

        private void CloseCurrentController()
        {
            _joystick = IntPtr.Zero;

            if (_controller != IntPtr.Zero)
            {
                SDL.SDL_GameControllerClose(_controller);
                _controller = IntPtr.Zero;
            }
        }

        private void ResetTransientState()
        {
            _micMuted = false;
            _wasMicPressed = false;
            _lightService.Reset();
            LightBarColorChanged?.Invoke(Colors.Black);
        }

        private void RaiseStatus(ControllerRuntimeStatus status, string message)
        {
            if (_lastRaisedStatus == status &&
                string.Equals(_lastRaisedMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            _lastRaisedStatus = status;
            _lastRaisedMessage = message;
            StatusChanged?.Invoke(this, new ControllerStatusChangedEventArgs(status, message));
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DualSenseControllerRuntime));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _timer.Tick -= Timer_Tick;
            _lightService.Dispose();
            _isDisposed = true;
        }
    }

    public sealed class ControllerStateSnapshot : EventArgs
    {
        public IntPtr ControllerHandle { get; }
        public IntPtr JoystickHandle { get; }
        public IReadOnlyCollection<SDL.SDL_GameControllerButton> PressedButtons { get; }
        public bool IsTouchPadPressed { get; }
        public bool IsMicMuted { get; }
        public double LeftTriggerPressure { get; }
        public double RightTriggerPressure { get; }
        public double LeftStickX { get; }
        public double LeftStickY { get; }
        public double RightStickX { get; }
        public double RightStickY { get; }

        public ControllerStateSnapshot(
            IntPtr controllerHandle,
            IntPtr joystickHandle,
            IReadOnlyCollection<SDL.SDL_GameControllerButton> pressedButtons,
            bool isTouchPadPressed,
            bool isMicMuted,
            double leftTriggerPressure,
            double rightTriggerPressure,
            double leftStickX,
            double leftStickY,
            double rightStickX,
            double rightStickY)
        {
            ControllerHandle = controllerHandle;
            JoystickHandle = joystickHandle;
            PressedButtons = pressedButtons;
            IsTouchPadPressed = isTouchPadPressed;
            IsMicMuted = isMicMuted;
            LeftTriggerPressure = leftTriggerPressure;
            RightTriggerPressure = rightTriggerPressure;
            LeftStickX = leftStickX;
            LeftStickY = leftStickY;
            RightStickX = rightStickX;
            RightStickY = rightStickY;
        }
    }

    internal enum ControllerRuntimeStatus
    {
        Connected,
        Disconnected,
        Error
    }

    internal sealed class ControllerStatusChangedEventArgs : EventArgs
    {
        public ControllerRuntimeStatus Status { get; }
        public string Message { get; }

        public ControllerStatusChangedEventArgs(ControllerRuntimeStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }
}
