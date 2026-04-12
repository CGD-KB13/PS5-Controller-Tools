using HidSharp;
using System;
using System.Linq;

namespace PS5_Controller_Tools
{
    internal enum DualSenseBatteryChargeState
    {
        Unknown,
        Discharging,
        Charging,
        Full,
        NotCharging
    }

    internal readonly struct DualSenseBatteryInfo
    {
        public static DualSenseBatteryInfo Unavailable => new(false, 0, DualSenseBatteryChargeState.Unknown);

        public bool IsAvailable { get; }
        public int Percentage { get; }
        public DualSenseBatteryChargeState ChargeState { get; }
        public bool IsCharging => ChargeState == DualSenseBatteryChargeState.Charging || ChargeState == DualSenseBatteryChargeState.Full;

        public DualSenseBatteryInfo(bool isAvailable, int percentage, DualSenseBatteryChargeState chargeState)
        {
            IsAvailable = isAvailable;
            Percentage = Math.Clamp(percentage, 0, 100);
            ChargeState = chargeState;
        }
    }

    internal sealed class DualSenseBatteryReader : IDisposable
    {
        private const byte UsbInputReportId = 0x01;
        private const int MinimumUsbInputReportLength = 64;
        private const int UsbBatteryStatusIndex = 53;

        private HidDevice? _device;
        private HidStream? _stream;

        public bool TryReadBatteryInfo(out DualSenseBatteryInfo batteryInfo)
        {
            batteryInfo = DualSenseBatteryInfo.Unavailable;

            if (!Connect(out _))
                return false;

            try
            {
                byte[]? report = ReadUsbInputReport();
                if (report == null || report.Length <= UsbBatteryStatusIndex)
                    return false;

                batteryInfo = ParseBatteryInfo(report[UsbBatteryStatusIndex]);
                return batteryInfo.IsAvailable;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(DualSenseBatteryReader), $"Lecture batterie impossible : {ex.Message}");
                Dispose();
                return false;
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
                .Where(d => d.GetMaxInputReportLength() >= MinimumUsbInputReportLength)
                .OrderByDescending(d => d.GetMaxInputReportLength())
                .FirstOrDefault();

            if (device == null)
            {
                error = "Aucune interface HID DualSense compatible n'a été trouvée pour la batterie.";
                return false;
            }

            try
            {
                HidStream stream = device.Open();
                stream.ReadTimeout = AppConstants.Controller.BatteryRefreshReadTimeoutMs;

                _device = device;
                _stream = stream;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ouverture HID batterie impossible : {ex.Message}";
                Dispose();
                return false;
            }
        }

        private byte[]? ReadUsbInputReport()
        {
            if (_stream == null || _device == null)
                throw new InvalidOperationException("La connexion HID batterie n'est pas ouverte.");

            int reportLength = Math.Max(_device.GetMaxInputReportLength(), MinimumUsbInputReportLength);
            byte[] buffer = new byte[reportLength];

            for (int attempt = 0; attempt < AppConstants.Controller.BatteryReadAttemptCount; attempt++)
            {
                Array.Clear(buffer, 0, buffer.Length);
                int bytesRead = _stream.Read(buffer);
                if (bytesRead <= UsbBatteryStatusIndex)
                    continue;

                if (buffer[0] == UsbInputReportId)
                {
                    byte[] report = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, report, 0, bytesRead);
                    return report;
                }
            }

            return null;
        }

        private static DualSenseBatteryInfo ParseBatteryInfo(byte status0)
        {
            int batteryData = status0 & 0x0F;
            int chargingStatus = (status0 >> 4) & 0x0F;

            return chargingStatus switch
            {
                0x0 => new DualSenseBatteryInfo(true, Math.Min(batteryData * 10 + 5, 100), DualSenseBatteryChargeState.Discharging),
                0x1 => new DualSenseBatteryInfo(true, Math.Min(batteryData * 10 + 5, 100), DualSenseBatteryChargeState.Charging),
                0x2 => new DualSenseBatteryInfo(true, 100, DualSenseBatteryChargeState.Full),
                0xA => new DualSenseBatteryInfo(true, 0, DualSenseBatteryChargeState.NotCharging),
                0xB => new DualSenseBatteryInfo(true, 0, DualSenseBatteryChargeState.NotCharging),
                _ => DualSenseBatteryInfo.Unavailable
            };
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
            _device = null;
        }
    }
}
