using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;

namespace PS5_Controller_Tools.Lighting
{
    public sealed class LightBarBreathingOverlay : Grid
    {
        public static readonly DependencyProperty LightColorProperty = DependencyProperty.Register(
            nameof(LightColor),
            typeof(Color),
            typeof(LightBarBreathingOverlay),
            new PropertyMetadata(Colors.Black, OnVisualPropertyChanged));

        public static readonly DependencyProperty IsLightEnabledProperty = DependencyProperty.Register(
            nameof(IsLightEnabled),
            typeof(bool),
            typeof(LightBarBreathingOverlay),
            new PropertyMetadata(false, OnVisualPropertyChanged));

        public static readonly DependencyProperty SkewAngleProperty = DependencyProperty.Register(
            nameof(SkewAngle),
            typeof(double),
            typeof(LightBarBreathingOverlay),
            new PropertyMetadata(0.0, OnVisualPropertyChanged));

        private readonly Grid _visualRoot;
        private readonly Border _outerGlow;
        private readonly Border _midGlow;
        private readonly Border _coreGlow;
        private readonly Border _specularGlow;
        private readonly SkewTransform _skewTransform;
        private Storyboard? _breathingStoryboard;
        private bool _isAnimationRunning;

        public LightBarBreathingOverlay()
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            IsHitTestVisible = false;
            Background = Brushes.Transparent;
            SnapsToDevicePixels = false;

            _skewTransform = new SkewTransform();

            _visualRoot = new Grid
            {
                Opacity = 0.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _skewTransform
            };

            _outerGlow = new Border
            {
                Width = 4,
                Height = 95,
                CornerRadius = new CornerRadius(10),
                Opacity = 0.55,
                Background = Brushes.Transparent,
                Effect = new BlurEffect { Radius = 14 }
            };

            _midGlow = new Border
            {
                Width = 4,
                Height = 95,
                CornerRadius = new CornerRadius(8),
                Opacity = 0.82,
                Background = Brushes.Transparent
            };

            _coreGlow = new Border
            {
                Width = 4,
                Height = 95,
                CornerRadius = new CornerRadius(5),
                Opacity = 1.0,
                Background = Brushes.Transparent
            };

            _specularGlow = new Border
            {
                Width = 3,
                Height = 72,
                CornerRadius = new CornerRadius(2),
                Opacity = 0.92,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(-1, 0, 0, 0),
                Background = Brushes.Transparent
            };

            _visualRoot.Children.Add(_outerGlow);
            _visualRoot.Children.Add(_midGlow);
            _visualRoot.Children.Add(_coreGlow);
            _visualRoot.Children.Add(_specularGlow);
            Children.Add(_visualRoot);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public Color LightColor
        {
            get => (Color)GetValue(LightColorProperty);
            set => SetValue(LightColorProperty, value);
        }

        public bool IsLightEnabled
        {
            get => (bool)GetValue(IsLightEnabledProperty);
            set => SetValue(IsLightEnabledProperty, value);
        }

        public double SkewAngle
        {
            get => (double)GetValue(SkewAngleProperty);
            set => SetValue(SkewAngleProperty, value);
        }

        public void SetLight(Color color, bool isEnabled = true)
        {
            LightColor = color;
            IsLightEnabled = isEnabled;
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LightBarBreathingOverlay overlay)
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
            _skewTransform.AngleX = SkewAngle;

            if (!IsLoaded)
                return;

            bool shouldDisplay = IsLightEnabled && (LightColor.R > 0 || LightColor.G > 0 || LightColor.B > 0);

            if (!shouldDisplay)
            {
                StopBreathingAnimation();
                _visualRoot.Opacity = 0.0;
                _outerGlow.Background = Brushes.Transparent;
                _midGlow.Background = Brushes.Transparent;
                _coreGlow.Background = Brushes.Transparent;
                _specularGlow.Background = Brushes.Transparent;
                return;
            }

            ApplyBrushes(LightColor);
            StartBreathingAnimation();
        }

        private void ApplyBrushes(Color color)
        {
            _outerGlow.Background = CreateVerticalBrush(color, 18, 96);
            _midGlow.Background = CreateVerticalBrush(color, 92, 210);
            _coreGlow.Background = CreateVerticalBrush(color, 210, 255);
            _specularGlow.Background = CreateVerticalBrush(Colors.White, 26, 150);
        }

        private static Brush CreateVerticalBrush(Color color, byte edgeAlpha, byte centerAlpha)
        {
            return new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0.0),
                EndPoint = new Point(0.5, 1.0),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(edgeAlpha, color.R, color.G, color.B), 0.0),
                    new GradientStop(Color.FromArgb(centerAlpha, color.R, color.G, color.B), 0.18),
                    new GradientStop(Color.FromArgb(centerAlpha, color.R, color.G, color.B), 0.82),
                    new GradientStop(Color.FromArgb(edgeAlpha, color.R, color.G, color.B), 1.0)
                }
            };
        }

        private void StartBreathingAnimation()
        {
            if (_isAnimationRunning)
                return;

            _breathingStoryboard ??= CreateBreathingStoryboard();
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
                Duration = TimeSpan.FromSeconds(2.5),
                EasingFunction = easing
            };
            Storyboard.SetTarget(rootOpacity, _visualRoot);
            Storyboard.SetTargetProperty(rootOpacity, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(rootOpacity);

            var outerOpacity = new DoubleAnimation
            {
                From = 0.28,
                To = 0.72,
                Duration = TimeSpan.FromSeconds(2.5),
                EasingFunction = easing
            };
            Storyboard.SetTarget(outerOpacity, _outerGlow);
            Storyboard.SetTargetProperty(outerOpacity, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(outerOpacity);

            var coreOpacity = new DoubleAnimation
            {
                From = 0.80,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(2.5),
                EasingFunction = easing
            };
            Storyboard.SetTarget(coreOpacity, _coreGlow);
            Storyboard.SetTargetProperty(coreOpacity, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(coreOpacity);

            return storyboard;
        }
    }
}
