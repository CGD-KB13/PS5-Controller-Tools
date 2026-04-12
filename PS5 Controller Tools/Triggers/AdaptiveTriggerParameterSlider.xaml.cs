using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PS5_Controller_Tools.Triggers
{
    internal sealed class AdaptiveTriggerParameterValueChangedEventArgs : EventArgs
    {
        public AdaptiveTriggerParameterValueChangedEventArgs(double valuePercent)
        {
            ValuePercent = valuePercent;
        }

        public double ValuePercent { get; }
    }

    public partial class AdaptiveTriggerParameterSlider : UserControl
    {
        private const double SliderThumbSize = 12.0;
        private const double BubbleTop = 0.0;

        private bool _suppressValueChanged;
        private bool _isBubbleVisible;

        internal event EventHandler<AdaptiveTriggerParameterValueChangedEventArgs>? ValuePercentChanged;

        public AdaptiveTriggerParameterSlider()
        {
            InitializeComponent();
            Loaded += AdaptiveTriggerParameterSlider_Loaded;
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(AdaptiveTriggerParameterSlider),
                new PropertyMetadata(string.Empty, OnLabelChanged));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public double ValuePercent
        {
            get => ValueSlider.Value;
            set => ValueSlider.Value = Math.Clamp(
                value,
                AppConstants.AdaptiveTriggers.OverlaySliderMinValue,
                AppConstants.AdaptiveTriggers.OverlaySliderMaxValue);
        }

        public void SetValueSilently(double valuePercent)
        {
            _suppressValueChanged = true;
            try
            {
                ValuePercent = valuePercent;
                HideBubble();
                UpdateVisuals();
            }
            finally
            {
                _suppressValueChanged = false;
            }
        }

        public void ResetToMinimum()
        {
            SetValueSilently(AppConstants.AdaptiveTriggers.OverlaySliderMinValue);
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AdaptiveTriggerParameterSlider slider)
                slider.UpdateLabel();
        }

        private void AdaptiveTriggerParameterSlider_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLabel();
            UpdateVisuals();
        }

        private void SliderVisualHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }

        private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;

            UpdateVisuals();
            RaiseValuePercentChangedIfNeeded();
        }

        private void ValueSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isBubbleVisible = true;
            UpdateBubblePosition();
        }

        private void ValueSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HideBubble();
        }

        private void ValueSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            HideBubble();
        }

        private void UpdateLabel()
        {
            if (ParameterLabelText != null)
                ParameterLabelText.Text = string.IsNullOrWhiteSpace(Label) ? string.Empty : Label.Trim();
        }

        private void UpdateVisuals()
        {
            if (ValueTrack == null || ValueFill == null || ValueBubbleText == null)
                return;

            double ratio = GetValueRatio();
            double fillWidth = ValueTrack.ActualWidth * ratio;
            if (fillWidth < 0.0)
                fillWidth = 0.0;

            ValueFill.Width = fillWidth;
            ValueBubbleText.Text = Math.Round(ratio * 100.0).ToString("0", CultureInfo.InvariantCulture);
            UpdateBubblePosition();
        }

        private void UpdateBubblePosition()
        {
            if (ValueBubble == null || BubbleLayer == null || SliderVisualHost == null || TrackContainer == null)
                return;

            if (!_isBubbleVisible)
            {
                ValueBubble.Visibility = Visibility.Collapsed;
                return;
            }

            if (ValueBubble.Visibility != Visibility.Visible)
            {
                ValueBubble.Visibility = Visibility.Hidden;
                ValueBubble.UpdateLayout();
            }

            double bubbleWidth = ValueBubble.ActualWidth;
            double bubbleHeight = ValueBubble.ActualHeight;
            if (bubbleWidth <= 0.0 || bubbleHeight <= 0.0)
            {
                ValueBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bubbleWidth = ValueBubble.DesiredSize.Width;
                bubbleHeight = ValueBubble.DesiredSize.Height;
            }

            double hostWidth = SliderVisualHost.ActualWidth;
            double trackWidth = TrackContainer.ActualWidth;
            if (hostWidth <= 0.0 || trackWidth <= 0.0)
            {
                ValueBubble.Visibility = Visibility.Collapsed;
                return;
            }

            double ratio = GetValueRatio();
            double trackLeft = (hostWidth - trackWidth) / 2.0;
            double thumbTravelWidth = Math.Max(0.0, trackWidth - SliderThumbSize);
            double thumbCenterX = trackLeft + (ratio * thumbTravelWidth) + (SliderThumbSize / 2.0);
            double bubbleLeft = thumbCenterX - (bubbleWidth / 2.0);
            bubbleLeft = Math.Clamp(bubbleLeft, 0.0, Math.Max(0.0, hostWidth - bubbleWidth));

            BubbleLayer.Width = hostWidth;
            BubbleLayer.Height = SliderVisualHost.ActualHeight;
            Canvas.SetLeft(ValueBubble, bubbleLeft);
            Canvas.SetTop(ValueBubble, BubbleTop);
            ValueBubble.Visibility = Visibility.Visible;
        }

        private void HideBubble()
        {
            _isBubbleVisible = false;

            if (ValueBubble != null)
                ValueBubble.Visibility = Visibility.Collapsed;
        }

        private void RaiseValuePercentChangedIfNeeded()
        {
            if (_suppressValueChanged)
                return;

            ValuePercentChanged?.Invoke(
                this,
                new AdaptiveTriggerParameterValueChangedEventArgs(ValuePercent));
        }

        private double GetValueRatio()
        {
            double range = ValueSlider.Maximum - ValueSlider.Minimum;
            if (range <= 0.0)
                return 0.0;

            double ratio = (ValueSlider.Value - ValueSlider.Minimum) / range;
            return Math.Clamp(ratio, 0.0, 1.0);
        }
    }
}
