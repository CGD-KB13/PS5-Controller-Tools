using HidSharp;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PS5_Controller_Tools
{
    internal sealed class DualSenseCommunicationSniffer : IDisposable
    {
        private const int SnapshotIntervalMs = 100;
        private const int HidReadTimeoutMs = 100;

        private readonly object _syncRoot = new();

        private HidDevice? _device;
        private HidStream? _stream;
        private CancellationTokenSource? _readLoopCancellation;
        private Task? _readLoopTask;
        private DateTime _nextSnapshotUtc = DateTime.MinValue;
        private bool _isDisposed;

        private string? _lastHidLine;
        private string? _lastSdlLine;

        public event EventHandler<string>? HidLineCaptured;
        public event EventHandler<string>? SdlLineCaptured;
        public event EventHandler<string>? StatusChanged;

        public bool IsRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return _stream != null && _readLoopCancellation != null;
                }
            }
        }

        public bool Start(out string error)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();

                error = string.Empty;
                if (_stream != null)
                    return true;

                DisposeStreamOnly();
                _lastHidLine = null;
                _lastSdlLine = null;

                HidDevice? device = DeviceList.Local
                    .GetHidDevices(AppConstants.Hid.SonyVendorId, AppConstants.Hid.DualSenseProductId)
                    .Where(d => d.GetMaxInputReportLength() > 0)
                    .OrderByDescending(d => d.GetMaxInputReportLength())
                    .FirstOrDefault();

                if (device == null)
                {
                    error = "Aucune interface HID DualSense lisible n'a ete trouvee.";
                    AppLogger.Warn(nameof(DualSenseCommunicationSniffer), error);
                    return false;
                }

                try
                {
                    HidStream stream = device.Open();
                    stream.ReadTimeout = HidReadTimeoutMs;

                    _device = device;
                    _stream = stream;
                    _readLoopCancellation = new CancellationTokenSource();
                    _nextSnapshotUtc = DateTime.MinValue;
                    _readLoopTask = Task.Run(() => ReadLoop(_readLoopCancellation.Token));

                    string productName = SafeText(device.ProductName, "DualSense");
                    string startMessage =
                        $"Sniffer actif sur {productName} | InputReportLength={device.GetMaxInputReportLength()} | Path={device.DevicePath}";

                    AppLogger.Info(nameof(DualSenseCommunicationSniffer), startMessage);
                    RaiseStatus(startMessage);
                    RaiseHidLine("=== Sniffer demarre ===");
                    RaiseHidLine($"Device  : {productName}");
                    RaiseHidLine($"Path    : {device.DevicePath}");
                    RaiseHidLine($"Input   : {device.GetMaxInputReportLength()} octets max");
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"Ouverture du sniffer impossible : {ex.Message}";
                    AppLogger.Error(nameof(DualSenseCommunicationSniffer), error, ex);
                    DisposeStreamOnly();
                    return false;
                }
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cancellation;
            Task? readLoopTask;

            lock (_syncRoot)
            {
                if (_stream == null && _readLoopCancellation == null)
                    return;

                cancellation = _readLoopCancellation;
                readLoopTask = _readLoopTask;

                _readLoopCancellation = null;
                _readLoopTask = null;

                try
                {
                    cancellation?.Cancel();
                }
                catch
                {
                }

                DisposeStreamOnly();
            }

            if (readLoopTask != null)
            {
                try
                {
                    readLoopTask.Wait(500);
                }
                catch
                {
                }
            }

            cancellation?.Dispose();
            RaiseStatus("Sniffer desactive.");
        }

        public void CaptureSnapshot(ControllerStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (!IsRunning)
                return;

            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc < _nextSnapshotUtc)
                return;

            _nextSnapshotUtc = nowUtc.AddMilliseconds(SnapshotIntervalMs);

            string pressedButtons = snapshot.PressedButtons.Count == 0
                ? "none"
                : string.Join(",", snapshot.PressedButtons
                    .Select(button => button.ToString().Replace("SDL_CONTROLLER_BUTTON_", string.Empty))
                    .OrderBy(name => name, StringComparer.Ordinal));

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "SDL  BTN=[{0}] TOUCH={1} MIC={2} LT={3:0.00} RT={4:0.00} LX={5:+0.00;-0.00;+0.00} LY={6:+0.00;-0.00;+0.00} RX={7:+0.00;-0.00;+0.00} RY={8:+0.00;-0.00;+0.00}",
                pressedButtons,
                snapshot.IsTouchPadPressed ? 1 : 0,
                snapshot.IsMicMuted ? 1 : 0,
                snapshot.LeftTriggerPressure,
                snapshot.RightTriggerPressure,
                snapshot.LeftStickX,
                snapshot.LeftStickY,
                snapshot.RightStickX,
                snapshot.RightStickY);

            if (string.Equals(line, _lastSdlLine, StringComparison.Ordinal))
                return;

            _lastSdlLine = line;
            RaiseSdlLine(line);
        }

        private void ReadLoop(CancellationToken cancellationToken)
        {
            try
            {
                int maxLength = _device?.GetMaxInputReportLength() ?? 64;
                byte[] buffer = new byte[Math.Max(64, maxLength)];

                while (!cancellationToken.IsCancellationRequested)
                {
                    HidStream? stream;
                    lock (_syncRoot)
                    {
                        stream = _stream;
                    }

                    if (stream == null)
                    {
                        Thread.Sleep(200);
                        continue;
                    }

                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead <= 0)
                            continue;

                        string hexPayload = BitConverter.ToString(buffer, 0, bytesRead).Replace('-', ' ');
                        string line = $"HID  IN  [{bytesRead,2}] {hexPayload}";

                        if (string.Equals(line, _lastHidLine, StringComparison.Ordinal))
                            continue;

                        _lastHidLine = line;
                        RaiseHidLine(line);
                    }
                    catch (Exception)
                    {
                        // relance automatique HID
                        DisposeStreamOnly();
                        Thread.Sleep(300);

                        try
                        {
                            var device = DeviceList.Local
                                .GetHidDevices(AppConstants.Hid.SonyVendorId, AppConstants.Hid.DualSenseProductId)
                                .Where(d => d.GetMaxInputReportLength() > 0)
                                .OrderByDescending(d => d.GetMaxInputReportLength())
                                .FirstOrDefault();

                            if (device != null)
                            {
                                _device = device;
                                _stream = device.Open();
                                _stream.ReadTimeout = HidReadTimeoutMs;
                                RaiseHidLine("=== HID reconnecté ===");
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                RaiseHidLine("=== Sniffer arrete ===");
            }
        }

        private void DisposeStreamOnly()
        {
            try
            {
                _stream?.Dispose();
            }
            catch
            {
            }

            _stream = null;
            _device = null;
        }

        private void RaiseHidLine(string line)
        {
            string timestamped = string.IsNullOrWhiteSpace(line)
                ? string.Empty
                : $"[{DateTime.Now:HH:mm:ss.fff}] {line}";

            try
            {
                HidLineCaptured?.Invoke(this, timestamped);
            }
            catch
            {
            }
        }

        private void RaiseSdlLine(string line)
        {
            string timestamped = string.IsNullOrWhiteSpace(line)
                ? string.Empty
                : $"[{DateTime.Now:HH:mm:ss.fff}] {line}";

            try
            {
                SdlLineCaptured?.Invoke(this, timestamped);
            }
            catch
            {
            }
        }

        private void RaiseStatus(string message)
        {
            try
            {
                StatusChanged?.Invoke(this, message);
            }
            catch
            {
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DualSenseCommunicationSniffer));
        }

        private static string SafeText(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
            }

            Stop();
        }
    }
}
