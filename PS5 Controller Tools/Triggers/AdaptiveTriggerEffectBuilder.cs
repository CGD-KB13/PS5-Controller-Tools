
namespace PS5_Controller_Tools.Triggers
{
    public readonly struct AdaptiveTriggerState
    {
        public AdaptiveTriggerState(
            AdaptiveTriggerSide side,
            string logicalName,
            AdaptiveTriggerMode mode,
            double feedbackStartPercent,
            double feedbackForcePercent,
            double weaponStartPercent,
            double weaponEndPercent,
            double weaponForcePercent,
            double bowStartPercent,
            double bowEndPercent,
            double bowForcePercent,
            double bowSnapForcePercent,
            double gallopingStartPercent,
            double gallopingEndPercent,
            double gallopingFirstFootPercent,
            double gallopingSecondFootPercent,
            double gallopingFrequencyPercent,
            double vibrationIntensityPercent,
            double vibrationFrequencyPercent,
            double blockPositionPercent)
        {
            Side = side;
            LogicalName = string.IsNullOrWhiteSpace(logicalName)
                ? AdaptiveTriggerNames.GetDefault(side)
                : logicalName.Trim();
            Mode = mode;
            FeedbackStartPercent = ClampPercent(feedbackStartPercent);
            FeedbackForcePercent = ClampPercent(feedbackForcePercent);
            WeaponStartPercent = ClampPercent(weaponStartPercent);
            WeaponEndPercent = ClampPercent(weaponEndPercent);
            WeaponForcePercent = ClampPercent(weaponForcePercent);
            BowStartPercent = ClampPercent(bowStartPercent);
            BowEndPercent = ClampPercent(bowEndPercent);
            BowForcePercent = ClampPercent(bowForcePercent);
            BowSnapForcePercent = ClampPercent(bowSnapForcePercent);
            GallopingStartPercent = ClampPercent(gallopingStartPercent);
            GallopingEndPercent = ClampPercent(gallopingEndPercent);
            GallopingFirstFootPercent = ClampPercent(gallopingFirstFootPercent);
            GallopingSecondFootPercent = ClampPercent(gallopingSecondFootPercent);
            GallopingFrequencyPercent = ClampPercent(gallopingFrequencyPercent);
            VibrationIntensityPercent = ClampPercent(vibrationIntensityPercent);
            VibrationFrequencyPercent = ClampPercent(vibrationFrequencyPercent);
            BlockPositionPercent = ClampPercent(blockPositionPercent);
        }

        public AdaptiveTriggerSide Side { get; }
        public string LogicalName { get; }
        public AdaptiveTriggerMode Mode { get; }
        public double FeedbackStartPercent { get; }
        public double FeedbackForcePercent { get; }
        public double WeaponStartPercent { get; }
        public double WeaponEndPercent { get; }
        public double WeaponForcePercent { get; }
        public double BowStartPercent { get; }
        public double BowEndPercent { get; }
        public double BowForcePercent { get; }
        public double BowSnapForcePercent { get; }
        public double GallopingStartPercent { get; }
        public double GallopingEndPercent { get; }
        public double GallopingFirstFootPercent { get; }
        public double GallopingSecondFootPercent { get; }
        public double GallopingFrequencyPercent { get; }
        public double VibrationIntensityPercent { get; }
        public double VibrationFrequencyPercent { get; }
        public double BlockPositionPercent { get; }

        public bool IsOff => Mode == AdaptiveTriggerMode.Off;

        public static AdaptiveTriggerState Disabled(AdaptiveTriggerSide side)
        {
            return new AdaptiveTriggerState(
                side,
                AdaptiveTriggerNames.GetDefault(side),
                AdaptiveTriggerMode.Off,
                AppConstants.AdaptiveTriggers.FeedbackDefaultStartPercent,
                AppConstants.AdaptiveTriggers.FeedbackDefaultForcePercent,
                AppConstants.AdaptiveTriggers.WeaponDefaultStartPercent,
                AppConstants.AdaptiveTriggers.WeaponDefaultEndPercent,
                AppConstants.AdaptiveTriggers.WeaponDefaultForcePercent,
                AppConstants.AdaptiveTriggers.BowDefaultStartPercent,
                AppConstants.AdaptiveTriggers.BowDefaultEndPercent,
                AppConstants.AdaptiveTriggers.BowDefaultForcePercent,
                AppConstants.AdaptiveTriggers.BowDefaultSnapForcePercent,
                AppConstants.AdaptiveTriggers.GallopingDefaultStartPercent,
                AppConstants.AdaptiveTriggers.GallopingDefaultEndPercent,
                AppConstants.AdaptiveTriggers.GallopingDefaultFirstFootPercent,
                AppConstants.AdaptiveTriggers.GallopingDefaultSecondFootPercent,
                AppConstants.AdaptiveTriggers.GallopingDefaultFrequencyPercent,
                AppConstants.AdaptiveTriggers.VibrationDefaultIntensityPercent,
                AppConstants.AdaptiveTriggers.VibrationDefaultFrequencyPercent,
                AppConstants.AdaptiveTriggers.BlockDefaultPositionPercent);
        }

        private static double ClampPercent(double value)
        {
            return Math.Clamp(
                value,
                AppConstants.AdaptiveTriggers.OverlaySliderMinValue,
                AppConstants.AdaptiveTriggers.OverlaySliderMaxValue);
        }
    }

