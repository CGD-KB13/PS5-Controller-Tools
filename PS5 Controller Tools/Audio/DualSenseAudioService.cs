
namespace PS5_Controller_Tools
{
    internal enum DualSenseAudioState
    {
        Stopped,
        Playing,
        Paused
    }

    internal enum AudioPlaybackMode
    {
        None,
        Beep,
        Wave
    }

    internal sealed class AudioVisualizerFrame : EventArgs
    {
        public float Level { get; }
        public AudioPlaybackMode Mode { get; }

        public AudioVisualizerFrame(float level, AudioPlaybackMode mode)
        {
            Level = Math.Clamp(level, 0.0f, 1.0f);
            Mode = mode;
        }
    }

    internal sealed class DualSenseAudioStatusChangedEventArgs : EventArgs
    {
        public DualSenseAudioState State { get; }
        public string Message { get; }
        public string? DeviceName { get; }
        public AudioPlaybackMode Mode { get; }
        public string? TrackName { get; }

        public DualSenseAudioStatusChangedEventArgs(
            DualSenseAudioState state,
            string message,
            string? deviceName = null,
            AudioPlaybackMode mode = AudioPlaybackMode.None,
            string? trackName = null)
        {
            State = state;
            Message = message;
            DeviceName = deviceName;
            Mode = mode;
            TrackName = string.IsNullOrWhiteSpace(trackName) ? null : trackName.Trim();
        }
    }

    internal sealed class DualSenseAudioService : IDisposable
    {
        private readonly DualSenseSpeakerTestPlayer _speakerTestPlayer = new();
        private readonly DualSenseHidAudioControl _hidAudioControl = new();

        private bool _isDisposed;
        private bool _isShuttingDown;
        private double _overlayVolume = AppConstants.Audio.DefaultSpeakerOverlayVolume;
        private byte _speakerVolume = ConvertOverlayVolumeToSpeakerVolume(AppConstants.Audio.DefaultSpeakerOverlayVolume);
        private byte? _lastAppliedSpeakerVolume;
        private int _activeBeepSequences;
        private AudioPlaybackMode _currentVisualizerMode = AudioPlaybackMode.None;
        private string? _currentWaveResourcePath;

        public event EventHandler<DualSenseAudioStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<AudioVisualizerFrame>? AudioLevelChanged;
        public event EventHandler<PlaybackProgressChangedEventArgs>? PlaybackProgressChanged;

        public double OverlayVolume => _overlayVolume;
        public bool IsPlaying => _speakerTestPlayer.IsPlaying;
        public bool IsPaused => _speakerTestPlayer.IsPaused;
        public bool HasPlaybackSession => _speakerTestPlayer.HasPlaybackSession;

        public DualSenseAudioService()
        {
            _speakerTestPlayer.PlaybackEnded += SpeakerTestPlayer_PlaybackEnded;
            _speakerTestPlayer.AudioLevelChanged += SpeakerTestPlayer_AudioLevelChanged;
            _speakerTestPlayer.PlaybackProgressChanged += SpeakerTestPlayer_PlaybackProgressChanged;
        }

        public void SetOverlayVolume(double volume, bool controllerConnected)
        {
            ThrowIfDisposed();

            _overlayVolume = volume;
            byte mappedSpeakerVolume = ConvertOverlayVolumeToSpeakerVolume(volume);
            bool volumeChanged = mappedSpeakerVolume != _speakerVolume;
            _speakerVolume = mappedSpeakerVolume;

            if (volumeChanged)
            {
                AppLogger.Info(nameof(DualSenseAudioService), $"Volume overlay mis à jour: {volume:0.##} -> volume matériel {_speakerVolume}");
            }

            bool shouldApplyToController =
                controllerConnected &&
                (_lastAppliedSpeakerVolume != _speakerVolume || !_hidAudioControl.IsConnected);

            if (shouldApplyToController)
            {
                ApplySpeakerVolumeToInternalSpeaker(showErrorDialog: false);
            }
        }

        private static string GetTrackName(string resourcePath)
        {
            return System.IO.Path.GetFileName(resourcePath);
        }

        private string GetCurrentTrackName()
        {
            return GetTrackName(_currentWaveResourcePath ?? AppConstants.Assets.TestWaveResourcePath);
        }

        public Task<bool> PlayWaveAsync(bool controllerConnected)
        {
            return PlayWaveAsync(controllerConnected, AppConstants.Assets.TestWaveResourcePath);
        }

