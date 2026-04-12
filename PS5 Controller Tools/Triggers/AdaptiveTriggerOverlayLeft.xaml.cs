using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PS5_Controller_Tools.Triggers
{
    public partial class AdaptiveTriggerOverlayLeft : UserControl, IAdaptiveTriggerOverlay
    {
        private const double ModeThumbSize = 12.0;
        private const double BubbleHorizontalGap = 6.0;
        private const double ParameterSliderWidth = 104.0;
        private const double ParameterSliderHeight = 34.0;

        private static readonly Brush ActiveTickBrush = CreateBrush("#002060");
        private static readonly Brush InactiveTickBrush = CreateBrush("#A0A0A0");

        private readonly Dictionary<AdaptiveTriggerParameterKind, double> _parameterValues = CreateDefaultParameterValueMap();
        private readonly List<(int ModeIndex, TextBlock TextBlock)> _modeTickLabels = new();

        private bool _suppressStateChanged;
        private bool _isModeBubbleVisible;

        public event EventHandler<AdaptiveTriggerStateChangedEventArgs>? TriggerStateChanged;

        public AdaptiveTriggerOverlayLeft()
        {
            InitializeComponent();
            Loaded += AdaptiveTriggerOverlay_Loaded;
            SizeChanged += AdaptiveTriggerOverlay_SizeChanged;
        }

        public static readonly DependencyProperty LogicalNameProperty =
            DependencyProperty.Register(
                nameof(LogicalName),
                typeof(string),
                typeof(AdaptiveTriggerOverlayLeft),
                new PropertyMetadata(AdaptiveTriggerNames.TriggerForceFeedbackL));

        public string LogicalName
        {
            get => (string)GetValue(LogicalNameProperty);
            set => SetValue(LogicalNameProperty, value);
        }

        public int SelectedModeIndex
        {
            get => AdaptiveTriggerModeCatalog.ClampModeIndex(ModeSlider.Value);
            set => ModeSlider.Value = Math.Clamp(value, 0, AdaptiveTriggerModeCatalog.ModeCount - 1);
        }

        public AdaptiveTriggerState GetState()
        {
            AdaptiveTriggerSide side = AdaptiveTriggerSide.Left;
            return new AdaptiveTriggerState(
                side,
                ResolveLogicalName(),
                AdaptiveTriggerModeCatalog.GetByIndex(SelectedModeIndex).Mode,
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.FeedbackStart),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.FeedbackForce),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.WeaponStart),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.WeaponEnd),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.WeaponForce),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.BowStart),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.BowEnd),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.BowForce),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.BowSnapForce),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.GallopingStart),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.GallopingEnd),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.GallopingFirstFoot),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.GallopingSecondFoot),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.GallopingFrequency),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.VibrationIntensity),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.VibrationFrequency),
                GetStoredParameterPercent(AdaptiveTriggerParameterKind.BlockPosition));
        }

        public void ResetToOff()
        {
            ApplySelectedMode(AdaptiveTriggerMode.Off, raiseStateChanged: false);
        }

        private void AdaptiveTriggerOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            OverlayCanvas.Width = ActualWidth;
            OverlayCanvas.Height = ActualHeight;
            ModeSlider.Maximum = AdaptiveTriggerModeCatalog.ModeCount - 1;
            RebuildModeTickLabels();
            RefreshParameterPanel();
            UpdateModeVisuals();
        }

        private void AdaptiveTriggerOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            OverlayCanvas.Width = ActualWidth;
            OverlayCanvas.Height = ActualHeight;
            UpdateModeBubblePosition();
        }

        private void ModeTrackShell_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateModeVisuals();
        }

        private void ModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;

            int snappedModeIndex = AdaptiveTriggerModeCatalog.ClampModeIndex(e.NewValue);
            if (Math.Abs(snappedModeIndex - ModeSlider.Value) > double.Epsilon)
            {
                _suppressStateChanged = true;
                try
                {
                    ModeSlider.Value = snappedModeIndex;
                }
                finally
                {
                    _suppressStateChanged = false;
                }
            }

            RefreshParameterPanel();
            UpdateModeVisuals();
            RaiseTriggerStateChangedIfNeeded();
        }

        private void ModeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isModeBubbleVisible = true;
            UpdateModeBubblePosition();
        }

        private void ModeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HideModeBubble();
        }

        private void ModeSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            HideModeBubble();
        }

        private void ParameterSlider_ValuePercentChanged(object? sender, AdaptiveTriggerParameterValueChangedEventArgs e)
        {
            if (sender is not AdaptiveTriggerParameterSlider slider || slider.Tag is not AdaptiveTriggerParameterDefinition definition)
                return;

            SetStoredParameterPercent(definition.Kind, e.ValuePercent);
            RaiseTriggerStateChangedIfNeeded();
        }

        private void ApplySelectedMode(AdaptiveTriggerMode mode, bool raiseStateChanged)
        {
            _suppressStateChanged = true;
            try
            {
                SelectedModeIndex = (int)mode;
                HideModeBubble();
                RefreshParameterPanel();
                UpdateModeVisuals();
            }
            finally
            {
                _suppressStateChanged = false;
            }

            if (raiseStateChanged)
                RaiseTriggerStateChanged();
        }

        private void RefreshParameterPanel()
        {
            if (ParameterStack == null)
                return;

            ParameterStack.Children.Clear();

            AdaptiveTriggerModeDefinition definition = AdaptiveTriggerModeCatalog.GetByIndex(SelectedModeIndex);
            if (definition.Mode == AdaptiveTriggerMode.Off)
            {
                ParameterStack.Visibility = Visibility.Collapsed;
                return;
            }

            ParameterStack.Visibility = Visibility.Visible;

            foreach (AdaptiveTriggerParameterDefinition parameter in definition.Parameters)
            {
                var slider = new AdaptiveTriggerParameterSlider
                {
                    Width = ParameterSliderWidth,
                    Height = ParameterSliderHeight,
                    Label = parameter.Label,
                    Margin = new Thickness(0, 0, 0, -2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = parameter
                };

                slider.SetValueSilently(GetStoredParameterPercent(parameter.Kind));
                slider.ValuePercentChanged += ParameterSlider_ValuePercentChanged;
                ParameterStack.Children.Add(slider);
            }
        }

        private void UpdateModeVisuals()
        {
            if (ModeTrack == null || ModeFill == null || ModeBubbleText == null)
                return;

            double ratio = GetModeRatio();
            double fillHeight = ModeTrack.ActualHeight * ratio;
            if (fillHeight < 0.0)
                fillHeight = 0.0;

            ModeFill.Height = fillHeight;
            ModeBubbleText.Text = AdaptiveTriggerModeCatalog.GetDisplayName(SelectedModeIndex);
            UpdateModeTickVisuals();
            UpdateModeBubblePosition();
        }

        private void RebuildModeTickLabels()
        {
            if (ModeNumbersGrid == null)
                return;

            _modeTickLabels.Clear();
            ModeNumbersGrid.Children.Clear();
            ModeNumbersGrid.Rows = AdaptiveTriggerModeCatalog.ModeCount;

            for (int modeIndex = AdaptiveTriggerModeCatalog.ModeCount - 1; modeIndex >= 0; modeIndex--)
            {
                var textBlock = new TextBlock
                {
                    Text = modeIndex.ToString(CultureInfo.InvariantCulture),
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = InactiveTickBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                ModeNumbersGrid.Children.Add(textBlock);
                _modeTickLabels.Add((modeIndex, textBlock));
            }
        }

        private void UpdateModeTickVisuals()
        {
            foreach ((int modeIndex, TextBlock textBlock) in _modeTickLabels)
                textBlock.Foreground = modeIndex == SelectedModeIndex ? ActiveTickBrush : InactiveTickBrush;
        }

        private void UpdateModeBubblePosition()
        {
            if (ModeBubble == null || RootSurface == null || ModeTrackShell == null)
                return;

            if (!_isModeBubbleVisible)
            {
                ModeBubble.Visibility = Visibility.Collapsed;
                return;
            }

            if (ModeBubble.Visibility != Visibility.Visible)
            {
                ModeBubble.Visibility = Visibility.Hidden;
                ModeBubble.UpdateLayout();
            }

            double bubbleWidth = ModeBubble.ActualWidth;
            double bubbleHeight = ModeBubble.ActualHeight;
            if (bubbleWidth <= 0.0 || bubbleHeight <= 0.0)
            {
                ModeBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bubbleWidth = ModeBubble.DesiredSize.Width;
                bubbleHeight = ModeBubble.DesiredSize.Height;
            }

            double trackHeight = ModeTrackShell.ActualHeight;
            double rootWidth = ActualWidth;
            double rootHeight = ActualHeight;
            if (trackHeight <= 0.0 || rootWidth <= 0.0 || rootHeight <= 0.0)
            {
                ModeBubble.Visibility = Visibility.Collapsed;
                return;
            }

            Point trackPosition = ModeTrackShell.TransformToVisual(RootSurface).Transform(new Point(0.0, 0.0));
            double ratio = GetModeRatio();
            double thumbTravelHeight = Math.Max(0.0, trackHeight - ModeThumbSize);
            double thumbCenterY = trackPosition.Y + ((1.0 - ratio) * thumbTravelHeight) + (ModeThumbSize / 2.0);
            double bubbleTop = thumbCenterY - (bubbleHeight / 2.0);
            bubbleTop = Math.Clamp(bubbleTop, 0.0, Math.Max(0.0, rootHeight - bubbleHeight));

            double bubbleLeft = trackPosition.X + ModeTrackShell.ActualWidth + BubbleHorizontalGap;
            bubbleLeft = Math.Clamp(bubbleLeft, 0.0, Math.Max(0.0, rootWidth - bubbleWidth));

            Canvas.SetLeft(ModeBubble, bubbleLeft);
            Canvas.SetTop(ModeBubble, bubbleTop);
            ModeBubble.Visibility = Visibility.Visible;
        }

        private void HideModeBubble()
        {
            _isModeBubbleVisible = false;

            if (ModeBubble != null)
                ModeBubble.Visibility = Visibility.Collapsed;
        }

        private void RaiseTriggerStateChangedIfNeeded()
        {
            if (_suppressStateChanged)
                return;

            RaiseTriggerStateChanged();
        }

        private void RaiseTriggerStateChanged()
        {
            TriggerStateChanged?.Invoke(this, new AdaptiveTriggerStateChangedEventArgs(GetState()));
        }

        private double GetModeRatio()
        {
            double maxIndex = AdaptiveTriggerModeCatalog.ModeCount - 1;
            if (maxIndex <= 0.0)
                return 0.0;

            return Math.Clamp(SelectedModeIndex / maxIndex, 0.0, 1.0);
        }

        private double GetStoredParameterPercent(AdaptiveTriggerParameterKind kind)
        {
            return _parameterValues.TryGetValue(kind, out double value)
                ? value
                : 0.0;
        }

        private void SetStoredParameterPercent(AdaptiveTriggerParameterKind kind, double valuePercent)
        {
            _parameterValues[kind] = Math.Clamp(
                valuePercent,
                AppConstants.AdaptiveTriggers.OverlaySliderMinValue,
                AppConstants.AdaptiveTriggers.OverlaySliderMaxValue);
        }

        private string ResolveLogicalName()
        {
            return string.IsNullOrWhiteSpace(LogicalName)
                ? AdaptiveTriggerNames.TriggerForceFeedbackL
                : LogicalName.Trim();
        }

        private static Dictionary<AdaptiveTriggerParameterKind, double> CreateDefaultParameterValueMap()
        {
            var values = new Dictionary<AdaptiveTriggerParameterKind, double>();
            foreach (AdaptiveTriggerParameterDefinition parameter in AdaptiveTriggerModeCatalog.AllParameters)
                values[parameter.Kind] = parameter.DefaultValuePercent;

            return values;
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
