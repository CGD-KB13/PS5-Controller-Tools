using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PS5_Controller_Tools.Vibration
{
    public partial class VibrationMotorOverlay : UserControl
    {
        private const double SliderThumbSize = 12.0;
        private const double BubbleHorizontalGap = 6.0;

        private static readonly Brush EnabledTextBrush = CreateBrush("#002060");
        private static readonly Brush DisabledTextBrush = CreateBrush("#7A8BA5");

        private bool _suppressStateChanged;
        private bool _isSliderBubbleVisible;

        public event EventHandler<VibrationMotorChangedEventArgs>? MotorStateChanged;

        public VibrationMotorOverlay()
        {
            InitializeComponent();
            Loaded += VibrationMotorOverlay_Loaded;
        }

        public static readonly DependencyProperty LogicalNameProperty =
            DependencyProperty.Register(
                nameof(LogicalName),
                typeof(string),
                typeof(VibrationMotorOverlay),
                new PropertyMetadata(VibrationMotorNames.ControllerVibratorL));

        public string LogicalName
        {
            get => (string)GetValue(LogicalNameProperty);
            set => SetValue(LogicalNameProperty, value);
        }

        public static readonly DependencyProperty MotorSideProperty =
            DependencyProperty.Register(
                nameof(MotorSide),
                typeof(VibrationMotorSide),
                typeof(VibrationMotorOverlay),
                new PropertyMetadata(VibrationMotorSide.Left));

        public VibrationMotorSide MotorSide
        {
            get => (VibrationMotorSide)GetValue(MotorSideProperty);
            set => SetValue(MotorSideProperty, value);
        }

        public static readonly DependencyProperty BubblePlacementProperty =
            DependencyProperty.Register(
                nameof(BubblePlacement),
                typeof(VibrationBubblePlacement),
                typeof(VibrationMotorOverlay),
                new PropertyMetadata(VibrationBubblePlacement.Right, OnBubblePlacementChanged));

        public VibrationBubblePlacement BubblePlacement
        {
            get => (VibrationBubblePlacement)GetValue(BubblePlacementProperty);
            set => SetValue(BubblePlacementProperty, value);
        }

        public double IntensityPercent
        {
            get => IntensitySlider.Value;
            set => IntensitySlider.Value = Math.Clamp(
                value,
                AppConstants.Vibration.OverlaySliderMinValue,
                AppConstants.Vibration.OverlaySliderMaxValue);
        }

        public bool IsMotorEnabled
        {
            get => EnableMotorCheckBox.IsChecked == true;
            set => EnableMotorCheckBox.IsChecked = value;
        }

        public VibrationMotorState GetState()
        {
            VibrationMotorSide side = MotorSide;
            return new VibrationMotorState(
                side,
                ResolveLogicalName(side),
                IsMotorEnabled,
                IsMotorEnabled ? IntensityPercent : AppConstants.Vibration.OverlaySliderMinValue);
        }

        public void ResetToMinimum()
        {
            ApplyState(isEnabled: false, intensityPercent: AppConstants.Vibration.OverlaySliderMinValue, raiseStateChanged: false);
        }

        private static void OnBubblePlacementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VibrationMotorOverlay overlay)
                overlay.UpdateBubblePosition();
        }

        private void VibrationMotorOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();
        }

        private void SliderVisualHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateIntensityVisuals();
        }

        private void EnableMotorCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();
            RaiseMotorStateChangedIfNeeded();
        }

        private void EnableMotorCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _suppressStateChanged = true;
            try
            {
                IntensitySlider.Value = AppConstants.Vibration.OverlaySliderMinValue;
                HideBubble();
                UpdateVisualState();
            }
            finally
            {
                _suppressStateChanged = false;
            }

            RaiseMotorStateChangedIfNeeded();
        }

        private void IntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;

            UpdateIntensityVisuals();
            RaiseMotorStateChangedIfNeeded();
        }

        private void IntensitySlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsMotorEnabled)
                return;

            _isSliderBubbleVisible = true;
            UpdateBubblePosition();
        }

        private void IntensitySlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HideBubble();
        }

        private void IntensitySlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            HideBubble();
        }

        private void ApplyState(bool isEnabled, double intensityPercent, bool raiseStateChanged)
        {
            _suppressStateChanged = true;
            try
            {
                EnableMotorCheckBox.IsChecked = isEnabled;
                IntensitySlider.Value = isEnabled
                    ? Math.Clamp(intensityPercent, AppConstants.Vibration.OverlaySliderMinValue, AppConstants.Vibration.OverlaySliderMaxValue)
                    : AppConstants.Vibration.OverlaySliderMinValue;

                HideBubble();
                UpdateVisualState();
            }
            finally
            {
                _suppressStateChanged = false;
            }

            if (raiseStateChanged)
                RaiseMotorStateChanged();
        }

        private void UpdateVisualState()
        {
            bool isEnabled = IsMotorEnabled;
            IntensitySlider.IsEnabled = isEnabled;
            TrackContainer.Opacity = isEnabled ? 1.0 : 0.45;
            UpdateIntensityVisuals();
        }

        private void UpdateIntensityVisuals()
        {
            if (IntensityTrack == null || IntensityFill == null || IntensityPercentText == null)
                return;

            double ratio = GetIntensityRatio();
            double fillHeight = IntensityTrack.ActualHeight * ratio;
            if (fillHeight < 0.0)
                fillHeight = 0.0;

            IntensityFill.Height = fillHeight;
            IntensityPercentText.Text = Math.Round(ratio * 100.0).ToString("0", CultureInfo.InvariantCulture);
            UpdateBubblePosition();
        }

        private void UpdateBubblePosition()
        {
            if (IntensityBubble == null || IntensityBubbleLayer == null || SliderVisualHost == null || TrackContainer == null)
                return;

            if (!_isSliderBubbleVisible || !IsMotorEnabled)
            {
                IntensityBubble.Visibility = Visibility.Collapsed;
                return;
            }

            if (IntensityBubble.Visibility != Visibility.Visible)
            {
                IntensityBubble.Visibility = Visibility.Hidden;
                IntensityBubble.UpdateLayout();
            }

            double bubbleWidth = IntensityBubble.ActualWidth;
            double bubbleHeight = IntensityBubble.ActualHeight;

            if (bubbleWidth <= 0.0 || bubbleHeight <= 0.0)
            {
                IntensityBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bubbleWidth = IntensityBubble.DesiredSize.Width;
                bubbleHeight = IntensityBubble.DesiredSize.Height;
            }

            double hostWidth = SliderVisualHost.ActualWidth;
            double hostHeight = SliderVisualHost.ActualHeight;
            double trackHeight = TrackContainer.ActualHeight;

            if (hostWidth <= 0.0 || hostHeight <= 0.0 || trackHeight <= 0.0)
            {
                IntensityBubble.Visibility = Visibility.Collapsed;
                return;
            }

            double ratio = GetIntensityRatio();
            double trackTop = (hostHeight - trackHeight) / 2.0;
            double thumbTravelHeight = Math.Max(0.0, trackHeight - SliderThumbSize);
            double thumbCenterY = trackTop + ((1.0 - ratio) * thumbTravelHeight) + (SliderThumbSize / 2.0);
            double bubbleTop = thumbCenterY - (bubbleHeight / 2.0);
            bubbleTop = Math.Clamp(bubbleTop, 0.0, Math.Max(0.0, hostHeight - bubbleHeight));

            double trackCenterX = hostWidth / 2.0;
            double bubbleLeft = BubblePlacement == VibrationBubblePlacement.Right
                ? trackCenterX + (SliderThumbSize / 2.0) + BubbleHorizontalGap
                : trackCenterX - (SliderThumbSize / 2.0) - BubbleHorizontalGap - bubbleWidth;

            bubbleLeft = Math.Clamp(bubbleLeft, 0.0, Math.Max(0.0, hostWidth - bubbleWidth));

            Canvas.SetLeft(IntensityBubble, bubbleLeft);
            Canvas.SetTop(IntensityBubble, bubbleTop);
            IntensityBubble.Visibility = Visibility.Visible;
        }

        private void HideBubble()
        {
            _isSliderBubbleVisible = false;

            if (IntensityBubble != null)
                IntensityBubble.Visibility = Visibility.Collapsed;
        }

        private void RaiseMotorStateChangedIfNeeded()
        {
            if (_suppressStateChanged)
                return;

            RaiseMotorStateChanged();
        }

        private void RaiseMotorStateChanged()
        {
            MotorStateChanged?.Invoke(this, new VibrationMotorChangedEventArgs(GetState()));
        }

        private double GetIntensityRatio()
        {
            double range = IntensitySlider.Maximum - IntensitySlider.Minimum;
            if (range <= 0.0)
                return 0.0;

            double value = IsMotorEnabled
                ? IntensitySlider.Value
                : AppConstants.Vibration.OverlaySliderMinValue;

            double ratio = (value - IntensitySlider.Minimum) / range;
            return Math.Clamp(ratio, 0.0, 1.0);
        }

        private string ResolveLogicalName(VibrationMotorSide side)
        {
            return string.IsNullOrWhiteSpace(LogicalName)
                ? VibrationMotorNames.GetDefault(side)
                : LogicalName.Trim();
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
