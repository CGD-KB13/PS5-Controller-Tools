using System;
using System.Windows;
using System.Windows.Controls;

namespace PS5_Controller_Tools.TouchPad
{
    internal sealed class TouchPadOverlayManager
    {
        private readonly Canvas _surfaceCanvas;
        private readonly FrameworkElement _dot;

        public TouchPadOverlayManager(Canvas surfaceCanvas, FrameworkElement dot)
        {
            _surfaceCanvas = surfaceCanvas ?? throw new ArgumentNullException(nameof(surfaceCanvas));
            _dot = dot ?? throw new ArgumentNullException(nameof(dot));
            Reset();
        }

        public void Update(IntPtr controllerHandle)
        {
            if (!DualSenseTouchPadReader.TryReadPrimary(controllerHandle, out TouchPadContactState state) || !state.IsActive)
            {
                Reset();
                return;
            }

            double canvasWidth = GetUsableLength(_surfaceCanvas.ActualWidth, _surfaceCanvas.Width);
            double canvasHeight = GetUsableLength(_surfaceCanvas.ActualHeight, _surfaceCanvas.Height);
            double dotWidth = GetUsableLength(_dot.ActualWidth, _dot.Width);
            double dotHeight = GetUsableLength(_dot.ActualHeight, _dot.Height);

            if (canvasWidth <= 0 || canvasHeight <= 0 || dotWidth <= 0 || dotHeight <= 0)
            {
                _dot.Visibility = Visibility.Collapsed;
                return;
            }

            double normalizedX = Math.Clamp(state.X, 0.0, 1.0);
            double normalizedY = Math.Clamp(state.Y, 0.0, 1.0);

            double usableHeight = Math.Max(0.0, canvasHeight - dotHeight - (AppConstants.TouchPad.DotPadding * 2.0));
            double top = AppConstants.TouchPad.DotPadding + (normalizedY * usableHeight);

            double topInsetRatio = AppConstants.TouchPad.TopInsetRatio;
            double bottomInsetRatio = AppConstants.TouchPad.BottomInsetRatio;
            // Interpolation entre le haut et le bas du trapèze
            // normalizedY = 0 → haut → utilise TopInsetRatio
            // normalizedY = 1 → bas → utilise BottomInsetRatio
            // donc BottomInsetRatio contrôle directement l’ouverture du bas
            double insetRatio = topInsetRatio + ((bottomInsetRatio - topInsetRatio) * normalizedY);
            double horizontalInset = canvasWidth * insetRatio;

            double minLeft = horizontalInset + AppConstants.TouchPad.DotPadding;
            double maxLeft = canvasWidth - horizontalInset - dotWidth - AppConstants.TouchPad.DotPadding;

            if (maxLeft < minLeft)
            {
                _dot.Visibility = Visibility.Collapsed;
                return;
            }

            double left = minLeft + (normalizedX * (maxLeft - minLeft));

            Canvas.SetLeft(_dot, left);
            Canvas.SetTop(_dot, top);
            _dot.Visibility = Visibility.Visible;
        }

        public void Reset()
        {
            CenterDot();
            _dot.Visibility = Visibility.Collapsed;
        }

        private void CenterDot()
        {
            double canvasWidth = GetUsableLength(_surfaceCanvas.ActualWidth, _surfaceCanvas.Width);
            double canvasHeight = GetUsableLength(_surfaceCanvas.ActualHeight, _surfaceCanvas.Height);
            double dotWidth = GetUsableLength(_dot.ActualWidth, _dot.Width);
            double dotHeight = GetUsableLength(_dot.ActualHeight, _dot.Height);

            if (canvasWidth <= 0 || canvasHeight <= 0 || dotWidth <= 0 || dotHeight <= 0)
                return;

            Canvas.SetLeft(_dot, (canvasWidth - dotWidth) / 2.0);
            Canvas.SetTop(_dot, (canvasHeight - dotHeight) / 2.0);
        }

        private static double GetUsableLength(double actualLength, double declaredLength)
        {
            if (actualLength > 0)
                return actualLength;
            if (!double.IsNaN(declaredLength) && declaredLength > 0)
                return declaredLength;
            return 0;
        }
    }
}
