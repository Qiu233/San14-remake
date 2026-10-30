using Godot;
using San14.Data;

public partial class SceneAudioSession
{
    internal Task<SceneAudioChannel> PlayUniqueTacticVoiceAsync(string channel,
        UniqueTacticSelectedVoice selection, CancellationToken cancellationToken = default)
    {
        RequireEngineThread();
        if (_service is null || _repository is null || _streams is null ||
            SceneKey is null || _sceneCancellation is null)
            throw new InvalidOperationException("Unique tactic voice needs an active audio scene.");
        StopChannel(channel);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        var state = new ChannelState(new(SceneKey, SceneRevision, channel, null, "preparing"),
            lifetime, 0, "Master", AudioVolumeGroup.Voice);
        _channels.Add(channel, state);
        ChannelChanged?.Invoke(state.Snapshot);
        return LoadUniqueTacticVoiceAsync(state, selection);
    }

    private async Task<SceneAudioChannel> LoadUniqueTacticVoiceAsync(ChannelState state,
        UniqueTacticSelectedVoice selection)
    {
        AudioClip? prepared = null;
        try
        {
            var token = state.Cancellation.Token;
            var playback = await Task.Run(() =>
            {
                lock (_repository!)
                {
                    token.ThrowIfCancellationRequested();
                    return UniqueTacticVoicePublication.Resolve(_repository!, selection);
                }
            }, token).ConfigureAwait(false);
            prepared = await NamedAudioClips.PrepareAsync(_service!, _repository!, _streams!,
                playback.Sample, token).ConfigureAwait(false);
            var clip = prepared;
            var source = playback.StaticReference?.Sound ?? playback.LegacyReference!.Sound;
            var result = await _service!.InvokeOnEngineThreadAsync(() =>
            {
                if (!IsCurrent(state) || token.IsCancellationRequested) throw new OperationCanceledException(token);
                var selected = clip with { Cue = new(source.CueName, source.CueId, source.CueSectionOffset, source.LeafOffset) };
                _service.PlayUniqueTacticVoiceRoutes(selected, state.Snapshot.Channel, playback);
                state.VoiceSequence = _service.ReadVoiceSequence(state.Snapshot.Channel);
                state.Clip = selected;
                state.Snapshot = state.Snapshot with
                {
                    Status = "playing", SampleRequest = playback.Sample,
                    Selection = selected.Selection, Cue = selected.Cue, Provenance = selected.Provenance,
                    SampleRate = selected.SampleRate, Channels = selected.Channels,
                    SampleFrames = selected.SampleFrames, OutputChannels = selected.OutputChannels
                };
                ChannelChanged?.Invoke(state.Snapshot);
                return state.Snapshot;
            }, token).ConfigureAwait(false);
            prepared = null;
            return result;
        }
        catch (Exception error)
        {
            prepared?.Dispose();
            await _service!.InvokeOnEngineThreadAsync(() =>
            {
                if (IsCurrent(state))
                {
                    state.Snapshot = state.Snapshot with
                    { Status = error is OperationCanceledException ? "cancelled" : "unavailable", Error = error.Message };
                    ChannelChanged?.Invoke(state.Snapshot);
                }
                return true;
            }).ConfigureAwait(false);
            throw;
        }
        finally { state.Cancellation.Dispose(); }
    }
}

public partial class AudioPlaybackService
{
    private sealed record UniqueTacticVoiceRoutes(OriginalSpatialOutput Output, string[] Channels,
        OriginalAudioPlayer PrimaryPlayer, Action Finished);
    private readonly Dictionary<string, UniqueTacticVoiceRoutes> _uniqueTacticVoiceRoutes = [];

    internal void PlayUniqueTacticVoiceRoutes(AudioClip clip, string channel, UniqueTacticVoicePlayback playback)
    {
        RequireEngineThread();
        Stop(channel);
        //This existing renderer builds the actual CGRS output buses only.
        //Channel1 has no spatial player, position, distance curve or culling.
        var output = new OriginalSpatialOutput(OriginalOutputs, playback.Outputs);
        var channels = output.Sends.Select((_, index) => index == 0 ? channel : channel + ":send" + index).ToArray();
        AudioServer.Lock();
        try
        {
            var controlMatrix = output.Sends.Any(send => send.IsControl) ?
                SpatialSoundControlMatrixSource.PrepareNonPositionVoice(playback, clip.OutputChannels) : null;
            for (int index = 0; index < channels.Length; index++)
            {
                var send = output.Sends[index];
                PlayVoice(clip, channels[index], 0, send.Bus, AudioVolumeGroup.Voice, 0,
                    controlMatrix: send.IsControl ? controlMatrix : null);
                var voice = _players[channels[index]] with { SourceGain = playback.Gain * output.Sends[index].Gain, StaticCue = true };
                _players[channels[index]] = voice;
                voice.Player.PitchScale = playback.PlaybackRate;
                ApplyVolume(voice);
            }
            var primary = _players[channel].Player;
            UniqueTacticVoiceRoutes? routes = null;
            Action finished = () =>
            {
                if (_uniqueTacticVoiceRoutes.GetValueOrDefault(channel) == routes) Stop(channel);
            };
            routes = new(output, channels, primary, finished);
            _uniqueTacticVoiceRoutes.Add(channel, routes);
            primary.Finished += finished;
        }
        catch
        {
            foreach (string selected in channels) Stop(selected);
            output.Dispose();
            throw;
        }
        finally { AudioServer.Unlock(); }
    }

    private void StopUniqueTacticVoiceRoutes(string channel)
    {
        if (!_uniqueTacticVoiceRoutes.Remove(channel, out var routes)) return;
        AudioServer.Lock();
        try
        {
            routes.PrimaryPlayer.Finished -= routes.Finished;
            foreach (string send in routes.Channels.Skip(1)) Stop(send);
            //The caller stops/replaces the primary immediately after this hook.
            routes.PrimaryPlayer.Stop();
            routes.Output.Dispose();
        }
        finally { AudioServer.Unlock(); }
    }

    private void StopUniqueTacticVoices()
    {
        foreach (string channel in _uniqueTacticVoiceRoutes.Keys.ToArray()) Stop(channel);
    }
}
