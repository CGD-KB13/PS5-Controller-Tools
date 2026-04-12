using SDL2;

namespace PS5_Controller_Tools
{
    internal static class OverlayButtonBindingsFactory
    {
        public static List<OverlayButtonBinding> Create(MainWindow window)
        {
            return new List<OverlayButtonBinding>
            {
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER, window.BtnL1Overlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER, window.BtnR1Overlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP, window.BtnDpadUpOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN, window.BtnDpadDownOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT, window.BtnDpadLeftOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT, window.BtnDpadRightOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A, window.BtnCrossOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B, window.BtnCircleOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X, window.BtnSquareOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y, window.BtnTriangleOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK, window.BtnL3Overlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK, window.BtnR3Overlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK, window.BtnCreateOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START, window.BtnOptionsOverlay),
                new(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_GUIDE, window.BtnPsOverlay)
            };
        }
    }
}
