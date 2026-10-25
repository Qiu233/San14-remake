using San14.Core;

internal sealed partial class SceneAudioBinding
{
    //The event playback manager owns1B0 independently of the world's1AC.
    //Its actual UI input owner supplies this gate; unknown is never false.
    internal Func<bool?>? StorySoundSuppressed { private get; set; }

    internal Task PlayStorySoundAsync(StoryAudio command, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_playableSession is null || _session.SceneKey is null)
            throw new InvalidOperationException("story.audio.sound_scene_owner");
        if (command.AudioKind is StoryAudioKind.Sound or StoryAudioKind.SoundIfNotSkipped)
        {
            bool suppressed = StorySoundSuppressed?.Invoke() ??
                throw new InvalidDataException("story.audio.sound_manager_suppression");
            if (suppressed) return Task.CompletedTask;
        }
        return _session.PlayStorySoundAsync(command, cancellation);
    }
}
