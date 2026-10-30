using Godot;
using San14.Core;
using San14.Data;

internal sealed record ArmySpeechAudioContext(Node3D World, NarrationSpatialVoicePosition Listener);

public partial class SceneAudioSession
{
    private sealed class ArmySpeechVoice(string channel, ArmyMapSpeechSpatialVoiceRequest request,
        NarrationSpatialVoicePlayback playback, OriginalSpatialOutput output, Node3D world,
        long revision, CancellationTokenSource lifetime)
    {
        internal readonly string Channel = channel;
        internal readonly ArmyMapSpeechSpatialVoiceRequest Request = request;
        internal readonly NarrationSpatialVoicePlayback Playback = playback;
        internal readonly OriginalSpatialOutput Output = output;
        internal readonly Node3D World = world;
        internal readonly long Revision = revision;
        internal readonly CancellationTokenSource Lifetime = lifetime;
        internal readonly CancellationToken Token = lifetime.Token;
    }

    private readonly Dictionary<string, ArmySpeechVoice> _armySpeechVoices = [];
    private readonly Dictionary<NarrationVoiceLanguage, Task<(AudioSelectionBank Bank, LoadedRdbAsset Categories)>> _armySpeechBanks = [];
    private OriginalSpatialOutput? _armySpeechOutput;
    private long _armySpeechVoiceSequence;

    //Loading is asynchronous and precedes the formatting transaction. A pending
    //bank leaves the source entry untouched; it does not draw App or reject a voice.
    internal bool PrepareArmySpeechBank(NarrationVoiceLanguage language)
    {
        RequireEngineThread();
        if (_repository is null || _sceneCancellation is null || _streams is null || _service is null)
            throw new InvalidOperationException("Army speech needs an active audio scene.");
        if (!_armySpeechBanks.TryGetValue(language, out var pending))
        {
            var repository = _repository;
            var token = _sceneCancellation.Token;
            pending = Task.Run(() =>
            {
                lock (repository)
                {
                    token.ThrowIfCancellationRequested();
                    return (AudioSelectionResources.Load(repository, NarrationVoiceSelection.Bank(language, 3).SelectionBankId),
                        repository.LoadRdbAsset("RRPreview", NarrationVoiceSelection.OutputResourceId));
                }
            }, token);
            _armySpeechBanks.Add(language, pending);
        }
        if (!pending.IsCompleted) return false;
        _ = pending.GetAwaiter().GetResult();
        return true;
    }

    internal ArmySpeechAudioBatch BeginArmySpeechAudio(ArmySpeechAudioContext? context, bool enabled)
    {
        RequireEngineThread();
        if (_sceneCancellation is null || _service is null || _repository is null || _streams is null)
            throw new InvalidOperationException("Army speech needs an active audio scene.");
        return new(this, context, enabled, SceneRevision);
    }

    internal sealed class ArmySpeechAudioBatch(SceneAudioSession owner, ArmySpeechAudioContext? context,
        bool enabled, long revision) : IDisposable
    {
        private readonly List<ArmySpeechVoice> voices = [];
        private bool committed, disposed;

        internal bool TryReserve(ArmyMapSpeechSpatialVoiceRequest request)
        {
            owner.RequireEngineThread();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (committed) throw new InvalidOperationException("Army speech audio batch is already committed.");
            if (!enabled || owner.SceneRevision != revision || context is null ||
                !GodotObject.IsInstanceValid(context.World) || !context.World.IsInsideTree() ||
                owner._armySpeechVoices.Count >= 3) return false;
            if (!ArmyMapSpeechVoiceGeometry.WithinSourceDistance(new(request.SourceX, request.SourceY, request.SourceZ),
                context.Listener)) return false;
            var language = request.Voice.Language switch
            {
                PersonnelVoiceLanguage.Japanese => NarrationVoiceLanguage.Japanese,
                PersonnelVoiceLanguage.Chinese => NarrationVoiceLanguage.Chinese,
                _ => throw new InvalidDataException("Unknown Army speech voice language.")
            };
            if (!owner._armySpeechBanks.TryGetValue(language, out var pending) || !pending.IsCompletedSuccessfully)
                throw new InvalidDataException("Army speech voice bank is not prepared.");
            var voice = request.Voice;
            var playback = NarrationVoiceSelection.ReadSpatialPlayback(new(voice.SpeakerId, voice.VoiceType, voice.VoiceId,
                language, voice.VariantIndex, voice.SoundId), pending.Result.Bank, pending.Result.Categories);
            var output = owner._armySpeechOutput;
            if (output is null) owner._armySpeechOutput = output = new(owner._service!.OriginalOutputs, playback.Outputs);
            else if (output.Graph.Source.DecodedSha256 != playback.Outputs.Source.DecodedSha256 ||
                output.Graph.Classification.Id != playback.Outputs.Classification.Id)
                throw new InvalidDataException("Army speech output resource changed; re-enter the map to bind its new route.");
            var slot = new ArmySpeechVoice("army-speech:" + unchecked(++owner._armySpeechVoiceSequence),
                request, playback, output, context.World, revision,
                CancellationTokenSource.CreateLinkedTokenSource(owner._sceneCancellation!.Token));
            owner._armySpeechVoices.Add(slot.Channel, slot);
            voices.Add(slot);
            return true;
        }

        //The actual reservation survives the formatting borrow only when its
        //world result was adopted. Starting decode before this point could play
        //a sound from a failed message transaction.
        internal void Commit()
        {
            owner.RequireEngineThread();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (committed) throw new InvalidOperationException("Army speech audio batch is already committed.");
            if (owner.SceneRevision != revision || voices.Any(voice => !owner.IsArmySpeechVoiceCurrent(voice)))
                throw new InvalidOperationException("Army speech audio scene has retired.");
            committed = true;
            foreach (var voice in voices) _ = owner.LoadArmySpeechVoiceAsync(voice);
        }

        internal void Abort()
        {
            owner.RequireEngineThread();
            foreach (var voice in voices) owner.ReleaseArmySpeechVoice(voice);
        }

        public void Dispose()
        {
            if (disposed) return;
            owner.RequireEngineThread();
            disposed = true;
            if (!committed) foreach (var voice in voices) owner.ReleaseArmySpeechVoice(voice);
        }
    }

