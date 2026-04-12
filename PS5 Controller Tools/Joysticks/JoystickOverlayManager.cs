using System;
using System.Windows;
using System.Windows.Controls;

namespace PS5_Controller_Tools.Joysticks
{
    internal sealed class JoystickOverlayManager
    {
        private readonly Canvas _leftCanvas;
        private readonly FrameworkElement _leftDot;
        private readonly TextBlock _leftXText;
        private readonly TextBlock _leftYText;
        private readonly Canvas _rightCanvas;
        private readonly FrameworkElement _rightDot;
        private readonly TextBlock _rightXText;
        private readonly TextBlock _rightYText;

        public JoystickOverlayManager(
            Canvas leftCanvas,
            FrameworkElement leftDot,
            TextBlock leftXText,
            TextBlock leftYText,
            Canvas rightCanvas,
            FrameworkElement rightDot,
            TextBlock rightXText,
            TextBlock rightYText)
        {
            _leftCanvas = leftCanvas ?? throw new ArgumentNullException(nameof(leftCanvas));
            _leftDot = leftDot ?? throw new ArgumentNullException(nameof(leftDot));
            _leftXText = leftXText ?? throw new ArgumentNullException(nameof(leftXText));
            _leftYText = leftYText ?? throw new ArgumentNullException(nameof(leftYText));
            _rightCanvas = rightCanvas ?? throw new ArgumentNullException(nameof(rightCanvas));
            _rightDot = rightDot ?? throw new ArgumentNullException(nameof(rightDot));
            _rightXText = rightXText ?? throw new ArgumentNullException(nameof(rightXText));
            _rightYText = rightYText ?? throw new ArgumentNullException(nameof(rightYText));
            Reset();
        }

        public void Update(JoystickState leftState, JoystickState rightState)
        {
            UpdateJoystickDot(_leftCanvas, _leftDot, leftState.X, leftState.Y);
            UpdateJoystickDot(_rightCanvas, _rightDot, rightState.X, rightState.Y);
            UpdateAxisTexts(_leftXText, _leftYText, leftState.X, leftState.Y);
            UpdateAxisTexts(_rightXText, _rightYText, rightState.X, rightState.Y);
        }

        public void Reset()
        {
            ResetJoystickDot(_leftCanvas, _leftDot);
            ResetJoystickDot(_rightCanvas, _rightDot);
            ResetAxisTexts(_leftXText, _leftYText);
            ResetAxisTexts(_rightXText, _rightYText);
        }

        private void UpdateJoystickDot(Canvas canvas, FrameworkElement dot, double x, double y)
        {
            if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0)
                return;

            double magnitude = Math.Sqrt((x * x) + (y * y));
            if (magnitude <= AppConstants.Controller.StickVisibleMagnitudeThreshold)
            {
                ResetJoystickDot(canvas, dot);
                return;
            }

            if (magnitude > 1.0)
            {
                x /= magnitude;
                y /= magnitude;
            }

            double canvasWidth = canvas.ActualWidth;
            double canvasHeight = canvas.ActualHeight;
            double dotWidth = dot.ActualWidth > 0 ? dot.ActualWidth : dot.Width;
            double dotHeight = dot.ActualHeight > 0 ? dot.ActualHeight : dot.Height;
            double centerLeft = (canvasWidth - dotWidth) / 2.0;
            double centerTop = (canvasHeight - dotHeight) / 2.0;
            double maxOffsetX = centerLeft - AppConstants.Controller.StickDotPadding;
            double maxOffsetY = centerTop - AppConstants.Controller.StickDotPadding;

            Canvas.SetLeft(dot, centerLeft + (x * maxOffsetX));
            Canvas.SetTop(dot, centerTop + (y * maxOffsetY));
            dot.Visibility = Visibility.Visible;
        }

        private static void UpdateAxisTexts(TextBlock xText, TextBlock yText, double x, double y)
        {
            double magnitude = Math.Sqrt((x * x) + (y * y));
            if (magnitude <= AppConstants.Controller.StickVisibleMagnitudeThreshold)
            {
                xText.Visibility = Visibility.Collapsed;
                yText.Visibility = Visibility.Collapsed;
                return;
            }

            xText.Text = $"X={FormatPercent(x)}";
            yText.Text = $"Y={FormatPercent(y)}";
            xText.Visibility = Visibility.Visible;
            yText.Visibility = Visibility.Visible;
        }

        private static void ResetAxisTexts(TextBlock xText, TextBlock yText)
        {
            xText.Visibility = Visibility.Collapsed;
            yText.Visibility = Visibility.Collapsed;
        }

        private static string FormatPercent(double value)
        {
            int percent = (int)Math.Round(value * 100.0);
            return percent >= 0 ? $"+{percent}%" : $"{percent}%";
        }

        private static void ResetJoystickDot(Canvas canvas, FrameworkElement dot)
        {
            double canvasWidth = canvas.ActualWidth;
            double canvasHeight = canvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                // fallback : centre approximatif basé sur taille du dot
                Canvas.SetLeft(dot, 0);
                Canvas.SetTop(dot, 0);
                dot.Visibility = Visibility.Collapsed;
                return;
            }

            CenterDot(canvas, dot);
            dot.Visibility = Visibility.Collapsed;
        }

        private static void CenterDot(Canvas canvas, FrameworkElement dot)
        {
            double canvasWidth = canvas.ActualWidth;
            double canvasHeight = canvas.ActualHeight;
            double dotWidth = dot.ActualWidth > 0 ? dot.ActualWidth : dot.Width;
            double dotHeight = dot.ActualHeight > 0 ? dot.ActualHeight : dot.Height;
            Canvas.SetLeft(dot, (canvasWidth - dotWidth) / 2.0);
            Canvas.SetTop(dot, (canvasHeight - dotHeight) / 2.0);
        }
    }
}
