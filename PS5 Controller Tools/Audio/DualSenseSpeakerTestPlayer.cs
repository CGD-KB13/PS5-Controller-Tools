using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using System.Windows;
using System.Windows.Resources;
using System.Windows.Threading;

namespace PS5_Controller_Tools
{
    public sealed class DualSenseSpeakerTestPlayer : IDisposable
    {
        private WasapiOut? _output;
        private Stream? _resourceStream;
        private WaveStream? _waveStream;
        private string? _currentDeviceName;
        private string? _currentResourcePath;
        private readonly DispatcherTimer _playbackProgressTimer;
        private bool _isDisposingPlayback;

        public event EventHandler? PlaybackEnded;
        public event EventHandler<float>? AudioLevelChanged;
        public event EventHandler<PlaybackProgressChangedEventArgs>? PlaybackProgressChanged;

        public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _output?.PlaybackState == PlaybackState.Paused;
        public bool HasPlaybackSession => _output != null;
        public TimeSpan CurrentPosition => _waveStream?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _waveStream?.TotalTime ?? TimeSpan.Zero;

        public DualSenseSpeakerTestPlayer()
        {
            _playbackProgressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _playbackProgressTimer.Tick += PlaybackProgressTimer_Tick;
        }

        private static string GetTrackName(string resourcePath)
        {
            return Path.GetFileName(resourcePath);
        }

        public Task<DualSenseSpeakerTestResult> PlayOrResumeWaveAsync(string resourcePath)
        {
            string trackName = GetTrackName(resourcePath);
            bool sameTrackRequested = string.Equals(_currentResourcePath, resourcePath, StringComparison.OrdinalIgnoreCase);

            if (_output != null && IsPaused && sameTrackRequested)
            {
                _output.Play();
                _playbackProgressTimer.Start();
                RaisePlaybackProgress();
                AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), "Reprise de la lecture {trackName}.");
                return Task.FromResult(DualSenseSpeakerTestResult.Ok(_currentDeviceName ?? "DualSense"));
            }

            if (_output != null && IsPlaying && sameTrackRequested)
            {
                return Task.FromResult(DualSenseSpeakerTestResult.Ok(_currentDeviceName ?? "DualSense"));
            }

            StopAndDisposePlayback();

            using var enumerator = new MMDeviceEnumerator();

            MMDevice? device = DualSenseRenderDeviceSelector.SelectBestActiveDevice(
                enumerator,
                out string diagnostics);

            AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), diagnostics);

            if (device == null)
            {
                return Task.FromResult(DualSenseSpeakerTestResult.Fail(UiMessageCatalog.Audio.NoDualSenseDevice));
            }

            try
            {
                WaveFormat outputFormat = device.AudioClient.MixFormat;
                ISampleProvider source = BuildWaveSampleProvider(resourcePath, outputFormat.SampleRate);

                var provider = new RightChannelWaveProvider(outputFormat, source, level =>
                {
                    AudioLevelChanged?.Invoke(this, level);
                });

                _output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
                _output.PlaybackStopped += Output_PlaybackStopped;
                _output.Init(provider);
                _output.Play();
                _playbackProgressTimer.Start();

                _currentDeviceName = device.FriendlyName;
                _currentResourcePath = resourcePath;
                RaisePlaybackProgress();
                AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), $"Lecture {trackName} initialisee sur : {device.FriendlyName}");

                return Task.FromResult(DualSenseSpeakerTestResult.Ok(device.FriendlyName));
            }
            catch (Exception ex)
            {
                StopAndDisposePlayback();
                AppLogger.Error(nameof(DualSenseSpeakerTestPlayer), "Impossible de lancer {trackName}.", ex);
                return Task.FromResult(DualSenseSpeakerTestResult.Fail(UiMessageCatalog.Audio.WaveStartFailed(trackName, ex.Message)));
            }
        }

        public static async Task<DualSenseSpeakerTestResult> PlayIndependentBeepSequenceAsync(Action<float>? levelCallback = null)
        {
            using var enumerator = new MMDeviceEnumerator();

            MMDevice? device = DualSenseRenderDeviceSelector.SelectBestActiveDevice(
                enumerator,
                out string diagnostics);

            AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), diagnostics);

            if (device == null)
            {
                return DualSenseSpeakerTestResult.Fail(UiMessageCatalog.Audio.NoDualSenseDevice);
            }

            WaveFormat outputFormat = device.AudioClient.MixFormat;
            ISampleProvider source = BuildBeepSequenceProvider(outputFormat.SampleRate);
            var provider = new RightChannelWaveProvider(outputFormat, source, levelCallback);

            using var output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
            var tcs = new TaskCompletionSource<StoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnPlaybackStopped(object? sender, StoppedEventArgs e)
            {
                tcs.TrySetResult(e);
            }

            output.PlaybackStopped += OnPlaybackStopped;

            try
            {
                output.Init(provider);
                output.Play();
                AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), $"Lecture sequence de bips sur : {device.FriendlyName}");

                StoppedEventArgs stoppedArgs = await tcs.Task.ConfigureAwait(false);

                if (stoppedArgs.Exception != null)
                {
                    AppLogger.Error(nameof(DualSenseSpeakerTestPlayer), "Lecture des bips interrompue avec erreur.", stoppedArgs.Exception);
                    return DualSenseSpeakerTestResult.Fail(UiMessageCatalog.Audio.BeepStartFailed(stoppedArgs.Exception.Message));
                }

                return DualSenseSpeakerTestResult.Ok(device.FriendlyName);
            }
            finally
            {
                output.PlaybackStopped -= OnPlaybackStopped;
            }
        }

        public DualSenseSpeakerTestResult Pause()
        {
            if (_output == null || !IsPlaying)
            {
                return DualSenseSpeakerTestResult.Fail(UiMessageCatalog.Audio.NoPlayback);
            }

            _output.Pause();
            _playbackProgressTimer.Stop();
            AudioLevelChanged?.Invoke(this, 0.0f);
            RaisePlaybackProgress();
            AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), "Lecture audio mise en pause.");
            return DualSenseSpeakerTestResult.Ok(_currentDeviceName ?? "DualSense");
        }

        public DualSenseSpeakerTestResult Stop()
        {
            if (_output == null)
            {
                return DualSenseSpeakerTestResult.Fail(UiMessageCatalog.Audio.NoPlayback);
            }

            _output.PlaybackStopped -= Output_PlaybackStopped;
            _output.Stop();

            string deviceName = _currentDeviceName ?? "DualSense";
            StopAndDisposePlayback();
            AppLogger.Info(nameof(DualSenseSpeakerTestPlayer), "Lecture audio arretee.");

            return DualSenseSpeakerTestResult.Ok(deviceName);
        }

        private void PlaybackProgressTimer_Tick(object? sender, EventArgs e)
        {
            RaisePlaybackProgress();
        }

        private void RaisePlaybackProgress()
        {
            PlaybackProgressChanged?.Invoke(
                this,
                new PlaybackProgressChangedEventArgs(
                    _waveStream?.CurrentTime ?? TimeSpan.Zero,
                    _waveStream?.TotalTime ?? TimeSpan.Zero));
        }

        private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_output == null)
                return;

            if (e.Exception != null)
            {
                AppLogger.Error(nameof(DualSenseSpeakerTestPlayer), "Lecture audio interrompue avec erreur.", e.Exception);
            }

            StopAndDisposePlayback();
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        }

        private void StopAndDisposePlayback()
        {
            if (_isDisposingPlayback)
                return;

            _isDisposingPlayback = true;

            try
            {
                _playbackProgressTimer.Stop();
                AudioLevelChanged?.Invoke(this, 0.0f);
                PlaybackProgressChanged?.Invoke(this, new PlaybackProgressChangedEventArgs(TimeSpan.Zero, TimeSpan.Zero));

                if (_output != null)
                {
                    _output.PlaybackStopped -= Output_PlaybackStopped;

                    try
                    {
                        if (_output.PlaybackState != PlaybackState.Stopped)
                            _output.Stop();
                    }
                    catch
                    {
                    }

                    _output.Dispose();
                    _output = null;
                }

                _waveStream?.Dispose();
                _waveStream = null;

                _resourceStream?.Dispose();
                _resourceStream = null;

                _currentDeviceName = null;
                _currentResourcePath = null;
            }
            finally
            {
                _isDisposingPlayback = false;
            }
        }

        private ISampleProvider BuildWaveSampleProvider(string resourcePath, int outputSampleRate)
        {
            _resourceStream = OpenWaveInputStream(resourcePath);
            _waveStream = new WaveFileReader(_resourceStream);

            ISampleProvider provider = _waveStream.ToSampleProvider();

            if (provider.WaveFormat.SampleRate != outputSampleRate)
            {
                provider = new WdlResamplingSampleProvider(provider, outputSampleRate);
            }

            return provider;
        }

        private static Stream OpenWaveInputStream(string resourcePath)
        {
            Stream? resourceStream = TryOpenApplicationResource(resourcePath);
            if (resourceStream != null)
                return resourceStream;

            Stream? fileStream = TryOpenFileSystemPath(resourcePath);
            if (fileStream != null)
                return fileStream;

            throw new FileNotFoundException($"Ressource introuvable : {resourcePath}");
        }

        private static Stream? TryOpenApplicationResource(string resourcePath)
        {
            try
            {
                var uri = new Uri(resourcePath, UriKind.RelativeOrAbsolute);
                StreamResourceInfo? resourceInfo = Application.GetResourceStream(uri);
                return resourceInfo?.Stream;
            }
            catch
            {
                return null;
            }
        }

        private static Stream? TryOpenFileSystemPath(string resourcePath)
        {
            foreach (string candidatePath in GetCandidateFilePaths(resourcePath))
            {
                if (!File.Exists(candidatePath))
                    continue;

                return File.Open(candidatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            return null;
        }

        private static string[] GetCandidateFilePaths(string resourcePath)
        {
            string normalized = resourcePath
                .Trim()
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(resourcePath) && File.Exists(resourcePath))
                return new[] { resourcePath };

            string baseDir = AppContext.BaseDirectory;
            string currentDir = Environment.CurrentDirectory;

            return new[]
            {
        Path.Combine(baseDir, normalized),
        Path.Combine(currentDir, normalized),
        resourcePath
    };
        }

        private static ISampleProvider BuildBeepSequenceProvider(int sampleRate)
        {
            const double beepFrequencyHz = 880.0;
            const double beepDurationSeconds = 0.15;
            const double silenceBetweenBeepsSeconds = 0.35;

            var providers = new System.Collections.Generic.List<ISampleProvider>();

            for (int i = 0; i < 3; i++)
            {
                var beep = new SignalGenerator(sampleRate, 1)
                {
                    Gain = 0.35,
                    Frequency = beepFrequencyHz,
                    Type = SignalGeneratorType.Sin
                }.Take(TimeSpan.FromSeconds(beepDurationSeconds));

                providers.Add(beep);

                if (i < 2)
                {
                    providers.Add(new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1))
                        .ToSampleProvider()
                        .Take(TimeSpan.FromSeconds(silenceBetweenBeepsSeconds)));
                }
            }

            return new ConcatenatingSampleProvider(providers);
        }

        private static WaveFormat GetSampleFormatForChecks(WaveFormat format)
        {
            if (format is WaveFormatExtensible extensible)
            {
                return extensible.ToStandardWaveFormat();
            }

            return format;
        }

        private static bool SupportsWaveFormat(WaveFormat format)
        {
            WaveFormat sampleFormat = GetSampleFormatForChecks(format);

            bool float32 = sampleFormat.Encoding == WaveFormatEncoding.IeeeFloat && sampleFormat.BitsPerSample == 32;
            bool pcm16 = sampleFormat.Encoding == WaveFormatEncoding.Pcm && sampleFormat.BitsPerSample == 16;

            return (float32 || pcm16) && format.Channels >= 1;
        }

        private sealed class RightChannelWaveProvider : IWaveProvider
        {
            private readonly WaveFormat _outputFormat;
            private readonly ISampleProvider _source;
            private readonly int _outputChannels;
            private readonly int _sourceChannels;
            private readonly int _bytesPerFrame;
            private float[] _sourceBuffer = Array.Empty<float>();

            private readonly Action<float>? _levelCallback;

            public RightChannelWaveProvider(
                WaveFormat outputFormat,
                ISampleProvider source,
                Action<float>? levelCallback)
            {
                _outputFormat = outputFormat ?? throw new ArgumentNullException(nameof(outputFormat));
                _source = source ?? throw new ArgumentNullException(nameof(source));
                _levelCallback = levelCallback;
                _outputChannels = _outputFormat.Channels;
                _sourceChannels = _source.WaveFormat.Channels;
                _bytesPerFrame = _outputFormat.BlockAlign;

                if (!SupportsWaveFormat(_outputFormat))
                {
                    throw new NotSupportedException(
                        $"Format audio de sortie non pris en charge : {_outputFormat.Encoding} / {_outputFormat.BitsPerSample} bits / {_outputFormat.Channels} canal(aux).");
                }

                if (_source.WaveFormat.SampleRate != _outputFormat.SampleRate)
                {
                    throw new InvalidOperationException("Le sample rate source ne correspond pas au sample rate de sortie.");
                }
            }

            public WaveFormat WaveFormat => _outputFormat;

            public int Read(byte[] buffer, int offset, int count)
            {
                int framesRequested = count / _bytesPerFrame;

                if (framesRequested <= 0)
                {
                    return 0;
                }

                int sourceSamplesRequested = framesRequested * _sourceChannels;

                if (_sourceBuffer.Length < sourceSamplesRequested)
                {
                    _sourceBuffer = new float[sourceSamplesRequested];
                }

                int sourceSamplesRead = _source.Read(_sourceBuffer, 0, sourceSamplesRequested);
                int framesRead = sourceSamplesRead / _sourceChannels;

                if (framesRead > 0)
                {
                    float peak = 0.0f;

                    for (int frame = 0; frame < framesRead; frame++)
                    {
                        float sample = GetSourceSample(frame);
                        float abs = Math.Abs(sample);
                        if (abs > peak)
                            peak = abs;
                    }

                    _levelCallback?.Invoke(peak);
                }
                else
                {
                    _levelCallback?.Invoke(0.0f);
                }

                if (framesRead <= 0)
                {
                    return 0;
                }

                WaveFormat sampleFormat = GetSampleFormatForChecks(_outputFormat);

                if (sampleFormat.Encoding == WaveFormatEncoding.IeeeFloat && sampleFormat.BitsPerSample == 32)
                {
                    WriteFloatFrames(buffer, offset, framesRead);
                }
                else if (sampleFormat.Encoding == WaveFormatEncoding.Pcm && sampleFormat.BitsPerSample == 16)
                {
                    WritePcm16Frames(buffer, offset, framesRead);
                }
                else
                {
                    throw new NotSupportedException(
                        $"Format audio non pris en charge : {sampleFormat.Encoding} / {sampleFormat.BitsPerSample} bits / {_outputFormat.Channels} canal(aux).");
                }

                return framesRead * _bytesPerFrame;
            }

            private void WriteFloatFrames(byte[] buffer, int offset, int framesToWrite)
            {
                int rightChannelIndex = _outputChannels > 1 ? 1 : 0;

                for (int frame = 0; frame < framesToWrite; frame++)
                {
                    float sample = GetSourceSample(frame);

                    for (int channel = 0; channel < _outputChannels; channel++)
                    {
                        float value = channel == rightChannelIndex ? sample : 0.0f;
                        byte[] bytes = BitConverter.GetBytes(value);
                        Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
                        offset += bytes.Length;
                    }
                }
            }

            private void WritePcm16Frames(byte[] buffer, int offset, int framesToWrite)
            {
                int rightChannelIndex = _outputChannels > 1 ? 1 : 0;

                for (int frame = 0; frame < framesToWrite; frame++)
                {
                    short sample = (short)Math.Round(GetSourceSample(frame) * short.MaxValue);

                    for (int channel = 0; channel < _outputChannels; channel++)
                    {
                        short value = channel == rightChannelIndex ? sample : (short)0;
                        byte[] bytes = BitConverter.GetBytes(value);
                        Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
                        offset += bytes.Length;
                    }
                }
            }

            private float GetSourceSample(int frameIndex)
            {
                int baseIndex = frameIndex * _sourceChannels;

                float sample = _sourceChannels <= 1
                    ? _sourceBuffer[baseIndex]
                    : _sourceBuffer[baseIndex + 1];

                if (sample > 1.0f) return 1.0f;
                if (sample < -1.0f) return -1.0f;
                return sample;
            }
        }

        public void Dispose()
        {
            _playbackProgressTimer.Stop();
            _playbackProgressTimer.Tick -= PlaybackProgressTimer_Tick;
            StopAndDisposePlayback();
        }
    }
    public sealed class PlaybackProgressChangedEventArgs : EventArgs
    {
        public TimeSpan Position { get; }
        public TimeSpan Duration { get; }

        public PlaybackProgressChangedEventArgs(TimeSpan position, TimeSpan duration)
        {
            Position = position;
            Duration = duration;
        }
    }

    public sealed record DualSenseSpeakerTestResult(bool Success, string Message, string? DeviceName = null)
    {
        public static DualSenseSpeakerTestResult Ok(string deviceName) =>
            new(true, UiMessageCatalog.Audio.ActivePlayback, deviceName);

        public static DualSenseSpeakerTestResult Fail(string message) =>
            new(false, message);
    }
}
