using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PS5_Controller_Tools
{
    public partial class SpeakerWaveVisualizerOverlay : UserControl
    {
        private sealed class BarState
        {
            public Rectangle Core { get; set; } = null!;
            public Rectangle Glow { get; set; } = null!;
            public double CurrentHeight { get; set; }
            public double Phase { get; set; }
            public double Weight { get; set; }
        }

        private sealed class VisualizerTheme
        {
            public Color CoreColor { get; init; }
            public Color GlowColor { get; init; }
            public double HeightMultiplier { get; init; }
            public double NoiseMultiplier { get; init; }
            public double GlowBoostMultiplier { get; init; }
            public double PhaseStepMultiplier { get; init; }
        }

        private static readonly VisualizerTheme IdleTheme = new()
        {
            CoreColor = Color.FromRgb(128, 146, 168),
            GlowColor = Color.FromArgb(70, 150, 168, 193),
            HeightMultiplier = 0.80,
            NoiseMultiplier = 0.10,
            GlowBoostMultiplier = 0.70,
            PhaseStepMultiplier = 0.85
        };

        private static readonly VisualizerTheme WaveTheme = new()
        {
            CoreColor = Color.FromArgb(95, 0, 112, 192), // Couleur principale des barres (ex: rouge)
            GlowColor = Color.FromArgb(90, 0, 32, 96), // Couleur du halo lumineux
            HeightMultiplier = AppConstants.Visualizer.WaveBarHeightMultiplier,
            NoiseMultiplier = AppConstants.Visualizer.WaveNoiseMultiplier,
            GlowBoostMultiplier = AppConstants.Visualizer.WaveGlowBoostMultiplier,
            PhaseStepMultiplier = 1.00
        };

        private static readonly VisualizerTheme BeepTheme = new()
        {
            CoreColor = Color.FromArgb(95, 0, 112, 192), // Couleur principale des barres (ex: rouge)
            GlowColor = Color.FromArgb(90, 0, 32, 96), // Couleur du halo lumineux
            HeightMultiplier = AppConstants.Visualizer.BeepBarHeightMultiplier,
            NoiseMultiplier = AppConstants.Visualizer.BeepNoiseMultiplier,
            GlowBoostMultiplier = AppConstants.Visualizer.BeepGlowBoostMultiplier,
            PhaseStepMultiplier = AppConstants.Visualizer.BeepPhaseStepMultiplier
        };

        private readonly List<BarState> _bars = new();
        private readonly Random _random = new();
        private bool _isBuilt;
        private double _smoothedLevel;
        private DualSenseAudioState _playbackState = DualSenseAudioState.Stopped;
        private AudioPlaybackMode _playbackMode = AudioPlaybackMode.None;

        public SpeakerWaveVisualizerOverlay()
        {
            InitializeComponent();
            Loaded += SpeakerWaveVisualizerOverlay_Loaded;
        }

        private void SpeakerWaveVisualizerOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isBuilt)
                return;

            BuildBars();
            Reset();
            _isBuilt = true;
        }

        internal void SetPlaybackState(DualSenseAudioState state, AudioPlaybackMode mode)
        {
            _playbackState = state;
            _playbackMode = mode;

            BarsCanvas.Opacity =
                (state == DualSenseAudioState.Playing || state == DualSenseAudioState.Paused)
                ? 1.0
                : 0.0;

            if (state == DualSenseAudioState.Stopped && mode == AudioPlaybackMode.None)
            {
                Reset();
            }
        }

        internal void UpdateFrame(AudioVisualizerFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            _playbackMode = frame.Mode;
            UpdateLevel(frame.Level);
        }

        private void BuildBars()
        {
            BarsCanvas.Children.Clear();
            _bars.Clear();

            double totalBarsWidth =
                (AppConstants.Visualizer.SpeakerBarCount * AppConstants.Visualizer.SpeakerBarWidth) +
                ((AppConstants.Visualizer.SpeakerBarCount - 1) * AppConstants.Visualizer.SpeakerBarGap);

            double startX = (AppConstants.Visualizer.SpeakerCanvasWidth - totalBarsWidth) / 2.0;
            double centerIndex = (AppConstants.Visualizer.SpeakerBarCount - 1) / 2.0;

            for (int i = 0; i < AppConstants.Visualizer.SpeakerBarCount; i++)
            {
                double distanceFromCenter = Math.Abs(i - centerIndex) / centerIndex;
                double centerBoost = 1.0 - distanceFromCenter;
                double weight = 0.45 + (centerBoost * 0.55);

                var glow = new Rectangle
                {
                    Width = AppConstants.Visualizer.SpeakerBarWidth + 2,
                    Height = AppConstants.Visualizer.SpeakerMinBarHeight,
                    RadiusX = 2,
                    RadiusY = 2,
                    Opacity = 0.85,
                    Effect = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = 4
                    }
                };

                var core = new Rectangle
                {
                    Width = AppConstants.Visualizer.SpeakerBarWidth,
                    Height = AppConstants.Visualizer.SpeakerMinBarHeight,
                    RadiusX = 2,
                    RadiusY = 2
                };

                double x = startX + i * (AppConstants.Visualizer.SpeakerBarWidth + AppConstants.Visualizer.SpeakerBarGap);

                Canvas.SetLeft(glow, x - 1);
                Canvas.SetTop(glow, (AppConstants.Visualizer.SpeakerCanvasHeight - AppConstants.Visualizer.SpeakerMinBarHeight) / 2.0);

                Canvas.SetLeft(core, x);
                Canvas.SetTop(core, (AppConstants.Visualizer.SpeakerCanvasHeight - AppConstants.Visualizer.SpeakerMinBarHeight) / 2.0);

                BarsCanvas.Children.Add(glow);
                BarsCanvas.Children.Add(core);

                _bars.Add(new BarState
                {
                    Core = core,
                    Glow = glow,
                    CurrentHeight = AppConstants.Visualizer.SpeakerMinBarHeight,
                    Phase = _random.NextDouble() * Math.PI * 2.0,
                    Weight = weight
                });
            }
        }

        public void UpdateLevel(float level)
        {
            if (!_isBuilt)
                return;

            VisualizerTheme theme = ResolveTheme();
            ApplyTheme(theme);

            double normalized = Math.Clamp(level, 0.0, 1.0);

            if (_playbackState == DualSenseAudioState.Paused)
            {
                normalized = 0.0;
            }

            _smoothedLevel =
                (_smoothedLevel * AppConstants.Visualizer.SpeakerSmoothCarry) +
                (normalized * AppConstants.Visualizer.SpeakerSmoothInput);

            double boosted = Math.Pow(_smoothedLevel, AppConstants.Visualizer.SpeakerBoostExponent);

            for (int i = 0; i < _bars.Count; i++)
            {
                BarState bar = _bars[i];

                double ripple =
                    AppConstants.Visualizer.SpeakerRippleBase +
                    (AppConstants.Visualizer.SpeakerRippleAmplitude *
                     Math.Sin(bar.Phase +
                              (_smoothedLevel * AppConstants.Visualizer.SpeakerPhaseLevelFactor) +
                              (i * AppConstants.Visualizer.SpeakerPhaseIndexFactor)));

                double targetHeight =
                    AppConstants.Visualizer.SpeakerMinBarHeight +
                    ((AppConstants.Visualizer.SpeakerMaxBarHeight - AppConstants.Visualizer.SpeakerMinBarHeight) *
                     boosted * bar.Weight * ripple * theme.HeightMultiplier);

                targetHeight += _random.NextDouble() * AppConstants.Visualizer.SpeakerNoiseAmplitude * theme.NoiseMultiplier;
                targetHeight = Math.Clamp(
                    targetHeight,
                    AppConstants.Visualizer.SpeakerMinBarHeight,
                    AppConstants.Visualizer.SpeakerMaxBarHeight);

                bar.CurrentHeight =
                    (bar.CurrentHeight * AppConstants.Visualizer.SpeakerBarHeightCarry) +
                    (targetHeight * AppConstants.Visualizer.SpeakerBarHeightInput);

                ApplyBarHeight(bar, bar.CurrentHeight);
                bar.Phase += AppConstants.Visualizer.SpeakerPhaseStep * theme.PhaseStepMultiplier;
            }

            GlowHost.Opacity =
                AppConstants.Visualizer.SpeakerGlowBaseOpacity +
                (boosted * AppConstants.Visualizer.SpeakerGlowBoostOpacity * theme.GlowBoostMultiplier);
        }

        public void Reset()
        {
            _smoothedLevel = 0.0;
            _playbackState = DualSenseAudioState.Stopped;
            _playbackMode = AudioPlaybackMode.None;
            ApplyTheme(IdleTheme);

            foreach (BarState bar in _bars)
            {
                bar.CurrentHeight = AppConstants.Visualizer.SpeakerMinBarHeight;
                ApplyBarHeight(bar, AppConstants.Visualizer.SpeakerMinBarHeight);
            }

            GlowHost.Opacity = AppConstants.Visualizer.SpeakerGlowIdleOpacity;
            BarsCanvas.Opacity = 0.0;
        }

        private VisualizerTheme ResolveTheme()
        {
            return _playbackMode switch
            {
                AudioPlaybackMode.Beep => BeepTheme,
                AudioPlaybackMode.Wave => WaveTheme,
                _ => IdleTheme
            };
        }

        private void ApplyTheme(VisualizerTheme theme)
        {
            foreach (BarState bar in _bars)
            {
                bar.Core.Fill = new SolidColorBrush(theme.CoreColor);
                bar.Glow.Fill = new SolidColorBrush(theme.GlowColor);
            }
        }

        private void ApplyBarHeight(BarState bar, double height)
        {
            double top = (AppConstants.Visualizer.SpeakerCanvasHeight - height) / 2.0;

            bar.Core.Height = height;
            bar.Glow.Height = height + 2;

            Canvas.SetTop(bar.Core, top);
            Canvas.SetTop(bar.Glow, top - 1);
        }
    }
}
