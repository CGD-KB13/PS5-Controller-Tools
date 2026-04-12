using NAudio.CoreAudioApi;
using System.Text;

namespace PS5_Controller_Tools.Audio
{
    internal static class DualSenseCaptureDeviceSelector
    {
        public static MMDevice? SelectBestActiveDevice(
            MMDeviceEnumerator enumerator,
            out string diagnostics)
        {
            if (enumerator == null)
                throw new ArgumentNullException(nameof(enumerator));

            var scoredDevices = enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(device => new
                {
                    Device = device,
                    Score = ComputeScore(device.FriendlyName)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Device.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            diagnostics = BuildDiagnostics(scoredDevices);
            return scoredDevices.FirstOrDefault()?.Device;
        }

        private static int ComputeScore(string? friendlyName)
        {
            if (string.IsNullOrWhiteSpace(friendlyName))
                return 0;

            int score = 0;

            if (friendlyName.Contains("DualSense", StringComparison.OrdinalIgnoreCase))
                score += 100;

            if (friendlyName.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase))
                score += 80;

            if (friendlyName.Contains("Controller", StringComparison.OrdinalIgnoreCase))
                score += 20;

            if (friendlyName.Contains("manette", StringComparison.OrdinalIgnoreCase))
                score += 20;

            if (friendlyName.Contains("Microphone", StringComparison.OrdinalIgnoreCase))
                score += 10;

            if (friendlyName.Contains("Micro", StringComparison.OrdinalIgnoreCase))
                score += 5;

            return score;
        }

        private static string BuildDiagnostics(dynamic[] scoredDevices)
        {
            if (scoredDevices.Length == 0)
                return "Aucun périphérique de capture actif compatible DualSense n'a été trouvé.";

            var sb = new StringBuilder();
            sb.Append("Candidats micro DualSense: ");

            for (int i = 0; i < scoredDevices.Length; i++)
            {
                if (i > 0)
                    sb.Append(" | ");

                sb.Append(scoredDevices[i].Device.FriendlyName);
                sb.Append(" (score=");
                sb.Append(scoredDevices[i].Score);
                sb.Append(')');
            }

            return sb.ToString();
        }
    }
}
