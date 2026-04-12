using System;

namespace PS5_Controller_Tools.Buttons
{
    internal sealed class ButtonOverlayManager
    {
        private readonly RippleButtonOverlay _overlay;

        public ButtonOverlayManager(RippleButtonOverlay overlay)
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        public void Update(bool isPressed)
        {
            _overlay.UpdatePressedState(isPressed);
        }

        public void Reset()
        {
            _overlay.Reset();
        }
    }
}
