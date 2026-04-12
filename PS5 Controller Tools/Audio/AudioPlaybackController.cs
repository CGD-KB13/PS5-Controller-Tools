
namespace PS5_Controller_Tools.Audio
{
    internal sealed class AudioPlaybackController
    {
        private readonly DualSenseAudioService _audioService;

        public AudioPlaybackController(DualSenseAudioService audioService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public double OverlayVolume => _audioService.OverlayVolume;
        public bool IsPlaying => _audioService.IsPlaying;
        public bool IsPaused => _audioService.IsPaused;

        public void SetVolume(double volume, bool controllerConnected) => _audioService.SetOverlayVolume(volume, controllerConnected);
        public Task<bool> PlayWaveAsync(bool controllerConnected) => _audioService.PlayWaveAsync(controllerConnected);
        public Task<bool> PlayIndependentBeepSequenceAsync(bool controllerConnected) => _audioService.PlayIndependentBeepSequenceAsync(controllerConnected);
        public void Pause() => _audioService.Pause();
        public void Stop(bool controllerConnected, bool restoreRouting) => _audioService.Stop(controllerConnected, restoreRouting);
        public void HandleControllerDisconnected() => _audioService.HandleControllerDisconnected();
    }
}
