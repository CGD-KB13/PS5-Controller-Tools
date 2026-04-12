using HidSharp;

namespace PS5_Controller_Tools.Triggers
{
    internal sealed class DualSenseHidTriggerControl : IDisposable
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
                error = "Aucune interface HID USB DualSense compatible n'a ete trouvee pour les triggers.";
                return false;
            }

            try
            {
                HidStream stream = device.Open();
                stream.WriteTimeout = AppConstants.Hid.HidWriteTimeoutMs;

                _device = device;
                _stream = stream;
                AppLogger.Info(nameof(DualSenseHidTriggerControl), $"Connexion HID triggers ouverte : {device.ProductName}");
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ouverture HID triggers impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidTriggerControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TrySetEffects(byte[] leftTriggerEffect, byte[] rightTriggerEffect, out string error)
        {
            error = string.Empty;

            if (leftTriggerEffect == null)
                throw new ArgumentNullException(nameof(leftTriggerEffect));
            if (rightTriggerEffect == null)
                throw new ArgumentNullException(nameof(rightTriggerEffect));

            if (!Connect(out error))
                return false;

            try
            {
                WriteEffectsCore(leftTriggerEffect, rightTriggerEffect);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ecriture HID triggers impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidTriggerControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TryReset(out string error)
        {
            byte[] offEffect = new byte[AppConstants.Hid.TriggerEffectByteLength];
            AdaptiveTriggerEffectBuilder.WriteEffect(
                offEffect,
                0,
                AdaptiveTriggerState.Disabled(AdaptiveTriggerSide.Left));

            return TrySetEffects(offEffect, offEffect, out error);
        }

        private void WriteEffectsCore(byte[] leftTriggerEffect, byte[] rightTriggerEffect)
        {
            EnsureConnected();
            ValidateEffectLength(leftTriggerEffect, nameof(leftTriggerEffect));
            ValidateEffectLength(rightTriggerEffect, nameof(rightTriggerEffect));

            byte[] report = CreateEmptyUsbOutputReport();
            report[AppConstants.Hid.ReportIndexValidFlag0] =
                (byte)(AppConstants.Hid.ValidFlag0RightTriggerEnable |
                       AppConstants.Hid.ValidFlag0LeftTriggerEnable);

            Buffer.BlockCopy(
                rightTriggerEffect,
                0,
                report,
                AppConstants.Hid.ReportIndexTriggerRightEffect,
                AppConstants.Hid.TriggerEffectByteLength);

            Buffer.BlockCopy(
                leftTriggerEffect,
                0,
                report,
                AppConstants.Hid.ReportIndexTriggerLeftEffect,
                AppConstants.Hid.TriggerEffectByteLength);

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
                throw new InvalidOperationException("La connexion HID DualSense triggers n'est pas ouverte.");
        }

        private static void ValidateEffectLength(byte[] effectBytes, string paramName)
        {
            if (effectBytes.Length != AppConstants.Hid.TriggerEffectByteLength)
            {
                throw new ArgumentException(
                    $"Le buffer {paramName} doit contenir {AppConstants.Hid.TriggerEffectByteLength} octets.",
                    paramName);
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
            _device = null;
        }
    }
}