    internal static class AdaptiveTriggerEffectBuilder
    {
        private const byte ModeOff = 0x05;
        private const byte ModeFeedback = 0x21;
        private const byte ModeWeapon = 0x25;
        private const byte ModeBow = 0x22;
        private const byte ModeGalloping = 0x23;
        private const byte ModeVibration = 0x26;
        private const byte ModeSimpleWeapon = 0x02;

        public static byte[] BuildEffectBytes(AdaptiveTriggerState state)
        {
            byte[] effectBytes = new byte[AppConstants.Hid.TriggerEffectByteLength];
            WriteEffect(effectBytes, 0, state);
            return effectBytes;
        }

        public static void WriteEffect(byte[] destinationArray, int destinationIndex, AdaptiveTriggerState state)
        {
            if (destinationArray == null)
                throw new ArgumentNullException(nameof(destinationArray));

            if (destinationIndex < 0 ||
                destinationIndex + AppConstants.Hid.TriggerEffectByteLength > destinationArray.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            }

            switch (state.Mode)
            {
                case AdaptiveTriggerMode.Feedback:
                    WriteFeedback(
                        destinationArray,
                        destinationIndex,
                        ToZoneIndex(state.FeedbackStartPercent),
                        ToEffectStrength(state.FeedbackForcePercent));
                    break;

                case AdaptiveTriggerMode.Weapon:
                {
                    byte startZone = ToWeaponStartZone(state.WeaponStartPercent);
                    WriteWeapon(
                        destinationArray,
                        destinationIndex,
                        startZone,
                        ToWeaponEndZone(state.WeaponEndPercent, startZone),
                        ToEffectStrength(state.WeaponForcePercent));
                    break;
                }

                case AdaptiveTriggerMode.Bow:
                {
                    byte startZone = ToBowStartZone(state.BowStartPercent);
                    WriteBow(
                        destinationArray,
                        destinationIndex,
                        startZone,
                        ToBowEndZone(state.BowEndPercent, startZone),
                        ToEffectStrength(state.BowForcePercent),
                        ToEffectStrength(state.BowSnapForcePercent));
                    break;
                }

                case AdaptiveTriggerMode.Galloping:
                {
                    byte startZone = ToGallopingStartZone(state.GallopingStartPercent);
                    byte firstFoot = ToGallopingFirstFoot(state.GallopingFirstFootPercent);
                    WriteGalloping(
                        destinationArray,
                        destinationIndex,
                        startZone,
                        ToGallopingEndZone(state.GallopingEndPercent, startZone),
                        firstFoot,
                        ToGallopingSecondFoot(state.GallopingSecondFootPercent, firstFoot),
                        ToGallopingFrequency(state.GallopingFrequencyPercent));
                    break;
                }

                case AdaptiveTriggerMode.Vibration:
                    WriteVibration(
                        destinationArray,
                        destinationIndex,
                        ToZoneIndex(AppConstants.AdaptiveTriggers.VibrationImplicitStartPercent),
                        ToEffectStrength(state.VibrationIntensityPercent),
                        ToFrequency(state.VibrationFrequencyPercent));
                    break;

                case AdaptiveTriggerMode.Block:
                    WriteBlock(
                        destinationArray,
                        destinationIndex,
                        ToSimpleWeaponStart(state.BlockPositionPercent));
                    break;

                default:
                    WriteOff(destinationArray, destinationIndex);
                    break;
            }
        }

        private static void WriteOff(byte[] destinationArray, int destinationIndex)
        {
            for (int index = 0; index < AppConstants.Hid.TriggerEffectByteLength; index++)
                destinationArray[destinationIndex + index] = 0x00;

            destinationArray[destinationIndex + 0] = ModeOff;
        }

