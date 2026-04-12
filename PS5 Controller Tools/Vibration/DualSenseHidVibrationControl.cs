using HidSharp;

namespace PS5_Controller_Tools.Vibration
{
    internal sealed class DualSenseHidVibrationControl : IDisposable
    {
        private HidDevice? _device;
        private HidStream? _stream;

        public string? DeviceName => _device?.ProductName;
        public string? DevicePath => _device?.DevicePath;
        public bool IsConnected => _stream != null;

        public bool Connect(out string error)
        {
            error = string.Empty;

            if (_stream != null)
                return true;

            Dispose();

            HidDevice? device = DeviceList.Local
                .GetHidDevices(AppConstants.Hid.SonyVendorId, AppConstants.Hid.DualSenseProductId)
                .Where(d => d.GetMaxOutputReportLength() >= AppConstants.Hid.UsbOutputReportLength)
                .OrderByDescending(d => d.GetMaxOutputReportLength())
                .FirstOrDefault();

            if (device == null)
            {
                error = "Aucune interface HID USB DualSense compatible n'a ete trouvee pour la vibration.";
                return false;
            }

            try
            {
                HidStream stream = device.Open();
                stream.WriteTimeout = AppConstants.Hid.HidWriteTimeoutMs;

                _device = device;
                _stream = stream;
                AppLogger.Info(nameof(DualSenseHidVibrationControl), $"Connexion HID vibration ouverte : {device.ProductName}");
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ouverture HID vibration impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidVibrationControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TrySetMotorStrength(byte leftMotorStrength, byte rightMotorStrength, out string error)
        {
            error = string.Empty;

            if (!Connect(out error))
                return false;

            try
            {
                WriteMotorStrengthCore(leftMotorStrength, rightMotorStrength);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ecriture HID vibration impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidVibrationControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TryStop(out string error)
        {
            return TrySetMotorStrength(0x00, 0x00, out error);
        }

        private void WriteMotorStrengthCore(byte leftMotorStrength, byte rightMotorStrength)
        {
            EnsureConnected();

            byte[] report = CreateEmptyUsbOutputReport();
            report[AppConstants.Hid.ReportIndexValidFlag0] =
                (byte)(AppConstants.Hid.ValidFlag0CompatibleVibrationEnable |
                       AppConstants.Hid.ValidFlag0HapticsSelectEnable);
            report[AppConstants.Hid.ReportIndexMotorRight] = rightMotorStrength;
            report[AppConstants.Hid.ReportIndexMotorLeft] = leftMotorStrength;

            WriteReport(report);
        }

        private static byte[] CreateEmptyUsbOutputReport()
        {
            byte[] report = new byte[AppConstants.Hid.UsbOutputReportLength];
            report[AppConstants.Hid.ReportIndexReportId] = AppConstants.Hid.UsbOutputReportId;
            return report;
        }

        private void WriteReport(byte[] report)
        {
            EnsureConnected();
            _stream!.Write(report);
        }

        private void EnsureConnected()
        {
            if (_stream == null)
                throw new InvalidOperationException("La connexion HID DualSense vibration n'est pas ouverte.");
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
            _device = null;
        }
    }
}
