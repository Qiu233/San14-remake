using San14.Core;

internal sealed partial class SceneAudioBinding
{
    //The event playback manager's suppression1B0 is distinct from its skip1AC.
    //Its actual UI input owner supplies this gate; unknown is never false.
    internal Func<bool?>? StorySoundSuppressed { private get; set; }
    //The application creates this event playback audio owner once. Its
    //source initializer clears1B0; an active event's input writer can supply
    //the current value through the existing callback.
    internal bool EventPlaybackAudioSuppressed => StorySoundSuppressed?.Invoke() ?? false;

    internal Task PlayNormalSoundAsync(int soundId, CancellationToken cancellation)
    {
        if (!HasStoryAudioOwner)
            throw new InvalidOperationException("normal.audio.sound_scene_owner");
        return _session.PlayStorySoundAsync(new(StoryAudioKind.Sound, soundId, 0, 0, 0), cancellation);
    }

    internal Task PlayStorySoundAsync(StoryAudio command, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!HasStoryAudioOwner)
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
