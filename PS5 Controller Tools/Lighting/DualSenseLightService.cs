using HidSharp;
using System;
using System.Linq;

namespace PS5_Controller_Tools.Lighting
{
    internal sealed class DualSenseLightService : IDisposable
    {
        private HidDevice? _device;
        private HidStream? _stream;
        private DateTime _lastLightBarUpdate = DateTime.MinValue;
        private byte _lastR, _lastG, _lastB;

        public bool IsAvailable => DeviceList.Local
            .GetHidDevices(AppConstants.Hid.SonyVendorId, AppConstants.Hid.DualSenseProductId)
            .Any(d => d.GetMaxOutputReportLength() >= AppConstants.Hid.UsbOutputReportLength);

        public bool IsConnected => _stream != null;

        public void SetMicrophoneLed(bool enabled)
        {
            if (!Connect(out _))
                return;

            try
            {
                byte[] report = CreateEmptyUsbOutputReport();
                report[AppConstants.Hid.ReportIndexValidFlag1] = AppConstants.Hid.ValidFlag1MicLedControlEnable;
                report[AppConstants.Hid.ReportIndexMicLed] = enabled
                    ? AppConstants.Hid.MicrophoneLedOn
                    : AppConstants.Hid.MicrophoneLedOff;

                WriteReport(report);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(DualSenseLightService), $"Commande LED micro impossible : {ex.Message}");
                Dispose();
            }
        }

        public void SetPlayerLeds(byte value)
        {
            if (!Connect(out _))
                return;

            try
            {
                byte[] report = CreateEmptyUsbOutputReport();

                report[AppConstants.Hid.ReportIndexValidFlag1] =
                    AppConstants.Hid.ValidFlag1PlayerIndicatorControlEnable;

                report[AppConstants.Hid.ReportIndexPlayerLeds] = value;

                WriteReport(report);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(DualSenseLightService), $"Commande LED joueur impossible : {ex.Message}");
                Dispose();
            }
        }

        public void SetLightBar(byte red, byte green, byte blue)
        {
            if (!Connect(out _))
                return;

            // 1. ignore si même couleur
            if (_lastR == red && _lastG == green && _lastB == blue)
                return;

            // 2. throttling
            if ((DateTime.Now - _lastLightBarUpdate).TotalMilliseconds < 50)
                return;

            _lastR = red;
            _lastG = green;
            _lastB = blue;

            _lastLightBarUpdate = DateTime.Now;

            try
            {
                byte[] report = CreateEmptyUsbOutputReport();

                report[AppConstants.Hid.ReportIndexValidFlag1] =
                    AppConstants.Hid.ValidFlag1LightbarControlEnable;

                report[AppConstants.Hid.ReportIndexLightbarRed] = red;
                report[AppConstants.Hid.ReportIndexLightbarGreen] = green;
                report[AppConstants.Hid.ReportIndexLightbarBlue] = blue;

                WriteReport(report);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(DualSenseLightService), $"Commande barre lumineuse impossible : {ex.Message}");
                Dispose();
            }
        }

        public void Reset()
        {
            try
            {
                SetMicrophoneLed(enabled: false);
                SetPlayerLeds(AppConstants.Hid.PlayerLedsOff);
            }
            finally
            {
                Dispose();
            }
        }

        private bool Connect(out string error)
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
                error = "Aucune interface HID USB DualSense compatible n'a été trouvée pour l'éclairage.";
                return false;
            }

            try
            {
                HidStream stream = device.Open();
                stream.WriteTimeout = AppConstants.Hid.HidWriteTimeoutMs;

                _device = device;
                _stream = stream;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ouverture HID impossible pour l'éclairage : {ex.Message}";
                Dispose();
                return false;
            }
        }

        private static byte[] CreateEmptyUsbOutputReport()
        {
            byte[] report = new byte[AppConstants.Hid.UsbOutputReportLength];
            report[AppConstants.Hid.ReportIndexReportId] = AppConstants.Hid.UsbOutputReportId;
            return report;
        }

        private void WriteReport(byte[] report)
        {
            if (_stream == null)
                throw new InvalidOperationException("La connexion HID d'éclairage n'est pas ouverte.");

            _stream.Write(report);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
            _device = null;
        }
    }
}
