using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private sealed record SpeakerLayoutChange(GameAudioSpeakerLayout Target, CancellationToken Cancellation,
        TaskCompletionSource Completion);
    private SpeakerLayoutChange? _speakerLayoutChange;
    internal GameAudioSpeakerLayout CurrentSpeakerLayout
    {
        get { RequireEngineThread(); return OriginalOutputs.SpeakerLayout; }
    }
    internal SpeakerLayoutAvailability QuerySpeakerLayoutAvailability(GameAudioSpeakerLayout layout)
    {
        RequireEngineThread();
        if (!Enum.IsDefined(layout)) throw new ArgumentOutOfRangeException(nameof(layout));
        if (_originalOutputs is null) return new(false, "audio.output.not_configured");
        try { _originalOutputs.ValidateHost(); }
        catch (InvalidOperationException error) { return new(false, error.Message); }
        return OriginalOutputs.QuerySpeakerLayoutAvailability(layout);
    }
    internal Task ApplySpeakerLayoutAsync(GameAudioSpeakerLayout layout, CancellationToken cancellationToken = default)
    {
        RequireEngineThread(); cancellationToken.ThrowIfCancellationRequested();
        var availability = QuerySpeakerLayoutAvailability(layout);
        if (!availability.Available) throw new NotSupportedException(availability.UnresolvedOwner);
        if (_speakerLayoutChange is not null) throw new InvalidOperationException("audio.output.change_already_pending");
        if (CurrentSpeakerLayout == layout) return Task.CompletedTask;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _speakerLayoutChange = new(layout, cancellationToken, completion);
        AudioServer.Lock();
        try
        {
            foreach (var pair in _mapMusic.ToArray())
            {
                // A genuinely paused track has no audible tail and cannot
                // advance a mixer-clock fade. Do not resume it for this change.
                if (pair.Value.Player.StreamPaused) Stop(pair.Key);
                else ReleaseMapMusicLocked(pair.Key, 1000);
            }
        }
        catch (Exception error) { _speakerLayoutChange = null; completion.SetException(error); }
        finally { AudioServer.Unlock(); }
        UpdateSpeakerLayoutChange();
        return completion.Task;
    }
    private void UpdateSpeakerLayoutChange()
    {
        if (_speakerLayoutChange is not { } change) return;
        if (_lifetime.IsCancellationRequested || change.Cancellation.IsCancellationRequested)
        { _speakerLayoutChange = null; change.Completion.TrySetCanceled(change.Cancellation); return; }
        if (_mapMusic.Count != 0) return; // The real mixer clock owns the1s fade.
        _speakerLayoutChange = null;
        try
        {
            var availability = QuerySpeakerLayoutAvailability(change.Target);
            if (!availability.Available) throw new NotSupportedException(availability.UnresolvedOwner);
            ForceStopEffects(); StopAllSpatial();
            foreach (string channel in _players.Keys.ToArray()) Stop(channel, "Audio output speaker layout changed.");
            OriginalOutputs.SetSpeakerLayout(change.Target);
            _originalDeviceProfile = _originalDeviceProfile! with { Layout = change.Target };
            change.Completion.SetResult();
        }
        catch (Exception error) { change.Completion.SetException(error); }
    }
}
