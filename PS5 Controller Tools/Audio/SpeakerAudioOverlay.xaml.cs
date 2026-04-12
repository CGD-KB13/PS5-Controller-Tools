using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PS5_Controller_Tools
{
    public partial class SpeakerAudioOverlay : UserControl
    {
        private const double SliderThumbWidth = 16.0;
        private const double BubbleVerticalOffset = 28.0;
        private const double TrackNameMarqueeGap = 20.0;
        private const double TrackNamePixelsPerSecond = 28.0;
        private const double AudioStatusMarqueeGap = 24.0;
        private const double AudioStatusPixelsPerSecond = 34.0;

        private static readonly Brush AccentBlueBrush = CreateBrush("#0070C0");
        private static readonly Brush AccentBlueBorderBrush = CreateBrush("#002060");
        private static readonly Brush PauseBrush = CreateBrush("#0070C0");
        private static readonly Brush PauseBorderBrush = CreateBrush("#002060");
        private static readonly Brush StopBrush = CreateBrush("#0070C0");
        private static readonly Brush StopBorderBrush = CreateBrush("#002060");
        private static readonly Brush PlayBadgeBrush = CreateBrush("#D8F5E3");
        private static readonly Brush PlayBadgeBorderBrush = CreateBrush("#97D8AF");
        private static readonly Brush PlayBadgeTextBrush = CreateBrush("#146C43");
        private static readonly Brush PauseBadgeBrush = CreateBrush("#D8F5E3");
        private static readonly Brush PauseBadgeBorderBrush = CreateBrush("#97D8AF");
        private static readonly Brush PauseBadgeTextBrush = CreateBrush("#146C43");
        private static readonly Brush StopBadgeBrush = CreateBrush("#D8F5E3");
        private static readonly Brush StopBadgeBorderBrush = CreateBrush("#97D8AF");
        private static readonly Brush StopBadgeTextBrush = CreateBrush("#146C43");

        private bool _suppressVolumeChanged;
        private bool _isSliderBubbleVisible;
        private bool _isTrackMarqueeRunning;
        private double _lastTrackViewportWidth = -1.0;
        private string _lastTrackNameMeasured = string.Empty;
        private TimeSpan _currentPlaybackPosition = TimeSpan.Zero;
        private TimeSpan _currentPlaybackDuration = TimeSpan.Zero;
        private string _currentPlaybackTrackName = GetDefaultPlaybackTrackName();
        private bool _isAudioStatusMarqueeRunning;
        private double _lastAudioStatusViewportWidth = -1.0;
        private string _lastAudioStatusMeasured = string.Empty;
        private string _currentAudioStatusMessage = string.Empty;

        public event EventHandler<double>? VolumeChanged;
        public event EventHandler? PlayRequested;
        public event EventHandler? PauseRequested;
        public event EventHandler? StopRequested;

        public string CurrentAudioStatusMessage => _currentAudioStatusMessage;

        public SpeakerAudioOverlay()
        {
            InitializeComponent();
            Loaded += SpeakerAudioOverlay_Loaded;
        }

        public double Volume
        {
            get => VolumeSlider.Value;
            set
            {
                double clamped = Math.Clamp(value, VolumeSlider.Minimum, VolumeSlider.Maximum);

                if (Math.Abs(VolumeSlider.Value - clamped) < double.Epsilon)
                {
                    UpdateVolumeVisuals();
                    return;
                }

                _suppressVolumeChanged = true;
                try
                {
                    VolumeSlider.Value = clamped;
                    UpdateVolumeVisuals();
                }
                finally
                {
                    _suppressVolumeChanged = false;
                }
            }
        }

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(
                nameof(IsPlaying),
                typeof(bool),
                typeof(SpeakerAudioOverlay),
                new PropertyMetadata(false, OnPlaybackStateChanged));

        public bool IsPaused
        {
            get => (bool)GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        public static readonly DependencyProperty IsPausedProperty =
            DependencyProperty.Register(
                nameof(IsPaused),
                typeof(bool),
                typeof(SpeakerAudioOverlay),
                new PropertyMetadata(false, OnPlaybackStateChanged));

        public void SetPlaybackState(bool isPlaying, bool isPaused)
        {
            IsPlaying = isPlaying;
            IsPaused = isPaused;
        }

        public void SetPlaybackTrackName(string? trackName)
        {
            _currentPlaybackTrackName = string.IsNullOrWhiteSpace(trackName)
                ? GetDefaultPlaybackTrackName()
                : trackName.Trim();

            UpdatePlaybackProgressVisuals();
        }

        public void UpdatePlaybackProgress(TimeSpan position, TimeSpan duration)
        {
            _currentPlaybackPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            _currentPlaybackDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
            UpdatePlaybackProgressVisuals();
        }

        public void ResetPlaybackProgress()
        {
            _currentPlaybackPosition = TimeSpan.Zero;
            _currentPlaybackDuration = TimeSpan.Zero;
            _currentPlaybackTrackName = GetDefaultPlaybackTrackName();
            UpdatePlaybackProgressVisuals();
        }

        private void SpeakerAudioOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVolumeVisuals();
            UpdateTransportButtons();
            UpdatePlaybackProgressVisuals();
            UpdateAudioStatusVisuals();
        }

        private static void OnPlaybackStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpeakerAudioOverlay overlay)
            {
                overlay.UpdateTransportButtons();
                overlay.UpdatePlaybackProgressVisuals();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;

            UpdateVolumeVisuals();

            if (_suppressVolumeChanged)
                return;

            VolumeChanged?.Invoke(this, VolumeSlider.Value);
        }

        private void VolumeSlider_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVolumeVisuals();
        }

        private void PlaybackProgressTrack_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePlaybackProgressVisuals();
        }

        private void PlaybackTrackViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTrackNameMarquee(forceRestart: true);
        }

        private void VolumeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSliderBubbleVisible = true;
            UpdateVolumeBubble();
        }

        private void VolumeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HideVolumeBubble();
        }

        private void VolumeSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            HideVolumeBubble();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlayRequested?.Invoke(this, EventArgs.Empty);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            PauseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateVolumeVisuals()
        {
            if (VolumeTrack == null || VolumeFill == null || VolumeSlider == null || VolumePercentText == null)
                return;

            double range = VolumeSlider.Maximum - VolumeSlider.Minimum;

            if (range <= 0)
            {
                VolumeFill.Width = 0;
                VolumePercentText.Text = "0";
                UpdateVolumeBubble();
                return;
            }

            double ratio = (VolumeSlider.Value - VolumeSlider.Minimum) / range;
            double width = VolumeTrack.ActualWidth * ratio;

            if (width < 0)
                width = 0;

            VolumeFill.Width = width;

            int percent = (int)Math.Round(ratio * 100.0);
            VolumePercentText.Text = $"{percent}";
            UpdateVolumeBubble();
        }

        private void UpdateVolumeBubble()
        {
            if (VolumeSlider == null || VolumeBubble == null || VolumeBubbleLayer == null || VolumePercentText == null || VolumeTrack == null)
                return;

            if (!_isSliderBubbleVisible)
            {
                VolumeBubble.Visibility = Visibility.Collapsed;
                return;
            }

            double range = VolumeSlider.Maximum - VolumeSlider.Minimum;
            double ratio = range <= 0.0
                ? 0.0
                : (VolumeSlider.Value - VolumeSlider.Minimum) / range;

            ratio = Math.Clamp(ratio, 0.0, 1.0);

            if (VolumeBubble.Visibility != Visibility.Visible)
            {
                VolumeBubble.Visibility = Visibility.Hidden;
                VolumeBubble.UpdateLayout();
            }

            double bubbleWidth = VolumeBubble.ActualWidth;
            if (bubbleWidth <= 0)
            {
                VolumeBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bubbleWidth = VolumeBubble.DesiredSize.Width;
            }

            double trackWidth = VolumeTrack.ActualWidth;
            if (trackWidth <= 0)
                trackWidth = VolumeBubbleLayer.ActualWidth;

            double thumbCenterX = (ratio * Math.Max(0.0, trackWidth - SliderThumbWidth)) + (SliderThumbWidth / 2.0);
            double bubbleLeft = thumbCenterX - (bubbleWidth / 2.0);
            bubbleLeft = Math.Clamp(bubbleLeft, 0.0, Math.Max(0.0, trackWidth - bubbleWidth));

            Canvas.SetLeft(VolumeBubble, bubbleLeft);
            Canvas.SetTop(VolumeBubble, -BubbleVerticalOffset);
            VolumeBubble.Visibility = Visibility.Visible;
        }

        private void HideVolumeBubble()
        {
            _isSliderBubbleVisible = false;
            if (VolumeBubble != null)
                VolumeBubble.Visibility = Visibility.Collapsed;
        }

        private void UpdateTransportButtons()
        {
            if (PlayButton == null || PauseButton == null || StopButton == null || PlaybackStateText == null)
                return;

            PlayButton.IsEnabled = !IsPlaying;
            PauseButton.IsEnabled = IsPlaying;
            StopButton.IsEnabled = IsPlaying || IsPaused;

            if (IsPlaying)
            {
                PlaybackStateText.Text = "WAV";
                SetBadgeStyle(PlayBadgeBrush, PlayBadgeBorderBrush, PlayBadgeTextBrush);
                SetButtonStyle(PlayButton, AccentBlueBrush, AccentBlueBorderBrush);
                SetButtonStyle(PauseButton, PauseBrush, PauseBorderBrush);
                SetButtonStyle(StopButton, StopBrush, StopBorderBrush);
                return;
            }

            if (IsPaused)
            {
                PlaybackStateText.Text = "PAUSE";
                SetBadgeStyle(PauseBadgeBrush, PauseBadgeBorderBrush, PauseBadgeTextBrush);
                SetButtonStyle(PlayButton, AccentBlueBrush, AccentBlueBorderBrush);
                SetButtonStyle(PauseButton, PauseBrush, PauseBorderBrush);
                SetButtonStyle(StopButton, StopBrush, StopBorderBrush);
                return;
            }

            PlaybackStateText.Text = "STOP";
            SetBadgeStyle(StopBadgeBrush, StopBadgeBorderBrush, StopBadgeTextBrush);
            SetButtonStyle(PlayButton, AccentBlueBrush, AccentBlueBorderBrush);
            SetButtonStyle(PauseButton, PauseBrush, PauseBorderBrush);
            SetButtonStyle(StopButton, StopBrush, StopBorderBrush);
        }

        private void UpdatePlaybackProgressVisuals()
        {
            if (PlaybackInfoPanel == null || PlaybackCurrentTimeText == null || PlaybackDurationText == null || PlaybackProgressTrack == null || PlaybackProgressFill == null || PlaybackTrackNameText == null || PlaybackTrackNameTextClone == null)
                return;

            bool shouldShow = (IsPlaying || IsPaused) && _currentPlaybackDuration > TimeSpan.Zero;
            PlaybackInfoPanel.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;

            if (!shouldShow)
            {
                PlaybackProgressFill.Width = 0;
                PlaybackCurrentTimeText.Text = "0:00";
                PlaybackDurationText.Text = "0:00";
                PlaybackTrackNameText.Text = GetDefaultPlaybackTrackName();
                PlaybackTrackNameTextClone.Text = GetDefaultPlaybackTrackName();
                StopTrackNameMarquee();
                return;
            }

            TimeSpan clampedPosition = _currentPlaybackPosition > _currentPlaybackDuration
                ? _currentPlaybackDuration
                : _currentPlaybackPosition;

            PlaybackCurrentTimeText.Text = FormatPlaybackTime(clampedPosition);
            PlaybackDurationText.Text = FormatPlaybackTime(_currentPlaybackDuration);

            string trackName = NormalizeTrackName(_currentPlaybackTrackName);

            double ratio = _currentPlaybackDuration.TotalMilliseconds <= 0.0
                ? 0.0
                : clampedPosition.TotalMilliseconds / _currentPlaybackDuration.TotalMilliseconds;

            ratio = Math.Clamp(ratio, 0.0, 1.0);
            PlaybackProgressFill.Width = PlaybackProgressTrack.ActualWidth * ratio;
            UpdateTrackNameMarquee(forceRestart: false);
        }

        private void UpdateTrackNameMarquee(bool forceRestart)
        {
            if (PlaybackInfoPanel == null || PlaybackInfoPanel.Visibility != Visibility.Visible || PlaybackTrackViewport == null || PlaybackTrackMarqueePanel == null || PlaybackTrackNameText == null || PlaybackTrackNameTextClone == null || PlaybackTrackNameTransform == null || PlaybackTrackGap == null)
                return;

            string trackName = NormalizeTrackName(_currentPlaybackTrackName);
            PlaybackTrackNameText.Text = trackName;
            PlaybackTrackNameTextClone.Text = trackName;
            PlaybackTrackGap.Width = TrackNameMarqueeGap;

            PlaybackTrackNameText.UpdateLayout();
            PlaybackTrackNameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            PlaybackTrackNameTextClone.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            PlaybackTrackMarqueePanel.UpdateLayout();

            double viewportWidth = PlaybackTrackViewport.ActualWidth;
            double singleTextWidth = Math.Max(PlaybackTrackNameText.ActualWidth, PlaybackTrackNameText.DesiredSize.Width);

            if (viewportWidth <= 0)
                return;

            bool marqueeInputChanged =
                forceRestart ||
                !_isTrackMarqueeRunning ||
                !string.Equals(_lastTrackNameMeasured, trackName, StringComparison.Ordinal) ||
                Math.Abs(_lastTrackViewportWidth - viewportWidth) > 2.0;

            if (!marqueeInputChanged)
                return;

            if (viewportWidth <= 0)
                return;

            if (singleTextWidth <= viewportWidth + 1.0)
            {
                PlaybackTrackGap.Visibility = Visibility.Collapsed;
                PlaybackTrackNameTextClone.Visibility = Visibility.Collapsed;
                StopTrackNameMarquee();
                return;
            }

            PlaybackTrackGap.Visibility = Visibility.Visible;
            PlaybackTrackNameTextClone.Visibility = Visibility.Visible;

            StopTrackNameMarquee();

            _lastTrackNameMeasured = trackName;
            _lastTrackViewportWidth = viewportWidth;
            _isTrackMarqueeRunning = true;

            double travelDistance = singleTextWidth + TrackNameMarqueeGap;
            double travelSeconds = Math.Max(3.0, travelDistance / TrackNamePixelsPerSecond);

            var animation = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-travelDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0 + travelSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(-travelDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.0 + travelSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.05 + travelSeconds))));

            PlaybackTrackNameTransform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void StopTrackNameMarquee()
        {
            if (PlaybackTrackNameTransform == null)
                return;

            PlaybackTrackNameTransform.BeginAnimation(TranslateTransform.XProperty, null);
            PlaybackTrackNameTransform.X = 0.0;
            _isTrackMarqueeRunning = false;
            _lastTrackViewportWidth = -1.0;
            _lastTrackNameMeasured = string.Empty;
        }

        public void SetAudioStatusMessage(string? message, Brush foreground)
        {
            _currentAudioStatusMessage = NormalizeAudioStatusMessage(message);

            if (AudioStatusText != null)
                AudioStatusText.Foreground = foreground;

            if (AudioStatusTextClone != null)
                AudioStatusTextClone.Foreground = foreground;

            UpdateAudioStatusVisuals();
        }

        public void ClearAudioStatusMessage()
        {
            _currentAudioStatusMessage = string.Empty;

            if (AudioStatusGap != null)
                AudioStatusGap.Visibility = Visibility.Collapsed;

            if (AudioStatusTextClone != null)
                AudioStatusTextClone.Visibility = Visibility.Collapsed;

            UpdateAudioStatusVisuals();
        }

        private void AudioStatusViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAudioStatusMarquee(forceRestart: true);
        }

        private void UpdateAudioStatusVisuals()
        {
            if (AudioStatusText == null || AudioStatusTextClone == null || AudioStatusGap == null)
                return;

            AudioStatusText.Text = _currentAudioStatusMessage;
            AudioStatusTextClone.Text = _currentAudioStatusMessage;
            AudioStatusGap.Width = AudioStatusMarqueeGap;

            if (string.IsNullOrWhiteSpace(_currentAudioStatusMessage))
            {
                StopAudioStatusMarquee();
                return;
            }

            UpdateAudioStatusMarquee(forceRestart: false);
        }

        private void UpdateAudioStatusMarquee(bool forceRestart)
        {
            if (AudioStatusViewport == null ||
                AudioStatusMarqueePanel == null ||
                AudioStatusText == null ||
                AudioStatusTextClone == null ||
                AudioStatusTextTransform == null ||
                AudioStatusGap == null)
                return;

            string message = NormalizeAudioStatusMessage(_currentAudioStatusMessage);
            AudioStatusText.Text = message;
            AudioStatusTextClone.Text = message;
            AudioStatusGap.Width = AudioStatusMarqueeGap;

            AudioStatusText.UpdateLayout();
            AudioStatusText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AudioStatusTextClone.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AudioStatusMarqueePanel.UpdateLayout();

            double viewportWidth = AudioStatusViewport.ActualWidth;
            double singleTextWidth = Math.Max(AudioStatusText.ActualWidth, AudioStatusText.DesiredSize.Width);

            if (viewportWidth <= 0)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                AudioStatusGap.Visibility = Visibility.Collapsed;
                AudioStatusTextClone.Visibility = Visibility.Collapsed;
                StopAudioStatusMarquee();
                return;
            }

            if (singleTextWidth <= viewportWidth + 1.0)
            {
                AudioStatusGap.Visibility = Visibility.Collapsed;
                AudioStatusTextClone.Visibility = Visibility.Collapsed;
                StopAudioStatusMarquee();
                return;
            }

            AudioStatusGap.Visibility = Visibility.Visible;
            AudioStatusTextClone.Visibility = Visibility.Visible;

            bool marqueeInputChanged =
                forceRestart ||
                !_isAudioStatusMarqueeRunning ||
                !string.Equals(_lastAudioStatusMeasured, message, StringComparison.Ordinal) ||
                Math.Abs(_lastAudioStatusViewportWidth - viewportWidth) > 2.0;

            if (!marqueeInputChanged)
                return;

            StopAudioStatusMarquee();

            _lastAudioStatusMeasured = message;
            _lastAudioStatusViewportWidth = viewportWidth;
            _isAudioStatusMarqueeRunning = true;

            double travelDistance = singleTextWidth + AudioStatusMarqueeGap;
            double travelSeconds = Math.Max(4.0, travelDistance / AudioStatusPixelsPerSecond);

            var animation = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-travelDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0 + travelSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(-travelDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.0 + travelSeconds))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.05 + travelSeconds))));

            AudioStatusTextTransform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void StopAudioStatusMarquee()
        {
            if (AudioStatusTextTransform == null)
                return;

            AudioStatusTextTransform.BeginAnimation(TranslateTransform.XProperty, null);
            AudioStatusTextTransform.X = 0.0;
            _isAudioStatusMarqueeRunning = false;
            _lastAudioStatusViewportWidth = -1.0;
            _lastAudioStatusMeasured = string.Empty;
        }

        private static string NormalizeAudioStatusMessage(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void SetBadgeStyle(Brush background, Brush borderBrush, Brush foreground)
        {
            if (PlaybackStateBadge != null)
            {
                PlaybackStateBadge.Background = background;
                PlaybackStateBadge.BorderBrush = borderBrush;
            }

            PlaybackStateText.Foreground = foreground;
        }

        private static void SetButtonStyle(Control button, Brush background, Brush borderBrush)
        {
            button.Background = background;
            button.BorderBrush = borderBrush;
        }

        private static string FormatPlaybackTime(TimeSpan value)
        {
            int totalMinutes = (int)value.TotalMinutes;
            return $"{totalMinutes}:{value.Seconds:D2}";
        }

        private static string GetDefaultPlaybackTrackName()
        {
            return Path.GetFileName(AppConstants.Assets.TestWaveResourcePath);
        }

        private static string NormalizeTrackName(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? GetDefaultPlaybackTrackName()
                : value.Trim();
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
