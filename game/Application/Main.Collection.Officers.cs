using San14.Data;
using San14.Core;
using Godot;

public partial class Main
{
    private CollectionOfficersView? _collectionOfficers;

    private void OpenCollectionOfficers()
    {
        if (_failed || _shuttingDown || _repository is null ||
            _collectionMenu is not { } parent || HasCollectionChild) return;
        var catalog = _repository.LoadCollectionOfficerCatalog(_ui.SourceLocale,
            CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current));
        var view = new CollectionOfficersView(); _collectionOfficers = view; AddChild(view); parent.Hide();
        try
        {
            var prepared = TryBorrowApplicationPreparedResources(view);
            view.Configure(_repository, _ui, catalog,
                () => !_failed && !_shuttingDown && _collectionMenu == parent &&
                    _collectionOfficers == view && _titleMenu is not null,
                () => RunUiAction(() => CloseCollectionOfficers(restoreMenu: true)),
                CurrentListScrollSpeed, RunUiAction, PlayCollectionOfficerVoice, prepared.CreateChromeResources());
        }
        catch { CloseCollectionOfficers(restoreMenu: true); throw; }
    }

    private void CloseCollectionOfficers(bool restoreMenu)
    {
        var view = _collectionOfficers; _collectionOfficers = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (restoreMenu && !_failed && !_shuttingDown) _collectionMenu?.Show();
    }

    private int PlayCollectionOfficerVoice(CollectionOfficerDetail person, int previousVoiceEvent)
    {
        if (_collectionOfficers is null || _sceneAudioBinding is not { } audio)
            throw new InvalidOperationException(_ui.Get("scene_audio_unavailable"));
        var speaker = person.VoiceSpeaker ?? throw new InvalidDataException("collection.person.voice_speaker");
        audio.StopUniqueTacticVoice();
        var prepared = PersonVoicePublication.PreparePreview(new CampaignAppRandomState(_presentationRandom.State),
            speaker, _preferences.Voice.Capture(), audio.EventPlaybackAudioSuppressed, previousVoiceEvent);
        if (!prepared.Available) throw new InvalidDataException(prepared.UnresolvedOwner);
        if (prepared.RandomState is { } random) _presentationRandom.InstallCommittedState(random.State);
        if (prepared.SelectedVoice is { } selected) _ = ObserveCollectionOfficerVoice(audio.PlayPersonVoiceAsync(selected));
        return prepared.ActualSelectedEvent ?? previousVoiceEvent;
    }

    private static async Task ObserveCollectionOfficerVoice(Task<SceneAudioChannel> request)
    {
        try { await request; }
        catch (OperationCanceledException) { }
        catch (Exception error) { GD.PushWarning(error.Message); }
    }
}
