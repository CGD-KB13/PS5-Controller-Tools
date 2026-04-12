using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PS5_Controller_Tools
{
    internal sealed class RippleAnimationController
    {
        private const double InitialScale = 0.1;

        private readonly FrameworkElement _container;
        private readonly UIElement _mainButton;
        private readonly IReadOnlyList<UIElement> _ripples;
        private readonly IReadOnlyList<ScaleTransform> _scaleTransforms;
        private readonly Storyboard? _storyboard;
        private readonly string _logSource;

        public bool WasPressed { get; private set; }

        public RippleAnimationController(
            FrameworkElement container,
            UIElement mainButton,
            IReadOnlyList<UIElement> ripples,
            IReadOnlyList<ScaleTransform> scaleTransforms,
            Storyboard? storyboard,
            string logSource)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _mainButton = mainButton ?? throw new ArgumentNullException(nameof(mainButton));
            _ripples = ripples ?? throw new ArgumentNullException(nameof(ripples));
            _scaleTransforms = scaleTransforms ?? throw new ArgumentNullException(nameof(scaleTransforms));
            _storyboard = storyboard;
            _logSource = string.IsNullOrWhiteSpace(logSource)
                ? nameof(RippleAnimationController)
                : logSource;

            Reset();
        }

        public void UpdatePressedState(bool isPressed)
        {
            if (isPressed && !WasPressed)
            {
                Start();
            }
            else if (!isPressed && WasPressed)
            {
                Stop();
            }

            WasPressed = isPressed;
        }

        public void Start()
        {
            if (_storyboard == null)
            {
                AppLogger.Warn(_logSource, "Storyboard ripple introuvable. Animation ignorée.");
                return;
            }

            _mainButton.Opacity = 1;
            _storyboard.Remove(_container);
            _storyboard.Begin(_container, true);
        }

        public void Stop()
        {
            _mainButton.Opacity = 0;
            StopStoryboard();
            ResetCoreVisuals();
            WasPressed = false;
        }

        public void Reset()
        {
            StopStoryboard();
            ResetCoreVisuals();
            WasPressed = false;
        }

        private void StopStoryboard()
        {
            if (_storyboard == null)
                return;

            _storyboard.Stop(_container);
            _storyboard.Remove(_container);
        }

        private void ResetCoreVisuals()
        {
            _mainButton.Opacity = 0;

            foreach (UIElement ripple in _ripples)
            {
                ripple.Opacity = 0;
            }

            foreach (ScaleTransform scale in _scaleTransforms)
            {
                scale.ScaleX = InitialScale;
                scale.ScaleY = InitialScale;
            }
        }
    }
}