        private static void WriteFeedback(byte[] destinationArray, int destinationIndex, byte position, byte strength)
        {
            if (position > 9 || strength > 8 || strength == 0)
            {
                WriteOff(destinationArray, destinationIndex);
                return;
            }

            byte forceValue = (byte)((strength - 1) & 0x07);
            uint forceZones = 0;
            ushort activeZones = 0;

            for (int zone = position; zone < AppConstants.AdaptiveTriggers.EffectZoneCount; zone++)
            {
                forceZones |= (uint)(forceValue << (3 * zone));
                activeZones |= (ushort)(1 << zone);
            }

            destinationArray[destinationIndex + 0] = ModeFeedback;
            destinationArray[destinationIndex + 1] = (byte)((activeZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 2] = (byte)((activeZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 3] = (byte)((forceZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 4] = (byte)((forceZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 5] = (byte)((forceZones >> 16) & 0xFF);
            destinationArray[destinationIndex + 6] = (byte)((forceZones >> 24) & 0xFF);
            destinationArray[destinationIndex + 7] = 0x00;
            destinationArray[destinationIndex + 8] = 0x00;
            destinationArray[destinationIndex + 9] = 0x00;
            destinationArray[destinationIndex + 10] = 0x00;
        }

        private static void WriteWeapon(byte[] destinationArray, int destinationIndex, byte startPosition, byte endPosition, byte strength)
        {
            if (startPosition < AppConstants.AdaptiveTriggers.WeaponStartZoneMin ||
                startPosition > AppConstants.AdaptiveTriggers.WeaponStartZoneMax ||
                endPosition < AppConstants.AdaptiveTriggers.WeaponEndZoneMin ||
                endPosition > AppConstants.AdaptiveTriggers.WeaponEndZoneMax ||
                endPosition <= startPosition ||
                strength > 8 ||
                strength == 0)
            {
                WriteOff(destinationArray, destinationIndex);
                return;
            }

            ushort startAndStopZones = (ushort)((1 << startPosition) | (1 << endPosition));

            destinationArray[destinationIndex + 0] = ModeWeapon;
            destinationArray[destinationIndex + 1] = (byte)((startAndStopZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 2] = (byte)((startAndStopZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 3] = (byte)(strength - 1);
            destinationArray[destinationIndex + 4] = 0x00;
            destinationArray[destinationIndex + 5] = 0x00;
            destinationArray[destinationIndex + 6] = 0x00;
            destinationArray[destinationIndex + 7] = 0x00;
            destinationArray[destinationIndex + 8] = 0x00;
            destinationArray[destinationIndex + 9] = 0x00;
            destinationArray[destinationIndex + 10] = 0x00;
        }

        private static void WriteBow(byte[] destinationArray, int destinationIndex, byte startPosition, byte endPosition, byte strength, byte snapForce)
        {
            if (startPosition < AppConstants.AdaptiveTriggers.BowStartZoneMin ||
                startPosition > AppConstants.AdaptiveTriggers.BowStartZoneMax ||
                endPosition < AppConstants.AdaptiveTriggers.BowEndZoneMin ||
                endPosition > AppConstants.AdaptiveTriggers.BowEndZoneMax ||
                startPosition >= endPosition ||
                strength > 8 ||
                snapForce > 8 ||
                endPosition == 0 ||
                strength == 0 ||
                snapForce == 0)
            {
                WriteOff(destinationArray, destinationIndex);
                return;
            }

            ushort startAndStopZones = (ushort)((1 << startPosition) | (1 << endPosition));
            uint forcePair = (uint)((((strength - 1) & 0x07) << (3 * 0)) |
                                    (((snapForce - 1) & 0x07) << (3 * 1)));

            destinationArray[destinationIndex + 0] = ModeBow;
            destinationArray[destinationIndex + 1] = (byte)((startAndStopZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 2] = (byte)((startAndStopZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 3] = (byte)((forcePair >> 0) & 0xFF);
            destinationArray[destinationIndex + 4] = (byte)((forcePair >> 8) & 0xFF);
            destinationArray[destinationIndex + 5] = 0x00;
            destinationArray[destinationIndex + 6] = 0x00;
            destinationArray[destinationIndex + 7] = 0x00;
            destinationArray[destinationIndex + 8] = 0x00;
            destinationArray[destinationIndex + 9] = 0x00;
            destinationArray[destinationIndex + 10] = 0x00;
        }

        private static void WriteGalloping(byte[] destinationArray, int destinationIndex, byte startPosition, byte endPosition, byte firstFoot, byte secondFoot, byte frequency)
        {
            if (startPosition < AppConstants.AdaptiveTriggers.GallopingStartZoneMin ||
                startPosition > AppConstants.AdaptiveTriggers.GallopingStartZoneMax ||
                endPosition < AppConstants.AdaptiveTriggers.GallopingEndZoneMin ||
                endPosition > AppConstants.AdaptiveTriggers.GallopingEndZoneMax ||
                startPosition >= endPosition ||
                firstFoot > AppConstants.AdaptiveTriggers.GallopingFirstFootMaxValue ||
                secondFoot > AppConstants.AdaptiveTriggers.GallopingSecondFootMaxValue ||
                firstFoot >= secondFoot ||
                frequency == 0)
            {
                WriteOff(destinationArray, destinationIndex);
                return;
            }

            ushort startAndStopZones = (ushort)((1 << startPosition) | (1 << endPosition));
            uint timeAndRatio = (uint)(((secondFoot & 0x07) << (3 * 0)) |
                                       ((firstFoot & 0x07) << (3 * 1)));

            destinationArray[destinationIndex + 0] = ModeGalloping;
            destinationArray[destinationIndex + 1] = (byte)((startAndStopZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 2] = (byte)((startAndStopZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 3] = (byte)((timeAndRatio >> 0) & 0xFF);
            destinationArray[destinationIndex + 4] = frequency;
            destinationArray[destinationIndex + 5] = 0x00;
            destinationArray[destinationIndex + 6] = 0x00;
            destinationArray[destinationIndex + 7] = 0x00;
            destinationArray[destinationIndex + 8] = 0x00;
            destinationArray[destinationIndex + 9] = 0x00;
            destinationArray[destinationIndex + 10] = 0x00;
        }

        private static void WriteVibration(byte[] destinationArray, int destinationIndex, byte position, byte amplitude, byte frequency)
        {
            if (position > 9 || amplitude > 8 || amplitude == 0 || frequency == 0)
            {
                WriteOff(destinationArray, destinationIndex);
                return;
            }

            byte amplitudeValue = (byte)((amplitude - 1) & 0x07);
            uint amplitudeZones = 0;
            ushort activeZones = 0;

            for (int zone = position; zone < AppConstants.AdaptiveTriggers.EffectZoneCount; zone++)
            {
                amplitudeZones |= (uint)(amplitudeValue << (3 * zone));
                activeZones |= (ushort)(1 << zone);
            }

            destinationArray[destinationIndex + 0] = ModeVibration;
            destinationArray[destinationIndex + 1] = (byte)((activeZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 2] = (byte)((activeZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 3] = (byte)((amplitudeZones >> 0) & 0xFF);
            destinationArray[destinationIndex + 4] = (byte)((amplitudeZones >> 8) & 0xFF);
            destinationArray[destinationIndex + 5] = (byte)((amplitudeZones >> 16) & 0xFF);
            destinationArray[destinationIndex + 6] = (byte)((amplitudeZones >> 24) & 0xFF);
            destinationArray[destinationIndex + 7] = 0x00;
            destinationArray[destinationIndex + 8] = 0x00;
            destinationArray[destinationIndex + 9] = frequency;
            destinationArray[destinationIndex + 10] = 0x00;
        }

        private static void WriteBlock(byte[] destinationArray, int destinationIndex, byte startPosition)
        {
            if (startPosition < AppConstants.AdaptiveTriggers.SimpleWeaponStartMinValue ||
                startPosition > AppConstants.AdaptiveTriggers.SimpleWeaponStartMaxValue)
            {
                WriteOff(destinationArray, destinationIndex);
                return;
            }

            destinationArray[destinationIndex + 0] = ModeSimpleWeapon;
            destinationArray[destinationIndex + 1] = startPosition;
            destinationArray[destinationIndex + 2] = AppConstants.AdaptiveTriggers.SimpleWeaponEndValue;
            destinationArray[destinationIndex + 3] = AppConstants.AdaptiveTriggers.SimpleWeaponStrengthMaxValue;
            destinationArray[destinationIndex + 4] = 0x00;
            destinationArray[destinationIndex + 5] = 0x00;
            destinationArray[destinationIndex + 6] = 0x00;
            destinationArray[destinationIndex + 7] = 0x00;
            destinationArray[destinationIndex + 8] = 0x00;
            destinationArray[destinationIndex + 9] = 0x00;
            destinationArray[destinationIndex + 10] = 0x00;
        }

        private static byte ToZoneIndex(double percent)
        {
            double normalized = percent / AppConstants.AdaptiveTriggers.OverlaySliderMaxValue;
            int value = (int)Math.Round(normalized * (AppConstants.AdaptiveTriggers.EffectZoneCount - 1), MidpointRounding.AwayFromZero);
            return (byte)Math.Clamp(value, 0, AppConstants.AdaptiveTriggers.EffectZoneCount - 1);
        }

        private static byte ToWeaponStartZone(double percent)
        {
            int value = ToZoneIndex(percent);
            return (byte)Math.Clamp(
                value,
                AppConstants.AdaptiveTriggers.WeaponStartZoneMin,
                AppConstants.AdaptiveTriggers.WeaponStartZoneMax);
        }

        private static byte ToWeaponEndZone(double percent, byte startZone)
        {
            int value = ToZoneIndex(percent);
            int minimum = Math.Max(startZone + 1, AppConstants.AdaptiveTriggers.WeaponEndZoneMin);
            return (byte)Math.Clamp(
                value,
                minimum,
                AppConstants.AdaptiveTriggers.WeaponEndZoneMax);
        }

        private static byte ToBowStartZone(double percent)
        {
            int value = ToZoneIndex(percent);
            return (byte)Math.Clamp(
                value,
                AppConstants.AdaptiveTriggers.BowStartZoneMin,
                AppConstants.AdaptiveTriggers.BowStartZoneMax);
        }

        private static byte ToBowEndZone(double percent, byte startZone)
        {
            int value = ToZoneIndex(percent);
            int minimum = Math.Max(startZone + 1, AppConstants.AdaptiveTriggers.BowEndZoneMin);
            return (byte)Math.Clamp(
                value,
                minimum,
                AppConstants.AdaptiveTriggers.BowEndZoneMax);
        }

        private static byte ToGallopingStartZone(double percent)
        {
            int value = ToZoneIndex(percent);
            return (byte)Math.Clamp(
                value,
                AppConstants.AdaptiveTriggers.GallopingStartZoneMin,
                AppConstants.AdaptiveTriggers.GallopingStartZoneMax);
        }

        private static byte ToGallopingEndZone(double percent, byte startZone)
        {
            int value = ToZoneIndex(percent);
            int minimum = Math.Max(startZone + 1, AppConstants.AdaptiveTriggers.GallopingEndZoneMin);
            return (byte)Math.Clamp(
                value,
                minimum,
                AppConstants.AdaptiveTriggers.GallopingEndZoneMax);
        }

        private static byte ToGallopingFirstFoot(double percent)
        {
            return ToMaxBoundedByte(percent, AppConstants.AdaptiveTriggers.GallopingFirstFootMaxValue);
        }

        private static byte ToGallopingSecondFoot(double percent, byte firstFoot)
        {
            int minimum = Math.Max(firstFoot + 1, 1);
            int value = ToMaxBoundedByte(percent, AppConstants.AdaptiveTriggers.GallopingSecondFootMaxValue);
            return (byte)Math.Clamp(value, minimum, AppConstants.AdaptiveTriggers.GallopingSecondFootMaxValue);
        }

        private static byte ToEffectStrength(double percent)
        {
            return ToMaxBoundedByte(percent, AppConstants.AdaptiveTriggers.EffectStrengthMaxValue);
        }

        private static byte ToFrequency(double percent)
        {
            return ToMaxBoundedByte(percent, AppConstants.AdaptiveTriggers.EffectFrequencyMaxValue);
        }

        private static byte ToGallopingFrequency(double percent)
        {
            if (percent <= AppConstants.AdaptiveTriggers.OverlaySliderMinValue)
                return 0;

            byte value = ToMaxBoundedByte(percent, AppConstants.AdaptiveTriggers.GallopingFrequencyMaxValue);
            return value == 0 ? (byte)1 : value;
        }

        private static byte ToSimpleWeaponStart(double percent)
        {
            double normalized = percent / AppConstants.AdaptiveTriggers.OverlaySliderMaxValue;
            int range = AppConstants.AdaptiveTriggers.SimpleWeaponStartMaxValue - AppConstants.AdaptiveTriggers.SimpleWeaponStartMinValue;
            int value = AppConstants.AdaptiveTriggers.SimpleWeaponStartMinValue +
                        (int)Math.Round(normalized * range, MidpointRounding.AwayFromZero);

            return (byte)Math.Clamp(
                value,
                AppConstants.AdaptiveTriggers.SimpleWeaponStartMinValue,
                AppConstants.AdaptiveTriggers.SimpleWeaponStartMaxValue);
        }

        private static byte ToMaxBoundedByte(double percent, byte maxValue)
        {
            double normalized = percent / AppConstants.AdaptiveTriggers.OverlaySliderMaxValue;
            int value = (int)Math.Round(normalized * maxValue, MidpointRounding.AwayFromZero);
            return (byte)Math.Clamp(value, 0, maxValue);
        }
    }
}
