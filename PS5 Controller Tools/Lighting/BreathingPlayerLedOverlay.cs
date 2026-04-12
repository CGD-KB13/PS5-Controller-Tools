using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PS5_Controller_Tools.Lighting
{
    public sealed class BreathingPlayerLedOverlay : Grid
    {
        public static readonly DependencyProperty LedColorProperty =
            DependencyProperty.Register(
                nameof(LedColor),
                typeof(Color),
                typeof(BreathingPlayerLedOverlay),
                new PropertyMetadata(Colors.Red, OnVisualPropertyChanged));

        public static readonly DependencyProperty IsLedEnabledProperty =
            DependencyProperty.Register(
                nameof(IsLedEnabled),
                typeof(bool),
                typeof(BreathingPlayerLedOverlay),
                new PropertyMetadata(false, OnVisualPropertyChanged));

        private readonly Grid _visualRoot;
        private readonly Ellipse _outerGlow;
        private readonly Ellipse _midGlow;
        private readonly Ellipse _coreGlow;
        private Storyboard? _breathingStoryboard;
        private bool _isAnimationRunning;

        public BreathingPlayerLedOverlay()
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            IsHitTestVisible = false;
            Background = Brushes.Transparent;
            SnapsToDevicePixels = false;

            _visualRoot = new Grid
            {
                Width = 40,
                Height = 12,
                Opacity = 0.70,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _outerGlow = new Ellipse
            {
                Width = 40,
                Height = 10,
                Opacity = 0.80,
                Fill = Brushes.Transparent,
                Effect = new BlurEffect { Radius = 8 }
            };

            _midGlow = new Ellipse
            {
                Width = 35,
                Height = 12,
                Opacity = 0.90,
                Fill = Brushes.Transparent
            };

            _coreGlow = new Ellipse
            {
                Width = 20,
                Height = 10,
                Opacity = 1.0,
                Fill = Brushes.Transparent,
            };

            _visualRoot.Children.Add(_outerGlow);
            _visualRoot.Children.Add(_midGlow);
            _visualRoot.Children.Add(_coreGlow);
            Children.Add(_visualRoot);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public Color LedColor
        {
            get => (Color)GetValue(LedColorProperty);
            set => SetValue(LedColorProperty, value);
        }

        public bool IsLedEnabled
        {
            get => (bool)GetValue(IsLedEnabledProperty);
            set => SetValue(IsLedEnabledProperty, value);
        }

        public void SetLed(Color color, bool isEnabled = true)
        {
            LedColor = color;
            IsLedEnabled = isEnabled;
        }

        public void RestartAnimation()
        {
            if (!IsLoaded || !IsLedEnabled)
                return;

            StopBreathingAnimation();
            StartBreathingAnimation(forceRestart: true);
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BreathingPlayerLedOverlay overlay)
                overlay.RefreshVisualState();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshVisualState();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopBreathingAnimation();
        }

        private void RefreshVisualState()
        {
            if (!IsLoaded)
                return;

            bool shouldDisplay = IsLedEnabled;

            if (!shouldDisplay)
            {
                StopBreathingAnimation();
                _visualRoot.Opacity = 0.0;
                _outerGlow.Fill = Brushes.Transparent;
                _midGlow.Fill = Brushes.Transparent;
                _coreGlow.Fill = Brushes.Transparent;
                return;
            }

            ApplyBrushes(LedColor);
            StartBreathingAnimation();
        }

        private void ApplyBrushes(Color color)
        {
            _outerGlow.Fill = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B));
            _midGlow.Fill = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
            _coreGlow.Fill = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));
        }

        private void StartBreathingAnimation(bool forceRestart = false)
        {
            if (_isAnimationRunning && !forceRestart)
                return;

            _breathingStoryboard ??= CreateBreathingStoryboard();
            _visualRoot.Opacity = 0.80;
            _outerGlow.Opacity = 0.80;
            _midGlow.Opacity = 0.90;
            _coreGlow.Opacity = 0.90;
            _breathingStoryboard.Begin(this, true);
            _isAnimationRunning = true;
        }

        private void StopBreathingAnimation()
        {
            if (_breathingStoryboard != null)
                _breathingStoryboard.Stop(this);

            _isAnimationRunning = false;
        }

        private Storyboard CreateBreathingStoryboard()
        {
            var easing = new SineEase { EasingMode = EasingMode.EaseInOut };

            var storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };

            var rootOpacity = new DoubleAnimation
            {
                From = 0.80,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(1.2),
                EasingFunction = easing
            };
            Storyboard.SetTarget(rootOpacity, _visualRoot);
            Storyboard.SetTargetProperty(rootOpacity, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(rootOpacity);

            var outerOpacity = new DoubleAnimation
            {
                From = 0.80,
                To = 0.90,
                Duration = TimeSpan.FromSeconds(1.2),
                EasingFunction = easing
            };
            Storyboard.SetTarget(outerOpacity, _outerGlow);
            Storyboard.SetTargetProperty(outerOpacity, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(outerOpacity);

            var coreOpacity = new DoubleAnimation
            {
                From = 0.90,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(1.2),
                EasingFunction = easing
            };
            Storyboard.SetTarget(coreOpacity, _coreGlow);
            Storyboard.SetTargetProperty(coreOpacity, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(coreOpacity);

            return storyboard;
        }
    }
}
