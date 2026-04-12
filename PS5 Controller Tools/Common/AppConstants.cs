namespace PS5_Controller_Tools
{
    internal static class AppConstants
    {
        internal static class Assets
        {
            public const string TestWaveResourcePath = "/Assets/Fichier de test WAVE.CGD2026.wav";
            public const string StartupWaveResourcePath = "/Assets/ps1_startup_short.wav";
        }

        internal static class Controller
        {
            public const int PollingIntervalMs = 16;
            public const int ReconnectScanIntervalMs = 250;
            public const int LightBarSequenceDelayMs = 590;
            public const int PlayerSequenceDelayMs = 1000;
            public const int TouchPadButtonIndex = 15;
            public const int MicButtonIndex = 16;

            public const double TriggerDeadZone = 0.03;
            public const double TriggerSignedDetectionThreshold = -1000.0;

            public const double StickDeadZone = 0.12;
            public const double StickDotPadding = 3.0;
            public const double StickVisibleMagnitudeThreshold = 0.001;
            public const int DebugAxisNoiseThreshold = 5000;

            public const int BatteryRefreshIntervalSeconds = 1;
            public const int BatteryRefreshReadTimeoutMs = 250;
            public const int BatteryReadAttemptCount = 4;

        }

        internal static class TouchPad
        {
            public const int PrimaryTouchPadIndex = 0;
            public const int PreferredFingerIndex = 0;
            public const double DotPadding = 4.0;

            // Contrôle la largeur en HAUT du pavé tactile
            // Valeur faible = très large en haut
            public const double TopInsetRatio = 0.02;
            // Contrôle la largeur en BAS du pavé tactile
            // Valeur faible = bas PLUS LARGE
            // Valeur élevée = bas PLUS ÉTROIT
            // Diminue cette valeur pour agrandir le bas du trapèze
            public const double BottomInsetRatio = 0.10;
        }

        internal static class Audio
        {
            public const double DefaultSpeakerOverlayVolume = 64.0;
            public const double OverlaySliderMaxValue = 127.0;
            public const int PostPlaybackRoutingRefreshDelayMs = 60;

            public const double VolumeCurveExponent = 0.55;
            public const int AudibleFloorVolume = 30;
            public const int SpeakerVolumeMax = 127;

            public const byte SpeakerPreampMute = 0x00;
            public const byte SpeakerPreampLow = 0x02;
            public const byte SpeakerPreampMediumLow = 0x03;
            public const byte SpeakerPreampMedium = 0x04;
            public const byte SpeakerPreampMediumHigh = 0x05;
            public const byte SpeakerPreampHigh = 0x06;
            public const byte SpeakerPreampMax = 0x07;

            public const byte SpeakerPreampThresholdLow = 45;
            public const byte SpeakerPreampThresholdMediumLow = 70;
            public const byte SpeakerPreampThresholdMedium = 90;
            public const byte SpeakerPreampThresholdHigh = 105;
            public const byte SpeakerPreampThresholdMax = 118;
        }

        internal static class Vibration
        {
            public const double OverlaySliderMinValue = 0.0;
            public const double OverlaySliderMaxValue = 100.0;
            public const byte HardwareMotorMaxValue = 255;
        }

        internal static class AdaptiveTriggers
        {
            public const double OverlaySliderMinValue = 0.0;
            public const double OverlaySliderMaxValue = 100.0;

            public const int ModeCount = 7;
            public const int ModeOffIndex = 0;
            public const int ModeFeedbackIndex = 1;
            public const int ModeWeaponIndex = 2;
            public const int ModeBowIndex = 3;
            public const int ModeGallopingIndex = 4;
            public const int ModeVibrationIndex = 5;
            public const int ModeBlockIndex = 6;

            public const int ParameterCountMax = 5;
            public const byte EffectZoneCount = 10;
            public const byte EffectStrengthMaxValue = 8;
            public const byte EffectFrequencyMaxValue = 255;

            public const byte WeaponStartZoneMin = 2;
            public const byte WeaponStartZoneMax = 7;
            public const byte WeaponEndZoneMin = 3;
            public const byte WeaponEndZoneMax = 8;

            public const byte BowStartZoneMin = 0;
            public const byte BowStartZoneMax = 8;
            public const byte BowEndZoneMin = 1;
            public const byte BowEndZoneMax = 8;

            public const byte GallopingStartZoneMin = 0;
            public const byte GallopingStartZoneMax = 8;
            public const byte GallopingEndZoneMin = 1;
            public const byte GallopingEndZoneMax = 9;
            public const byte GallopingFirstFootMaxValue = 6;
            public const byte GallopingSecondFootMaxValue = 7;
            public const byte GallopingFrequencyMaxValue = 20;

            public const byte SimpleWeaponStartMinValue = 0x10;
            public const byte SimpleWeaponStartMaxValue = 0x90;
            public const byte SimpleWeaponEndValue = 0xA0;
            public const byte SimpleWeaponStrengthMaxValue = 0xFF;

            public const double FeedbackDefaultStartPercent = 28.0;
            public const double FeedbackDefaultForcePercent = 70.0;

            public const double WeaponDefaultStartPercent = 35.0;
            public const double WeaponDefaultEndPercent = 78.0;
            public const double WeaponDefaultForcePercent = 82.0;

            public const double BowDefaultStartPercent = 26.0;
            public const double BowDefaultEndPercent = 74.0;
            public const double BowDefaultForcePercent = 72.0;
            public const double BowDefaultSnapForcePercent = 68.0;

            public const double GallopingDefaultStartPercent = 18.0;
            public const double GallopingDefaultEndPercent = 84.0;
            public const double GallopingDefaultFirstFootPercent = 26.0;
            public const double GallopingDefaultSecondFootPercent = 70.0;
            public const double GallopingDefaultFrequencyPercent = 18.0;

            public const double VibrationDefaultIntensityPercent = 70.0;
            public const double VibrationDefaultFrequencyPercent = 45.0;
            public const double VibrationImplicitStartPercent = 0.0;

            public const double BlockDefaultPositionPercent = 62.0;
        }

        internal static class Hid
        {
            public const int SonyVendorId = 0x054C;
            public const int DualSenseProductId = 0x0CE6;
            public const byte UsbOutputReportId = 0x02;
            public const int UsbOutputReportLength = 48;

            public const int ReportIndexReportId = 0;
            public const int ReportIndexValidFlag0 = 1;
            public const int ReportIndexValidFlag1 = 2;
            public const int ReportIndexMotorRight = 3;
            public const int ReportIndexMotorLeft = 4;
            public const int ReportIndexSpeakerVolume = 6;
            public const int ReportIndexAudioFlags = 8;
            public const int ReportIndexMicLed = 9;
            public const int ReportIndexPowerSaveControl = 10;
            public const int ReportIndexTriggerRightEffect = 11;
            public const int ReportIndexTriggerLeftEffect = 22;
            public const int TriggerEffectByteLength = 11;
            public const int ReportIndexSpeakerPreamp = 38;
            public const int ReportIndexValidFlag2 = 39;
            public const int ReportIndexLightbarSetup = 42;
            public const int ReportIndexLedBrightness = 43;
            public const int ReportIndexPlayerLeds = 44;
            public const int ReportIndexLightbarRed = 45;
            public const int ReportIndexLightbarGreen = 46;
            public const int ReportIndexLightbarBlue = 47;

            public const byte ValidFlag0CompatibleVibrationEnable = 1 << 0;
            public const byte ValidFlag0HapticsSelectEnable = 1 << 1;
            public const byte ValidFlag0RightTriggerEnable = 1 << 2;
            public const byte ValidFlag0LeftTriggerEnable = 1 << 3;
            public const byte ValidFlag0SpeakerVolumeEnable = 1 << 5;
            public const byte ValidFlag0AudioControlEnable = 1 << 7;

            public const byte ValidFlag1MicLedControlEnable = 1 << 0;
            public const byte ValidFlag1PowerSaveControlEnable = 1 << 1;
            public const byte ValidFlag1LightbarControlEnable = 1 << 2;
            public const byte ValidFlag1ReleaseLeds = 1 << 3;
            public const byte ValidFlag1PlayerIndicatorControlEnable = 1 << 4;
            public const byte ValidFlag1AudioControl2Enable = 1 << 7;

            public const byte ValidFlag2LightbarSetupControlEnable = 1 << 1;
            public const byte LightbarSetupLightOut = 1 << 1;

            public const byte OutputPathInternalSpeakerRightChannel = 0x30;
            public const byte OutputPathHeadphonesDefault = 0x00;

            public const byte MicrophoneLedOff = 0x00;
            public const byte MicrophoneLedOn = 0x01;

            public const byte PlayerLedBrightnessHigh = 0x00;
            public const byte PlayerLedBrightnessMedium = 0x01;
            public const byte PlayerLedBrightnessLow = 0x02;

            public const byte PlayerLedsOff = 0x00;
            public const byte PlayerLedsInstantMask = 0x20;
            public const byte PlayerLedPatternCenter = 0x04;
            public const byte PlayerLedPatternInner = 0x0A;
            public const byte PlayerLedPatternCenterAndOuter = 0x15;
            public const byte PlayerLedPatternAll = 0x1F;

            public const byte Player1Leds = 0x04;
            public const byte Player2Leds = 0x0A;
            public const byte Player3Leds = 0x15;
            public const byte Player4Leds = 0x1B;

            public const int HidWriteTimeoutMs = 2000;
        }

        internal static class Visualizer
        {
            public const int SpeakerBarCount = 26;
            public const double SpeakerCanvasWidth = 55.0;
            public const double SpeakerCanvasHeight = 24.0;
            public const double SpeakerBarWidth = 2.0;
            public const double SpeakerBarGap = 0;
            public const double SpeakerMinBarHeight = 2.0;
            public const double SpeakerMaxBarHeight = 100.0;

            public const double SpeakerSmoothCarry = 0.72;
            public const double SpeakerSmoothInput = 0.28;
            public const double SpeakerBoostExponent = 0.65;
            public const double SpeakerRippleBase = 0.78;
            public const double SpeakerRippleAmplitude = 0.22;
            public const double SpeakerPhaseLevelFactor = 8.0;
            public const double SpeakerPhaseIndexFactor = 0.35;
            public const double SpeakerNoiseAmplitude = 1.2;
            public const double SpeakerBarHeightCarry = 0.58;
            public const double SpeakerBarHeightInput = 0.42;
            public const double SpeakerPhaseStep = 0.10;
            public const double SpeakerGlowIdleOpacity = 0.15;
            public const double SpeakerGlowBaseOpacity = 0.25;
            public const double SpeakerGlowBoostOpacity = 0.75;

            public const double WaveBarHeightMultiplier = 1.00;
            public const double WaveNoiseMultiplier = 1.00;
            public const double WaveGlowBoostMultiplier = 1.00;

            public const double BeepBarHeightMultiplier = 1.25;
            public const double BeepNoiseMultiplier = 0.35;
            public const double BeepGlowBoostMultiplier = 1.20;
            public const double BeepPhaseStepMultiplier = 1.45;
        }
    }
}
