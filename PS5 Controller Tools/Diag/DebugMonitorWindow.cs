using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PS5_Controller_Tools
{
    public sealed class DebugMonitorWindow : Window
    {
        private readonly TextBox _output;
        private readonly DebugControllerMonitor _monitor;

        public DebugMonitorWindow()
        {
            Title = "Debugge";
            Width = 620;
            Height = 520;
            MinWidth = 480;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;

            _output = new TextBox
            {
                Margin = new Thickness(12),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _monitor = new DebugControllerMonitor(_output);
            Content = _output;
            _monitor.Clear();
        }

        public void Update(ControllerStateSnapshot snapshot)
        {
            Dispatcher.Invoke(() => _monitor.Update(snapshot));
        }

        public void Clear()
        {
            Dispatcher.Invoke(_monitor.Clear);
        }
    }
}