    private bool IsArmySpeechVoiceCurrent(ArmySpeechVoice voice) => SceneRevision == voice.Revision &&
        _armySpeechVoices.TryGetValue(voice.Channel, out var current) && ReferenceEquals(current, voice) &&
        !voice.Token.IsCancellationRequested;

    private async Task LoadArmySpeechVoiceAsync(ArmySpeechVoice voice)
    {
        AudioClip? prepared = null;
        try
        {
            prepared = await NamedAudioClips.PrepareAsync(_service!, _repository!, _streams!, voice.Playback.NamedSample,
                voice.Token).ConfigureAwait(false);
            var clip = prepared;
            await _service!.InvokeOnEngineThreadAsync(() =>
            {
                if (!IsArmySpeechVoiceCurrent(voice) || !GodotObject.IsInstanceValid(voice.World) || !voice.World.IsInsideTree())
                    throw new OperationCanceledException(voice.Token);
                _service.PlaySpatial(clip, voice.Channel, voice.World,
                    new(voice.Request.SourceX, voice.Request.SourceY, voice.Request.SourceZ), voice.Playback.PlaybackRate,
                    () => voice.Playback.Gain,
                    () => ReleaseArmySpeechVoice(voice, stopPlayer: false), AudioVolumeGroup.Voice,
                    AudioStreamPlayer3D.AttenuationModelEnum.Disabled, voice.Output.Bus, voice.Output.Buses.Skip(1).ToArray(), voice.Output.Sends,
                    voice.Output.Sends.Any(send => send.IsControl) ? SpatialSoundControlMatrixSource.PrepareSpatialNarration(
                        clip.OutputChannels, voice.Playback.SourceChannelMask) : null);
                prepared = null; //The actual spatial player owns the decoded clip.
                return true;
            }, voice.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            try
            {
                await _service!.InvokeOnEngineThreadAsync(() =>
                {
                    if (IsArmySpeechVoiceCurrent(voice)) GD.PushWarning("Army speech voice: " + error.Message);
                    return true;
                }, voice.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
        finally
        {
            prepared?.Dispose();
            if (!voice.Token.IsCancellationRequested)
                try
                {
                    await _service!.InvokeOnEngineThreadAsync(() =>
                    {
                        //A started player owns the slot until Finished/scene exit.
                        if (!_service.HasSpatialVoice(voice.Channel)) ReleaseArmySpeechVoice(voice, stopPlayer: false);
                        return true;
                    }, voice.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
        }
    }

    private void ReleaseArmySpeechVoice(ArmySpeechVoice voice, bool stopPlayer = true)
    {
        if (!_armySpeechVoices.Remove(voice.Channel)) return;
        voice.Lifetime.Cancel();
        if (stopPlayer && _service?.HasSpatialVoice(voice.Channel) == true) _service.StopSpatial(voice.Channel);
        voice.Lifetime.Dispose();
    }

    internal void StopArmySpeechVoices()
    {
        RequireEngineThread();
        foreach (var voice in _armySpeechVoices.Values.ToArray()) ReleaseArmySpeechVoice(voice);
        _armySpeechBanks.Clear();
        AudioServer.Lock();
        try { _armySpeechOutput?.Dispose(); _armySpeechOutput = null; }
        finally { AudioServer.Unlock(); }
    }
}
