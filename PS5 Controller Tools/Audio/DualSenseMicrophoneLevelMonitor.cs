using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PS5_Controller_Tools.Audio
{
    internal sealed class DualSenseMicrophoneLevelMonitor : IDisposable
    {
        private MMDeviceEnumerator? _enumerator;
        private MMDevice? _device;
        private WasapiCapture? _capture;
        private bool _isDisposed;
        private string? _currentDeviceName;

        public event EventHandler<float>? LevelChanged;

        public bool IsRunning => _capture != null;
        public string CurrentDeviceName => _currentDeviceName ?? "DualSense";

        public bool Start(out string? errorMessage)
        {
            errorMessage = null;

            if (_isDisposed)
            {
                errorMessage = "Le moniteur micro est déjà libéré.";
                return false;
            }

            if (_capture != null)
                return true;

            try
            {
                _enumerator = new MMDeviceEnumerator();
                _device = DualSenseCaptureDeviceSelector.SelectBestActiveDevice(_enumerator, out string diagnostics);
                AppLogger.Info(nameof(DualSenseMicrophoneLevelMonitor), diagnostics);

                if (_device == null)
                {
                    errorMessage = "Aucun microphone DualSense compatible n'a été trouvé.";
                    Stop();
                    return false;
                }

                _capture = new WasapiCapture(_device);
                _capture.DataAvailable += Capture_DataAvailable;
                _capture.RecordingStopped += Capture_RecordingStopped;
                _capture.StartRecording();

                _currentDeviceName = _device.FriendlyName;
                AppLogger.Info(nameof(DualSenseMicrophoneLevelMonitor), $"Capture micro démarrée sur : {_currentDeviceName}");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                AppLogger.Error(nameof(DualSenseMicrophoneLevelMonitor), "Impossible de démarrer la capture micro.", ex);
                Stop();
                return false;
            }
        }

        public void Stop()
        {
            if (_capture != null)
            {
                _capture.DataAvailable -= Capture_DataAvailable;
                _capture.RecordingStopped -= Capture_RecordingStopped;

                try
                {
                    _capture.StopRecording();
                }
                catch
                {
                }

                _capture.Dispose();
                _capture = null;
            }

            _device?.Dispose();
            _device = null;

            _enumerator?.Dispose();
            _enumerator = null;

            _currentDeviceName = null;
            LevelChanged?.Invoke(this, 0f);
        }

        private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_capture == null || e.BytesRecorded <= 0)
                return;

            float level = ComputePeakLevel(e.Buffer, e.BytesRecorded, _capture.WaveFormat);
            LevelChanged?.Invoke(this, level);
        }

        private void Capture_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                AppLogger.Error(nameof(DualSenseMicrophoneLevelMonitor), "Capture micro interrompue avec erreur.", e.Exception);
            }

            LevelChanged?.Invoke(this, 0f);
        }

        private static float ComputePeakLevel(byte[] buffer, int bytesRecorded, WaveFormat waveFormat)
        {
            if (waveFormat.BitsPerSample == 16)
                return ComputePeak16(buffer, bytesRecorded, waveFormat.BlockAlign);

            if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat && waveFormat.BitsPerSample == 32)
                return ComputePeakFloat32(buffer, bytesRecorded, waveFormat.BlockAlign);

            return 0f;
        }

        private static float ComputePeak16(byte[] buffer, int bytesRecorded, int blockAlign)
        {
            if (blockAlign <= 0)
                return 0f;

            float peak = 0f;

            for (int index = 0; index + 1 < bytesRecorded; index += 2)
            {
                short sample = BitConverter.ToInt16(buffer, index);
                float value = Math.Abs(sample / 32768f);
                if (value > peak)
                    peak = value;
            }

            return Math.Clamp(peak, 0f, 1f);
        }

        private static float ComputePeakFloat32(byte[] buffer, int bytesRecorded, int blockAlign)
        {
            if (blockAlign <= 0)
                return 0f;

            float peak = 0f;

            for (int index = 0; index + 3 < bytesRecorded; index += 4)
            {
                float sample = BitConverter.ToSingle(buffer, index);
                float value = Math.Abs(sample);
                if (value > peak)
                    peak = value;
            }

            return Math.Clamp(peak, 0f, 1f);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _isDisposed = true;
        }
    }
}
