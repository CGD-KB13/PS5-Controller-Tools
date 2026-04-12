using HidSharp;

namespace PS5_Controller_Tools
{
    public sealed class DualSenseHidAudioControl : IDisposable
    {
        private const byte DefaultSpeakerVolume = AppConstants.Audio.SpeakerVolumeMax;
        private const byte DefaultSpeakerPreamp = AppConstants.Audio.SpeakerPreampMax;

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
                error = "Aucune interface HID USB DualSense compatible n'a été trouvée.";
                AppLogger.Warn(nameof(DualSenseHidAudioControl), error);
                return false;
            }

            try
            {
                HidStream stream = device.Open();
                stream.WriteTimeout = AppConstants.Hid.HidWriteTimeoutMs;

                _device = device;
                _stream = stream;
                AppLogger.Info(nameof(DualSenseHidAudioControl), $"Connexion HID ouverte : {device.ProductName}");
                return true;
            }
            catch (Exception ex)
            {
                error = $"Ouverture HID impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidAudioControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TryRouteToInternalSpeaker(
            byte speakerVolume,
            byte speakerPreamp,
            out string error)
        {
            error = string.Empty;

            if (!Connect(out error))
                return false;

            try
            {
                RouteToInternalSpeakerCore(speakerVolume, speakerPreamp);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Routage vers le haut-parleur interne impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidAudioControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TryRouteToHeadphones(out string error)
        {
            error = string.Empty;

            if (!Connect(out error))
                return false;

            try
            {
                RouteToHeadphonesCore();
                return true;
            }
            catch (Exception ex)
            {
                error = $"Restauration du routage audio impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidAudioControl), error, ex);
                Dispose();
                return false;
            }
        }

        public bool TryRestoreControllerDefaults(out string error)
        {
            error = string.Empty;

            if (!Connect(out error))
                return false;

            try
            {
                RouteToHeadphonesCore();
                FadeLightbarOutAndReleaseLedsCore();
                AppLogger.Info(nameof(DualSenseHidAudioControl), "Etat materiel DualSense restaure.");
                return true;
            }
            catch (Exception ex)
            {
                error = $"Restauration de l'etat DualSense impossible : {ex.Message}";
                AppLogger.Error(nameof(DualSenseHidAudioControl), error, ex);
                Dispose();
                return false;
            }
        }

        private void RouteToInternalSpeakerCore(byte speakerVolume, byte speakerPreamp)
        {
            EnsureConnected();

            if (speakerVolume > AppConstants.Audio.SpeakerVolumeMax)
                speakerVolume = AppConstants.Audio.SpeakerVolumeMax;

            if (speakerPreamp > AppConstants.Audio.SpeakerPreampMax)
                speakerPreamp = AppConstants.Audio.SpeakerPreampMax;

            byte[] report = CreateEmptyUsbOutputReport();
            report[AppConstants.Hid.ReportIndexValidFlag0] =
                (byte)(AppConstants.Hid.ValidFlag0AudioControlEnable | AppConstants.Hid.ValidFlag0SpeakerVolumeEnable);
            report[AppConstants.Hid.ReportIndexValidFlag1] =
                (byte)(AppConstants.Hid.ValidFlag1PowerSaveControlEnable | AppConstants.Hid.ValidFlag1AudioControl2Enable);
            report[AppConstants.Hid.ReportIndexSpeakerVolume] = speakerVolume;
            report[AppConstants.Hid.ReportIndexAudioFlags] = AppConstants.Hid.OutputPathInternalSpeakerRightChannel;
            report[AppConstants.Hid.ReportIndexPowerSaveControl] = 0x00;
            report[AppConstants.Hid.ReportIndexSpeakerPreamp] = speakerPreamp;

            WriteReport(report);
        }

        private void RouteToHeadphonesCore()
        {
            EnsureConnected();

            byte[] report = CreateEmptyUsbOutputReport();
            report[AppConstants.Hid.ReportIndexValidFlag0] = AppConstants.Hid.ValidFlag0AudioControlEnable;
            report[AppConstants.Hid.ReportIndexAudioFlags] = AppConstants.Hid.OutputPathHeadphonesDefault;

            WriteReport(report);
        }

        private void FadeLightbarOutAndReleaseLedsCore()
        {
            EnsureConnected();

            byte[] report = CreateEmptyUsbOutputReport();
            report[AppConstants.Hid.ReportIndexValidFlag1] =
                (byte)(AppConstants.Hid.ValidFlag1LightbarControlEnable |
                       AppConstants.Hid.ValidFlag1ReleaseLeds |
                       AppConstants.Hid.ValidFlag1PlayerIndicatorControlEnable);
            report[AppConstants.Hid.ReportIndexValidFlag2] = AppConstants.Hid.ValidFlag2LightbarSetupControlEnable;
            report[AppConstants.Hid.ReportIndexLightbarSetup] = AppConstants.Hid.LightbarSetupLightOut;
            report[AppConstants.Hid.ReportIndexLedBrightness] = AppConstants.Hid.PlayerLedBrightnessLow;
            report[AppConstants.Hid.ReportIndexPlayerLeds] = AppConstants.Hid.PlayerLedsOff;
            report[AppConstants.Hid.ReportIndexLightbarRed] = 0x00;
            report[AppConstants.Hid.ReportIndexLightbarGreen] = 0x00;
            report[AppConstants.Hid.ReportIndexLightbarBlue] = 0x00;

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
            {
                throw new InvalidOperationException("La connexion HID DualSense n'est pas ouverte.");
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
