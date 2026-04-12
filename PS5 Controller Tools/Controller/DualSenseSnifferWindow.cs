using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PS5_Controller_Tools
{
    internal sealed class DualSenseSnifferWindow : Window
    {
        private const int MaxLinesPerPane = 350;
        private const int FlushIntervalMs = 50;
        private const int MaxLinesPerFlush = 120;

        private readonly TextBox _hidOutputTextBox;
        private readonly TextBox _sdlOutputTextBox;

        private readonly Queue<string> _visibleHidLines = new();
        private readonly Queue<string> _visibleSdlLines = new();
        private readonly ConcurrentQueue<string> _pendingHidLines = new();
        private readonly ConcurrentQueue<string> _pendingSdlLines = new();
        private readonly DispatcherTimer _flushTimer;

        private bool _hidAutoScroll = true;
        private bool _sdlAutoScroll = true;
        private bool _isDisposed;

        public DualSenseSnifferWindow()
        {
            Title = "Sniffer manette";
            Width = 980;
            Height = 620;
            MinWidth = 760;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = "Flux temps reel :",
                Margin = new Thickness(12, 10, 12, 8),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var hidPanel = CreatePane("HID", out _hidOutputTextBox);
            Grid.SetRow(hidPanel, 1);
            root.Children.Add(hidPanel);

            var sdlPanel = CreatePane("SDL", out _sdlOutputTextBox);
            Grid.SetRow(sdlPanel, 2);
            root.Children.Add(sdlPanel);

            Content = root;

            _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(FlushIntervalMs)
            };
            _flushTimer.Tick += FlushTimer_Tick;
            _flushTimer.Start();

            Closed += (_, __) => DisposeResources();
        }

        public void AppendHid(string line)
        {
            if (_isDisposed)
                return;

            _pendingHidLines.Enqueue(line ?? string.Empty);
        }

        public void AppendSdl(string line)
        {
            if (_isDisposed)
                return;

            _pendingSdlLines.Enqueue(line ?? string.Empty);
        }

        public void ClearOutput()
        {
            if (_isDisposed)
                return;

            while (_pendingHidLines.TryDequeue(out _)) { }
            while (_pendingSdlLines.TryDequeue(out _)) { }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ClearVisibleOutput), DispatcherPriority.Background);
                return;
            }

            ClearVisibleOutput();
        }

        private Border CreatePane(string title, out TextBox textBox)
        {
            var panelGrid = new Grid();
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = title == "HID"
                    ? "Mode HID"
                    : title == "SDL"
                    ? "Mode SDL"
                    : "Mode Autre",
                Margin = new Thickness(10, 8, 10, 6),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black
            };
            Grid.SetRow(label, 0);
            panelGrid.Children.Add(label);

            textBox = new TextBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                IsReadOnly = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                Background = Brushes.White,
                Foreground = Brushes.Black
            };

            if (title == "HID")
            {
                textBox.TextChanged += (_, __) =>
                {
                    if (_hidAutoScroll)
                    {
                        _hidOutputTextBox.CaretIndex = _hidOutputTextBox.Text.Length;
                        _hidOutputTextBox.ScrollToEnd();
                    }
                };
                textBox.PreviewMouseWheel += (_, __) => _hidAutoScroll = false;
                textBox.PreviewMouseDown += (_, __) => _hidAutoScroll = false;
                textBox.GotFocus += (_, __) => _hidAutoScroll = false;
            }
            else
            {
                textBox.TextChanged += (_, __) =>
                {
                    if (_sdlAutoScroll)
                    {
                        _sdlOutputTextBox.CaretIndex = _sdlOutputTextBox.Text.Length;
                        _sdlOutputTextBox.ScrollToEnd();
                    }
                };
                textBox.PreviewMouseWheel += (_, __) => _sdlAutoScroll = false;
                textBox.PreviewMouseDown += (_, __) => _sdlAutoScroll = false;
                textBox.GotFocus += (_, __) => _sdlAutoScroll = false;
            }

            Grid.SetRow(textBox, 1);
            panelGrid.Children.Add(textBox);

            return new Border
            {
                Margin = new Thickness(12, 0, 12, 12),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(190, 190, 190)),
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                Child = panelGrid
            };
        }

        private void FlushTimer_Tick(object? sender, EventArgs e)
        {
            if (_isDisposed)
                return;

            bool hidChanged = FlushQueue(_pendingHidLines, _visibleHidLines, _hidOutputTextBox, ref _hidAutoScroll);
            bool sdlChanged = FlushQueue(_pendingSdlLines, _visibleSdlLines, _sdlOutputTextBox, ref _sdlAutoScroll);

            if (!hidChanged && !sdlChanged)
                return;
        }

        private static bool FlushQueue(
            ConcurrentQueue<string> pendingQueue,
            Queue<string> visibleLines,
            TextBox targetTextBox,
            ref bool autoScroll)
        {
            if (pendingQueue.IsEmpty)
                return false;

            int count = 0;
            bool changed = false;

            while (count < MaxLinesPerFlush && pendingQueue.TryDequeue(out string? line))
            {
                visibleLines.Enqueue(line ?? string.Empty);
                count++;
                changed = true;
            }

            if (!changed)
                return false;

            while (visibleLines.Count > MaxLinesPerPane)
                visibleLines.Dequeue();

            targetTextBox.Text = string.Join(Environment.NewLine, visibleLines);

            if (autoScroll)
            {
                targetTextBox.CaretIndex = targetTextBox.Text.Length;
                targetTextBox.ScrollToEnd();
            }

            return true;
        }

        private void ClearVisibleOutput()
        {
            _visibleHidLines.Clear();
            _visibleSdlLines.Clear();

            _hidOutputTextBox.Clear();
            _sdlOutputTextBox.Clear();

            _hidAutoScroll = true;
            _sdlAutoScroll = true;
        }

        private void DisposeResources()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _flushTimer.Stop();
            _flushTimer.Tick -= FlushTimer_Tick;
        }
    }
}