        public async Task<bool> PlayWaveAsync(bool controllerConnected, string resourcePath)
        {
            string trackName = GetTrackName(resourcePath);
            ThrowIfDisposed();

            if (!controllerConnected)
            {
                RaiseStatus(DualSenseAudioState.Stopped, UiMessageCatalog.Audio.UsbRequired);
                AppLogger.Warn(nameof(DualSenseAudioService), "Demande de lecture {trackName} refusée : manette non connectée.");
                return false;
            }

            AppLogger.Info(nameof(DualSenseAudioService), "Demande de lecture audio : {trackName}");

            if (!ApplySpeakerVolumeToInternalSpeaker(showErrorDialog: true))
                return false;

            try
            {
                DualSenseSpeakerTestResult result = await _speakerTestPlayer.PlayOrResumeWaveAsync(resourcePath);

                if (!result.Success)
                {
                    if (!HasAnyActiveAudio())
                        RestoreSpeakerRoutingIfPossible(controllerConnected);

                    _currentWaveResourcePath = null;
                    _currentVisualizerMode = AudioPlaybackMode.None;
                    RaiseAudioFrame(0.0f, AudioPlaybackMode.None);
                    RaiseStatus(DualSenseAudioState.Stopped, result.Message, result.DeviceName, AudioPlaybackMode.None);
                    return false;
                }

                if (_speakerTestPlayer.IsPlaying)
                {
                    _currentWaveResourcePath = resourcePath;
                    _currentVisualizerMode = AudioPlaybackMode.Wave;
                    await Task.Delay(AppConstants.Audio.PostPlaybackRoutingRefreshDelayMs);
                    ApplySpeakerVolumeToInternalSpeaker(showErrorDialog: false);

                    AppLogger.Info(nameof(DualSenseAudioService), $"Lecture {trackName} active sur : {result.DeviceName}");
                    RaiseStatus(
                        DualSenseAudioState.Playing,
                        UiMessageCatalog.Audio.WavePlaying(trackName, result.DeviceName!),
                        result.DeviceName,
                        AudioPlaybackMode.Wave,
                        trackName);
                }
                else
                {
                    _currentWaveResourcePath = resourcePath;
                    _currentVisualizerMode = AudioPlaybackMode.Wave;
                    AppLogger.Info(nameof(DualSenseAudioService), $"Lecture {trackName} en pause sur : {result.DeviceName}");
                    RaiseAudioFrame(0.0f, AudioPlaybackMode.Wave);
                    RaiseStatus(DualSenseAudioState.Paused, UiMessageCatalog.Audio.WavePaused(trackName), result.DeviceName, AudioPlaybackMode.Wave, trackName);
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(DualSenseAudioService), "Echec de la lecture {trackName}.", ex);

                if (!HasAnyActiveAudio())
                    RestoreSpeakerRoutingIfPossible(controllerConnected);

                _currentWaveResourcePath = null;
                _currentVisualizerMode = AudioPlaybackMode.None;
                RaiseAudioFrame(0.0f, AudioPlaybackMode.None);
                RaiseStatus(DualSenseAudioState.Stopped, UiMessageCatalog.Audio.WavePlaybackFailed(trackName), mode: AudioPlaybackMode.None, trackName: trackName);
                return false;
            }
        }

