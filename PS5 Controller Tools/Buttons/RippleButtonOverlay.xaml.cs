using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PS5_Controller_Tools
{
    public partial class RippleButtonOverlay : UserControl
    {
        private readonly RippleAnimationController _animationController;

        public RippleButtonOverlay()
        {
            InitializeComponent();

            var storyboard = Resources["RippleStoryboard"] as Storyboard;
            _animationController = new RippleAnimationController(
                this,
                MainEllipse,
                new UIElement[] { Ripple1, Ripple2, Ripple3, Ripple4 },
                new[] { RippleScale1, RippleScale2, RippleScale3, RippleScale4 },
                storyboard,
                nameof(RippleButtonOverlay));
        }

        public static readonly DependencyProperty AccentFillProperty =
            DependencyProperty.Register(
                nameof(AccentFill),
                typeof(Brush),
                typeof(RippleButtonOverlay),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4497BCE9"))));

        public Brush AccentFill
        {
            get => (Brush)GetValue(AccentFillProperty);
            set => SetValue(AccentFillProperty, value);
        }

        public static readonly DependencyProperty RippleFill1Property =
            DependencyProperty.Register(
                nameof(RippleFill1),
                typeof(Brush),
                typeof(RippleButtonOverlay),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#997592B5"))));

        public Brush RippleFill1
        {
            get => (Brush)GetValue(RippleFill1Property);
            set => SetValue(RippleFill1Property, value);
        }

        public static readonly DependencyProperty RippleFill2Property =
            DependencyProperty.Register(
                nameof(RippleFill2),
                typeof(Brush),
                typeof(RippleButtonOverlay),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#887592B5"))));

        public Brush RippleFill2
        {
            get => (Brush)GetValue(RippleFill2Property);
            set => SetValue(RippleFill2Property, value);
        }

        public static readonly DependencyProperty RippleFill3Property =
            DependencyProperty.Register(
                nameof(RippleFill3),
                typeof(Brush),
                typeof(RippleButtonOverlay),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#777592B5"))));

        public Brush RippleFill3
        {
            get => (Brush)GetValue(RippleFill3Property);
            set => SetValue(RippleFill3Property, value);
        }

        public static readonly DependencyProperty RippleFill4Property =
            DependencyProperty.Register(
                nameof(RippleFill4),
                typeof(Brush),
                typeof(RippleButtonOverlay),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#667592B5"))));

        public Brush RippleFill4
        {
            get => (Brush)GetValue(RippleFill4Property);
            set => SetValue(RippleFill4Property, value);
        }

        public void UpdatePressedState(bool isPressed)
        {
            _animationController.UpdatePressedState(isPressed);
        }

        public void Start()
        {
            _animationController.Start();
        }

        public void Stop()
        {
            _animationController.Stop();
        }

        public void Reset()
        {
            _animationController.Reset();
        }
    }
}
