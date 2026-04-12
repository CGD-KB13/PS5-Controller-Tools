using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PS5_Controller_Tools.Triggers
{
    public partial class TriggerPressureOverlay : UserControl
    {
        public TriggerPressureOverlay()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateVisual();
            SizeChanged += (_, _) => UpdateVisual();
        }

        public static readonly DependencyProperty SweepClockwiseProperty =
            DependencyProperty.Register(
                nameof(SweepClockwise),
                typeof(bool),
                typeof(TriggerPressureOverlay),
                new PropertyMetadata(true, OnSweepClockwiseChanged));

        public bool SweepClockwise
        {
            get => (bool)GetValue(SweepClockwiseProperty);
            set => SetValue(SweepClockwiseProperty, value);
        }

        private static void OnSweepClockwiseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TriggerPressureOverlay overlay)
                overlay.UpdateVisual();
        }

        public static readonly DependencyProperty PressureProperty =
            DependencyProperty.Register(
                nameof(Pressure),
                typeof(double),
                typeof(TriggerPressureOverlay),
                new PropertyMetadata(0.0, OnPressureChanged));

        public double Pressure
        {
            get => (double)GetValue(PressureProperty);
            set => SetValue(PressureProperty, value);
        }

        private static void OnPressureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TriggerPressureOverlay overlay)
                overlay.UpdateVisual();
        }

        public void SetPressure(double value)
        {
            Pressure = Math.Clamp(value, 0.0, 1.0);
        }

        public void Reset()
        {
            Pressure = 0.0;
        }

        private void UpdateVisual()
        {
            double p = Math.Clamp(Pressure, 0.0, 1.0);

            if (p <= 0.0)
            {
                FillPath.Data = null;
                PercentText.Text = "0%";
                PercentText.Visibility = Visibility.Collapsed;
                Visibility = Visibility.Collapsed;
                return;
            }

            Visibility = Visibility.Visible;
            PercentText.Visibility = Visibility.Visible;
            PercentText.Text = $"{Math.Round(p * 100):0}%";

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0)
            {
                FillPath.Data = null;
                return;
            }

            double cx = w / 2.0;
            double cy = h / 2.0;
            double radius = Math.Min(w, h) / 2.0 - 2.0;

            ClipEllipse.Center = new Point(cx, cy);
            ClipEllipse.RadiusX = radius;
            ClipEllipse.RadiusY = radius;

            if (p >= 1.0)
            {
                FillPath.Data = new EllipseGeometry(new Point(cx, cy), radius, radius);
                return;
            }

            double startAngle = -90.0;
            double endAngle = SweepClockwise
                ? startAngle + (360.0 * p)
                : startAngle - (360.0 * p);

            Point center = new Point(cx, cy);
            Point startPoint = PointOnCircle(cx, cy, radius, startAngle);
            Point endPoint = PointOnCircle(cx, cy, radius, endAngle);

            bool isLargeArc = p > 0.5;
            SweepDirection sweepDirection = SweepClockwise
                ? SweepDirection.Clockwise
                : SweepDirection.Counterclockwise;

            var figure = new PathFigure
            {
                StartPoint = center,
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new LineSegment(startPoint, true));
            figure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                sweepDirection,
                true));
            figure.Segments.Add(new LineSegment(center, true));

            FillPath.Data = new PathGeometry(new[] { figure });
        }

        private static Point PointOnCircle(double cx, double cy, double radius, double angleDegrees)
        {
            double angleRadians = angleDegrees * Math.PI / 180.0;
            double x = cx + radius * Math.Cos(angleRadians);
            double y = cy + radius * Math.Sin(angleRadians);
            return new Point(x, y);
        }
    }
}
