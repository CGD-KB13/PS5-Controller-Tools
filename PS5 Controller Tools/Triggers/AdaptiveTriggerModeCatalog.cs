
namespace PS5_Controller_Tools.Triggers
{
    public enum AdaptiveTriggerMode
    {
        Off = AppConstants.AdaptiveTriggers.ModeOffIndex,
        Feedback = AppConstants.AdaptiveTriggers.ModeFeedbackIndex,
        Weapon = AppConstants.AdaptiveTriggers.ModeWeaponIndex,
        Bow = AppConstants.AdaptiveTriggers.ModeBowIndex,
        Galloping = AppConstants.AdaptiveTriggers.ModeGallopingIndex,
        Vibration = AppConstants.AdaptiveTriggers.ModeVibrationIndex,
        Block = AppConstants.AdaptiveTriggers.ModeBlockIndex
    }

    public enum AdaptiveTriggerSide
    {
        Left,
        Right
    }

    internal enum AdaptiveTriggerParameterKind
    {
        FeedbackStart,
        FeedbackForce,
        WeaponStart,
        WeaponEnd,
        WeaponForce,
        BowStart,
        BowEnd,
        BowForce,
        BowSnapForce,
        GallopingStart,
        GallopingEnd,
        GallopingFirstFoot,
        GallopingSecondFoot,
        GallopingFrequency,
        VibrationIntensity,
        VibrationFrequency,
        BlockPosition
    }

    internal readonly struct AdaptiveTriggerParameterDefinition
    {
        public AdaptiveTriggerParameterDefinition(
            AdaptiveTriggerParameterKind kind,
            string label,
            double defaultValuePercent)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            DefaultValuePercent = Math.Clamp(
                defaultValuePercent,
                AppConstants.AdaptiveTriggers.OverlaySliderMinValue,
                AppConstants.AdaptiveTriggers.OverlaySliderMaxValue);
        }

        public AdaptiveTriggerParameterKind Kind { get; }
        public string Label { get; }
        public double DefaultValuePercent { get; }
    }

    internal sealed class AdaptiveTriggerModeDefinition
    {
        public AdaptiveTriggerModeDefinition(
            AdaptiveTriggerMode mode,
            string displayName,
            IReadOnlyList<AdaptiveTriggerParameterDefinition> parameters)
        {
            Mode = mode;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? mode.ToString() : displayName.Trim();
            Parameters = parameters ?? Array.Empty<AdaptiveTriggerParameterDefinition>();
        }

        public AdaptiveTriggerMode Mode { get; }
        public int ModeIndex => (int)Mode;
        public string DisplayName { get; }
        public IReadOnlyList<AdaptiveTriggerParameterDefinition> Parameters { get; }
    }

    internal static class AdaptiveTriggerNames
    {
        public const string TriggerForceFeedbackL = "triggerForceFeedbackL";
        public const string TriggerForceFeedbackR = "triggerForceFeedbackR";

        public static string GetDefault(AdaptiveTriggerSide side)
        {
            return side == AdaptiveTriggerSide.Right
                ? TriggerForceFeedbackR
                : TriggerForceFeedbackL;
        }
    }

    internal static class AdaptiveTriggerModeCatalog
    {
        private static readonly IReadOnlyList<AdaptiveTriggerModeDefinition> DefinitionsInternal =
            new[]
            {
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Off,
                    "OFF",
                    Array.Empty<AdaptiveTriggerParameterDefinition>()),
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Feedback,
                    "Feedback",
                    new[]
                    {
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.FeedbackStart,
                            "Debut",
                            AppConstants.AdaptiveTriggers.FeedbackDefaultStartPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.FeedbackForce,
                            "Force",
                            AppConstants.AdaptiveTriggers.FeedbackDefaultForcePercent)
                    }),
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Weapon,
                    "Weapon",
                    new[]
                    {
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.WeaponStart,
                            "Debut",
                            AppConstants.AdaptiveTriggers.WeaponDefaultStartPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.WeaponEnd,
                            "Fin",
                            AppConstants.AdaptiveTriggers.WeaponDefaultEndPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.WeaponForce,
                            "Force",
                            AppConstants.AdaptiveTriggers.WeaponDefaultForcePercent)
                    }),
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Bow,
                    "Bow",
                    new[]
                    {
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.BowStart,
                            "Debut",
                            AppConstants.AdaptiveTriggers.BowDefaultStartPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.BowEnd,
                            "Fin",
                            AppConstants.AdaptiveTriggers.BowDefaultEndPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.BowForce,
                            "Force",
                            AppConstants.AdaptiveTriggers.BowDefaultForcePercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.BowSnapForce,
                            "Retour",
                            AppConstants.AdaptiveTriggers.BowDefaultSnapForcePercent)
                    }),
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Galloping,
                    "Galloping",
                    new[]
                    {
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.GallopingStart,
                            "Debut",
                            AppConstants.AdaptiveTriggers.GallopingDefaultStartPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.GallopingEnd,
                            "Fin",
                            AppConstants.AdaptiveTriggers.GallopingDefaultEndPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.GallopingFirstFoot,
                            "Temps 1",
                            AppConstants.AdaptiveTriggers.GallopingDefaultFirstFootPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.GallopingSecondFoot,
                            "Temps 2",
                            AppConstants.AdaptiveTriggers.GallopingDefaultSecondFootPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.GallopingFrequency,
                            "Frequence",
                            AppConstants.AdaptiveTriggers.GallopingDefaultFrequencyPercent)
                    }),
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Vibration,
                    "Vibration",
                    new[]
                    {
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.VibrationIntensity,
                            "Intensite",
                            AppConstants.AdaptiveTriggers.VibrationDefaultIntensityPercent),
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.VibrationFrequency,
                            "Frequence",
                            AppConstants.AdaptiveTriggers.VibrationDefaultFrequencyPercent)
                    }),
                new AdaptiveTriggerModeDefinition(
                    AdaptiveTriggerMode.Block,
                    "Block",
                    new[]
                    {
                        new AdaptiveTriggerParameterDefinition(
                            AdaptiveTriggerParameterKind.BlockPosition,
                            "Niveau",
                            AppConstants.AdaptiveTriggers.BlockDefaultPositionPercent)
                    })
            };

        private static readonly IReadOnlyList<AdaptiveTriggerParameterDefinition> AllParametersInternal =
            DefinitionsInternal
                .SelectMany(definition => definition.Parameters)
                .GroupBy(parameter => parameter.Kind)
                .Select(group => group.First())
                .ToArray();

        public static IReadOnlyList<AdaptiveTriggerModeDefinition> Definitions => DefinitionsInternal;
        public static IReadOnlyList<AdaptiveTriggerParameterDefinition> AllParameters => AllParametersInternal;
        public static int ModeCount => DefinitionsInternal.Count;

        public static AdaptiveTriggerModeDefinition GetByIndex(int modeIndex)
        {
            int clamped = Math.Clamp(modeIndex, 0, DefinitionsInternal.Count - 1);
            return DefinitionsInternal[clamped];
        }

        public static AdaptiveTriggerModeDefinition GetByMode(AdaptiveTriggerMode mode)
        {
            return GetByIndex((int)mode);
        }

        public static int ClampModeIndex(double rawModeIndex)
        {
            return Math.Clamp(
                (int)Math.Round(rawModeIndex, MidpointRounding.AwayFromZero),
                0,
                DefinitionsInternal.Count - 1);
        }

        public static string GetDisplayName(int modeIndex)
        {
            return GetByIndex(modeIndex).DisplayName;
        }
    }
}
