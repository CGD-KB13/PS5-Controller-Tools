namespace PS5_Controller_Tools
{
    internal static class UiMessageCatalog
    {
        internal static class Controller
        {
            public const string NotDetected = "Manette : non détectée";
            public static string Connected(string deviceName)
                => $"Manette détectée : {deviceName}";
            public const string Disconnected = "Manette déconnectée";
            public const string SdlNotInitialized = "Manette : SDL non initialisé";
            public const string SdlError = "Erreur SDL";
        }

        internal static class Audio
        {
            public const string UsbRequired = "Branche la DualSense en USB avant le test.";
            public const string NoPlayback = "Aucune lecture en cours.";
            public const string GenericError = "Erreur audio";
            public const string ActivePlayback = "Lecture audio active.";
            public const string NoDualSenseDevice = "Aucun périphérique de sortie audio DualSense actif n'a été trouvé.";

            public static string WavePaused(string trackName)
                => $"Lecture {trackName} en pause";

            public static string WavePlaybackFailed(string trackName)
                => $"Échec de la lecture {trackName}";

            public static string BeepStarted()
                => "Test son lancé";

            public static string BeepStopped()
                => "Test son arrêté";

            public static string WavePlaying(string trackName, string deviceName)
                => $"Lecture {trackName} en cours sur : {deviceName}";

            public static string WaveStartFailed(string trackName, string details)
                => $"Lecture de {trackName} impossible : {details}";

            public static string BeepStartFailed(string details)
                => $"Lecture des bips impossible : {details}";
        }
    }
}
