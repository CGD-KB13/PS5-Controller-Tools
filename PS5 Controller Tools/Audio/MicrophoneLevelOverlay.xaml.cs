using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PS5_Controller_Tools.Audio
{
    public partial class MicrophoneLevelOverlay : UserControl
    {
        private readonly Rectangle[] _bars;
        private bool _isActive;

        public MicrophoneLevelOverlay()
        {
            InitializeComponent();
            _bars = new[] { Bar01, Bar02, Bar03, Bar04, Bar05, Bar06, Bar07, Bar08, Bar09, Bar10 };
            SetActive(false);
        }

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
            MicrophoneIcon.Opacity = isActive ? 1.0 : 0.35;
            MicrophoneIcon.Foreground = isActive ? new SolidColorBrush(Color.FromRgb(31, 59, 100)) : Brushes.Gray;
            SetMeterLevel(0.0);
        }

        public void SetMeterLevel(double level)
        {
            double normalizedLevel = Math.Clamp(level, 0.0, 1.0);
            int activeBars = _isActive ? (int)Math.Round(normalizedLevel * _bars.Length, MidpointRounding.AwayFromZero) : 0;

            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i].Opacity = i < activeBars ? 1.0 : (_isActive ? 0.20 : 0.08);
                _bars[i].Fill = _isActive
                    ? new SolidColorBrush(Color.FromRgb(45, 156, 219))
                    : Brushes.LightGray;
            }
        }
    }
}
