using System;
using System.Runtime.InteropServices;

namespace PS5_Controller_Tools.TouchPad
{
    internal static class DualSenseTouchPadReader
    {
        private static bool _touchApiUnavailable;

        public static bool TryReadPrimary(IntPtr controllerHandle, out TouchPadContactState state)
        {
            state = TouchPadContactState.Inactive;

            if (_touchApiUnavailable || controllerHandle == IntPtr.Zero)
                return false;

            try
            {
                int touchPadCount = NativeMethods.SDL_GameControllerGetNumTouchpads(controllerHandle);
                if (touchPadCount <= 0)
                    return false;

                int touchPadIndex = Math.Clamp(AppConstants.TouchPad.PrimaryTouchPadIndex, 0, touchPadCount - 1);
                int fingerCount = NativeMethods.SDL_GameControllerGetNumTouchpadFingers(controllerHandle, touchPadIndex);
                if (fingerCount <= 0)
                    return true;

                int preferredFingerIndex = Math.Clamp(AppConstants.TouchPad.PreferredFingerIndex, 0, fingerCount - 1);
                if (TryReadFinger(controllerHandle, touchPadIndex, preferredFingerIndex, out state))
                    return true;

                for (int fingerIndex = 0; fingerIndex < fingerCount; fingerIndex++)
                {
                    if (fingerIndex == preferredFingerIndex)
                        continue;

                    if (TryReadFinger(controllerHandle, touchPadIndex, fingerIndex, out state))
                        return true;
                }

                state = TouchPadContactState.Inactive;
                return true;
            }
            catch (DllNotFoundException)
            {
                _touchApiUnavailable = true;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                _touchApiUnavailable = true;
                return false;
            }
        }

        private static bool TryReadFinger(IntPtr controllerHandle, int touchPadIndex, int fingerIndex, out TouchPadContactState state)
        {
            state = TouchPadContactState.Inactive;

            int result = NativeMethods.SDL_GameControllerGetTouchpadFinger(
                controllerHandle,
                touchPadIndex,
                fingerIndex,
                out byte fingerState,
                out float x,
                out float y,
                out float pressure);

            if (result < 0 || fingerState == 0)
                return false;

            state = new TouchPadContactState(true, x, y, pressure, touchPadIndex, fingerIndex);
            return true;
        }

        private static class NativeMethods
        {
            [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SDL_GameControllerGetNumTouchpads(IntPtr gamecontroller);

            [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SDL_GameControllerGetNumTouchpadFingers(IntPtr gamecontroller, int touchpad);

            [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SDL_GameControllerGetTouchpadFinger(
                IntPtr gamecontroller,
                int touchpad,
                int finger,
                out byte state,
                out float x,
                out float y,
                out float pressure);
        }
    }
}
