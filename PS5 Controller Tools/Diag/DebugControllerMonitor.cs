using SDL2;
using System;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using PS5_Controller_Tools.TouchPad;

namespace PS5_Controller_Tools
{
    internal sealed class DebugControllerMonitor
    {
        private readonly TextBox _output;

        public DebugControllerMonitor(TextBox output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public void Update(ControllerStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (snapshot.ControllerHandle == IntPtr.Zero)
                return;

            var sb = new StringBuilder();

            sb.AppendLine("=== DEBUG CONTROLEUR ===");
            sb.AppendLine($"Horodatage     : {DateTime.Now:HH:mm:ss.fff}");
            sb.AppendLine($"Controller     : 0x{snapshot.ControllerHandle.ToInt64():X}");
            sb.AppendLine($"Joystick       : 0x{snapshot.JoystickHandle.ToInt64():X}");
            sb.AppendLine();

            sb.AppendLine("=== BOUTONS SDL ===");
            if (snapshot.PressedButtons.Count == 0)
            {
                sb.AppendLine("Aucun bouton SDL presse");
            }
            else
            {
                foreach (SDL.SDL_GameControllerButton button in snapshot.PressedButtons.OrderBy(b => b.ToString()))
                {
                    sb.AppendLine($"{button} = PRESSED");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== ENTREES SPECIFIQUES DUALSENSE ===");
            sb.AppendLine($"TouchPad clic  : {(snapshot.IsTouchPadPressed ? "PRESSED" : "released")}");
            sb.AppendLine($"Micro mute     : {(snapshot.IsMicMuted ? "ON" : "OFF")}");

            if (DualSenseTouchPadReader.TryReadPrimary(snapshot.ControllerHandle, out TouchPadContactState touchState))
            {
                sb.AppendLine($"Touch active   : {(touchState.IsActive ? "YES" : "NO")}");
                sb.AppendLine($"Touch X        : {(touchState.IsActive ? FormatPercent(touchState.X) : "--")}");
                sb.AppendLine($"Touch Y        : {(touchState.IsActive ? FormatPercent(touchState.Y) : "--")}");
                sb.AppendLine($"Touch pressure : {(touchState.IsActive ? FormatPercent(touchState.Pressure) : "--")}");
            }
            else
            {
                sb.AppendLine("Touch active   : indisponible");
                sb.AppendLine("Touch X        : --");
                sb.AppendLine("Touch Y        : --");
                sb.AppendLine("Touch pressure : --");
            }

            sb.AppendLine();

            sb.AppendLine("=== GACHETTES ===");
            sb.AppendLine($"L2             : {FormatPercent(snapshot.LeftTriggerPressure)}");
            sb.AppendLine($"R2             : {FormatPercent(snapshot.RightTriggerPressure)}");
            sb.AppendLine();

            sb.AppendLine("=== JOYSTICKS NORMALISES ===");
            sb.AppendLine($"Left X         : {FormatSignedPercent(snapshot.LeftStickX)}");
            sb.AppendLine($"Left Y         : {FormatSignedPercent(snapshot.LeftStickY)}");
            sb.AppendLine($"Right X        : {FormatSignedPercent(snapshot.RightStickX)}");
            sb.AppendLine($"Right Y        : {FormatSignedPercent(snapshot.RightStickY)}");
            sb.AppendLine();

            sb.AppendLine("=== SDL AXES BRUTS ===");
            foreach (SDL.SDL_GameControllerAxis axis in Enum.GetValues(typeof(SDL.SDL_GameControllerAxis)))
            {
                if (axis == SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_INVALID)
                    continue;

                short value = SDL.SDL_GameControllerGetAxis(snapshot.ControllerHandle, axis);
                int absValue = Math.Abs((int)value);

                if (absValue > AppConstants.Controller.DebugAxisNoiseThreshold)
                    sb.AppendLine($"{axis} = {value}");
            }

            if (snapshot.JoystickHandle != IntPtr.Zero)
            {
                sb.AppendLine();
                sb.AppendLine("=== RAW BUTTONS (JOYSTICK) ===");

                int numButtons = SDL.SDL_JoystickNumButtons(snapshot.JoystickHandle);
                bool anyRawButtonPressed = false;

                for (int i = 0; i < numButtons; i++)
                {
                    byte state = SDL.SDL_JoystickGetButton(snapshot.JoystickHandle, i);

                    if (state == 1)
                    {
                        anyRawButtonPressed = true;
                        sb.AppendLine($"RAW BUTTON {i} = PRESSED");
                    }
                }

                if (!anyRawButtonPressed)
                    sb.AppendLine("Aucun raw button presse");
            }

            _output.Text = sb.ToString();
            _output.ScrollToHome();
        }

        public void Clear()
        {
            _output.Text =
                "=== DEBUG CONTROLEUR ===" + Environment.NewLine +
                "En attente de donnees de la manette...";
        }

        private static string FormatPercent(double value)
        {
            return $"{Math.Round(Math.Clamp(value, 0.0, 1.0) * 100.0):0}%";
        }

        private static string FormatSignedPercent(double value)
        {
            int percent = (int)Math.Round(Math.Clamp(value, -1.0, 1.0) * 100.0);
            return percent >= 0 ? $"+{percent}%" : $"{percent}%";
        }
    }
}