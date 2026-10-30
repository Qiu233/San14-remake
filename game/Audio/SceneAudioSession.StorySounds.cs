using San14.Core;
using San14.Data;

public partial class SceneAudioSession
{
    internal async Task PlayStorySoundAsync(StoryAudio command, CancellationToken cancellation)
    {
        RequireEngineThread(); cancellation.ThrowIfCancellationRequested();
        if (_service is null || _repository is null || _sceneCancellation is null || SceneKey is null)
            throw new InvalidOperationException("story.audio.sound_scene_owner");
        if (command.Flags != 0 || command.ScalarB != 0 || !float.IsFinite(command.ScalarA))
            throw new InvalidDataException("story.audio.sound_manager_parameters");
        switch(command.AudioKind)
        {
            case StoryAudioKind.ChangeSelectedSoundVolumes:
                _service.ChangeSelectedStorySoundVolumes(command.ScalarA);return;
            case StoryAudioKind.FadeOutMatchingSounds:
                _service.ControlMatchingStorySounds(command.Id,command.ScalarA);return;
            case StoryAudioKind.StopMatchingSounds:
                _service.ControlMatchingStorySounds(command.Id,null);return;
        }
        bool fadeIn=command.AudioKind==StoryAudioKind.FadeInSoundIfNotSkipped;
        if(!fadeIn&&command.ScalarA!=0)
            throw new InvalidDataException("story.audio.sound_manager_parameters");
        switch (command.AudioKind)
        {
            case StoryAudioKind.StopAllSounds: _service.StopEffects(); return;
            case StoryAudioKind.StopSoundSlot: _service.StopOrdinarySoundSlot(command.Id); return;
            case StoryAudioKind.StopActiveSoundSlots: _service.StopActiveOrdinarySounds(); return;
            case StoryAudioKind.Sound:
            case StoryAudioKind.SoundIfNotSkipped:
            case StoryAudioKind.FadeInSoundIfNotSkipped: break;
            default: throw new InvalidDataException("story.audio.sound_operation");
        }
        if (NormalSoundSource.CueHash(command.Id) is null || _service.IsRecentOrdinarySound(command.Id)) return;
        long revision = SceneRevision;
        var owner = _sceneCancellation;
        using var preparation = CancellationTokenSource.CreateLinkedTokenSource(cancellation, owner.Token);
        var selected = await Task.Run(() =>
        {
            lock (_repository)
            {
                preparation.Token.ThrowIfCancellationRequested();
                return NormalSoundSource.Prepare(AudioSelectionResources.Load(_repository, NormalSoundSource.SelectionBankId),
                    _repository.LoadRdbAsset("RRPreview", 0x1CC2C167), command.Id);
            }
        }, preparation.Token);
        preparation.Token.ThrowIfCancellationRequested();
        if (SceneRevision != revision || !ReferenceEquals(owner, _sceneCancellation))
            throw new OperationCanceledException("Story sound belongs to a retired audio scene.");
        //Queue acceptance is synchronous on the actual audio service owner.
        //After this point only the map/scene lifetime controls the sound.
        _service.QueueStorySound(command.Id, selected, owner, owner.Token,fadeIn?command.ScalarA:null);
    }

    internal void StopStorySounds()
    {
        RequireEngineThread();
        if (_sceneCancellation is { } owner) _service?.StopOrdinarySoundsOwnedBy(owner);
    }
}