        public async Task<bool> PlayIndependentBeepSequenceAsync(bool controllerConnected)
        {
            ThrowIfDisposed();

            if (!controllerConnected)
            {
                RaiseStatus(DualSenseAudioState.Stopped, UiMessageCatalog.Audio.UsbRequired);
                AppLogger.Warn(nameof(DualSenseAudioService), "Demande de sequence de bips refusée : manette non connectée.");
                return false;
            }

            if (!ApplySpeakerVolumeToInternalSpeaker(showErrorDialog: true))
                return false;

            Interlocked.Increment(ref _activeBeepSequences);
            AppLogger.Info(nameof(DualSenseAudioService), $"Nouvelle sequence de bips. Actives={_activeBeepSequences}");
            RaiseAudioFrame(0.25f, AudioPlaybackMode.Beep);
            RaiseStatus(DualSenseAudioState.Playing, UiMessageCatalog.Audio.BeepStarted(), mode: AudioPlaybackMode.Beep);

            _ = Task.Run(async () =>
            {
                try
                {
                    DualSenseSpeakerTestResult result = await DualSenseSpeakerTestPlayer.PlayIndependentBeepSequenceAsync(
                        level => RaiseAudioFrame(level, AudioPlaybackMode.Beep));

                    if (!result.Success)
                    {
                        AppLogger.Warn(nameof(DualSenseAudioService), result.Message);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(nameof(DualSenseAudioService), "Echec d'une sequence de bips.", ex);
                }
                finally
                {
                    int remaining = Interlocked.Decrement(ref _activeBeepSequences);
                    AppLogger.Info(nameof(DualSenseAudioService), $"Sequence de bips terminee. Restantes={remaining}");

                    if (remaining <= 0 && _currentVisualizerMode != AudioPlaybackMode.Wave)
                    {
                        RaiseAudioFrame(0.0f, AudioPlaybackMode.None);
                        RaiseStatus(DualSenseAudioState.Stopped, UiMessageCatalog.Audio.NoPlayback, mode: AudioPlaybackMode.None);
                    }

                    if (!_isShuttingDown && !HasAnyActiveAudio())
                    {
                        RestoreSpeakerRoutingIfPossible(controllerConnected: true);
                    }
                }
            });

            return true;
        }

        public void Pause()
        {
            string trackName = GetCurrentTrackName();
            ThrowIfDisposed();

            AppLogger.Info(nameof(DualSenseAudioService), "Demande de pause audio.");
            DualSenseSpeakerTestResult result = _speakerTestPlayer.Pause();

            if (result.Success)
            {
                _currentVisualizerMode = AudioPlaybackMode.Wave;
                RaiseAudioFrame(0.0f, AudioPlaybackMode.Wave);
                RaiseStatus(DualSenseAudioState.Paused, UiMessageCatalog.Audio.WavePaused(trackName), result.DeviceName, AudioPlaybackMode.Wave, trackName);
            }
            else
            {
                _currentVisualizerMode = AudioPlaybackMode.None;
                RaiseAudioFrame(0.0f, AudioPlaybackMode.None);
                RaiseStatus(DualSenseAudioState.Stopped, result.Message, result.DeviceName, AudioPlaybackMode.None);
            }
        }

        public void Stop(
            bool controllerConnected,
            bool restoreRouting,
            string statusMessage = UiMessageCatalog.Audio.NoPlayback)
        {
            ThrowIfDisposed();
            string trackName = GetCurrentTrackName();
            AppLogger.Info(nameof(DualSenseAudioService), $"Demande d'arrêt audio {trackName}. restoreRouting={restoreRouting}");
            StopCore(controllerConnected, restoreRouting, statusMessage, raiseStatus: true);
        }

        public void HandleControllerDisconnected()
        {
            ThrowIfDisposed();
            AppLogger.Warn(nameof(DualSenseAudioService), "Arrêt audio forcé : manette déconnectée.");
            StopCore(
                controllerConnected: false,
                restoreRouting: false,
                statusMessage: UiMessageCatalog.Audio.NoPlayback,
                raiseStatus: true);
        }

        public void Shutdown(bool controllerConnected)
        {
            if (_isDisposed || _isShuttingDown)
                return;

            _isShuttingDown = true;
            AppLogger.Info(nameof(DualSenseAudioService), "Extinction du service audio.");

            StopCore(
                controllerConnected,
                restoreRouting: false,
                statusMessage: UiMessageCatalog.Audio.NoPlayback,
                raiseStatus: false);

            RestoreControllerHardwareState(controllerConnected);

            _speakerTestPlayer.PlaybackEnded -= SpeakerTestPlayer_PlaybackEnded;
            _speakerTestPlayer.AudioLevelChanged -= SpeakerTestPlayer_AudioLevelChanged;
            _speakerTestPlayer.PlaybackProgressChanged -= SpeakerTestPlayer_PlaybackProgressChanged;

            _speakerTestPlayer.Dispose();
            DisposeHidAudioSession();
        }

        private void StopCore(
            bool controllerConnected,
            bool restoreRouting,
            string statusMessage,
            bool raiseStatus)
        {
            if (_speakerTestPlayer.HasPlaybackSession)
            {
                DualSenseSpeakerTestResult result = _speakerTestPlayer.Stop();
                if (!result.Success)
                {
                    AppLogger.Warn(nameof(DualSenseAudioService), result.Message);
                }
            }

            _currentWaveResourcePath = null;
            _currentVisualizerMode = AudioPlaybackMode.None;
            RaiseAudioFrame(0.0f, AudioPlaybackMode.None);

            if (restoreRouting && !HasAnyActiveAudio())
                RestoreSpeakerRoutingIfPossible(controllerConnected);
            else if (!HasAnyActiveAudio())
                DisposeHidAudioSession();

            if (raiseStatus)
            {
                RaiseStatus(DualSenseAudioState.Stopped, statusMessage, mode: AudioPlaybackMode.None);
            }
        }

        private void SpeakerTestPlayer_PlaybackEnded(object? sender, EventArgs e)
        {
            if (_isShuttingDown)
                return;

            string trackName = GetCurrentTrackName();
            AppLogger.Info(nameof(DualSenseAudioService), "Fin de lecture {trackName} détectée.");

            _currentWaveResourcePath = null;
            _currentVisualizerMode = AudioPlaybackMode.None;
            RaiseAudioFrame(0.0f, AudioPlaybackMode.None);

            if (!HasAnyActiveAudio())
                RestoreSpeakerRoutingIfPossible(controllerConnected: true);

            RaiseStatus(DualSenseAudioState.Stopped, UiMessageCatalog.Audio.NoPlayback, mode: AudioPlaybackMode.None);
        }

        private void SpeakerTestPlayer_AudioLevelChanged(object? sender, float level)
        {
            if (_isShuttingDown)
                return;

            _currentVisualizerMode = AudioPlaybackMode.Wave;
            RaiseAudioFrame(level, AudioPlaybackMode.Wave);
        }

        private void SpeakerTestPlayer_PlaybackProgressChanged(object? sender, PlaybackProgressChangedEventArgs e)
        {
            if (_isShuttingDown)
                return;

            PlaybackProgressChanged?.Invoke(this, e);
        }

        private bool HasAnyActiveAudio()
        {
            return _speakerTestPlayer.HasPlaybackSession || Volatile.Read(ref _activeBeepSequences) > 0;
        }

        private bool ApplySpeakerVolumeToInternalSpeaker(bool showErrorDialog)
        {
            if (_hidAudioControl.TryRouteToInternalSpeaker(_speakerVolume, GetSpeakerPreamp(), out string error))
            {
                _lastAppliedSpeakerVolume = _speakerVolume;
                return true;
            }

            AppLogger.Warn(nameof(DualSenseAudioService), error);

            if (showErrorDialog)
            {
                RaiseStatus(DualSenseAudioState.Stopped, error, mode: AudioPlaybackMode.None);
            }

            return false;
        }

        private void RestoreSpeakerRoutingIfPossible(bool controllerConnected)
        {
            if (!controllerConnected && !_hidAudioControl.IsConnected)
            {
                DisposeHidAudioSession();
                return;
            }

            if (!_hidAudioControl.TryRouteToHeadphones(out string error))
            {
                AppLogger.Warn(nameof(DualSenseAudioService), error);
            }

            DisposeHidAudioSession();
        }

        private void RestoreControllerHardwareState(bool controllerConnected)
        {
            if (!controllerConnected && !_hidAudioControl.IsConnected)
            {
                DisposeHidAudioSession();
                return;
            }

            if (!_hidAudioControl.TryRestoreControllerDefaults(out string error))
            {
                AppLogger.Warn(nameof(DualSenseAudioService), error);
            }

            DisposeHidAudioSession();
        }

        private void DisposeHidAudioSession()
        {
            _hidAudioControl.Dispose();
            _lastAppliedSpeakerVolume = null;
        }

        private byte GetSpeakerPreamp()
        {
            if (_speakerVolume >= AppConstants.Audio.SpeakerPreampThresholdMax)
                return AppConstants.Audio.SpeakerPreampMax;

            if (_speakerVolume >= AppConstants.Audio.SpeakerPreampThresholdHigh)
                return AppConstants.Audio.SpeakerPreampHigh;

            if (_speakerVolume >= AppConstants.Audio.SpeakerPreampThresholdMedium)
                return AppConstants.Audio.SpeakerPreampMediumHigh;

            if (_speakerVolume >= AppConstants.Audio.SpeakerPreampThresholdMediumLow)
                return AppConstants.Audio.SpeakerPreampMedium;

            if (_speakerVolume >= AppConstants.Audio.SpeakerPreampThresholdLow)
                return AppConstants.Audio.SpeakerPreampMediumLow;

            if (_speakerVolume > 0)
                return AppConstants.Audio.SpeakerPreampLow;

            return AppConstants.Audio.SpeakerPreampMute;
        }

        private static byte ConvertOverlayVolumeToSpeakerVolume(double volume)
        {
            double normalized = Math.Clamp(
                volume / AppConstants.Audio.OverlaySliderMaxValue,
                0.0,
                1.0);

            if (normalized <= 0.0)
                return 0;

            double curved = Math.Pow(normalized, AppConstants.Audio.VolumeCurveExponent);

            int mapped = (int)Math.Round(
                AppConstants.Audio.AudibleFloorVolume +
                (curved * (AppConstants.Audio.SpeakerVolumeMax - AppConstants.Audio.AudibleFloorVolume)));

            return (byte)Math.Clamp(mapped, 0, AppConstants.Audio.SpeakerVolumeMax);
        }

        private void RaiseStatus(
            DualSenseAudioState state,
            string message,
            string? deviceName = null,
            AudioPlaybackMode mode = AudioPlaybackMode.None,
            string? trackName = null)
        {
            StatusChanged?.Invoke(this, new DualSenseAudioStatusChangedEventArgs(state, message, deviceName, mode, trackName));
        }

        private void RaiseAudioFrame(float level, AudioPlaybackMode mode)
        {
            AudioLevelChanged?.Invoke(this, new AudioVisualizerFrame(level, mode));
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DualSenseAudioService));
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
