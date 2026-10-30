using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    // The title has its own fresh application environment. These are the
    // source constructor's values, independent of any loaded campaign.
    private sealed class TitleEventEnvironment
    {
        internal bool UsePrimaryPredicate { get; } = true;
        internal sbyte PrimaryPredicate { get; }
        internal int AdvancedPredicate { get; }
        internal int PlaybackSetting { get; set; }
        internal int Predicate => UsePrimaryPredicate ? PrimaryPredicate : AdvancedPredicate == 1 ? 1 : 0;
    }
    private readonly TitleEventEnvironment _titleEventEnvironment = new();
    private EditedEventTestWorkspace? _editedEventTest;

    private sealed class EditedEventTestWorkspace(EventEditorContentListView parent, EditedEventDraft draft,
        EditedEventTestPlayback session, EditedEventTestContext context, int savedSetting)
    {
        internal readonly EventEditorContentListView Parent = parent;
        internal readonly EditedEventDraft Draft = draft;
        internal readonly EditedEventTestPlayback Session = session;
        internal readonly EditedEventTestContext Context = context;
        internal readonly int SavedSetting = savedSetting;
        internal readonly CancellationTokenSource Lifetime = new();
        internal StoryPlaybackHost? Host;
        internal bool AudioEntered, Returning, Retired;
    }

    private bool IsEditedEventTestCurrent(EditedEventTestWorkspace owner) =>
        !_failed && !_shuttingDown && !owner.Retired && ReferenceEquals(_editedEventTest, owner) &&
        IsEditedEventContentsCurrent && ReferenceEquals(_editedEventContentsPage, owner.Parent) &&
        ReferenceEquals(_editedEventDraft, owner.Draft);

    private void PlayEditedEventTest()
    {
        if (!CanEditEditedEventContents || _repository is null || _editedEventContentsPage is not { } parent ||
            _editedEventContentsOwner is not { } draft) return;
        var store = GetEditedEventStore();
        var context = EditedEventTestContext.Load(_repository, _ui.SourceLocale,
            GetRegisteredOfficerStore().Current, CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current));
        var session = EditedEventTestPlayback.Create(draft.Slot, _editedEventContentsWorking, store.Current,
            store.ResourceUri, store.Revision, context, _titleEventEnvironment.Predicate);
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("story.audio.scene_owner");
        var owner = new EditedEventTestWorkspace(parent, draft, session, context, _titleEventEnvironment.PlaybackSetting);
        _editedEventTest = owner; _titleEventEnvironment.PlaybackSetting = 0;
        parent.SetBusy(true); parent.Hide();
        try
        {
            owner.AudioEntered = true;
            audio.BeginEventTest("title-event-test:" + draft.Slot, _ui.SourceLocale,
                _preferences.MusicSelection, owner.Lifetime.Token);
            var host = new StoryPlaybackHost { Name = "EditedEventTestPlayback" };
            owner.Host = host; AddChild(host);
            host.Configure(_repository, _ui, new(() => session.Story,
                () =>
                {
                    var prepared = session.PrepareCurrentStep();
                    if (prepared.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
                }, receipt => session.CompleteStep(session.Story, receipt),
                _ => session.QueryPresentation()), null, audio,
                _preferences.MusicSelection, () => IsEditedEventTestCurrent(owner) && CanAdvanceNarration,
                () => _preferences.AutomaticMessageAdvance, CurrentListScrollSpeed, HandleNarrationKeyboard,
                null, () => CompleteEditedEventTest(owner), error => FailEditedEventTest(owner, error), inputWaitKeyboard: HandleNarrationKeyboard);
        }
        catch { CloseEditedEventTest(restore: true); throw; }
    }

    private void CompleteEditedEventTest(EditedEventTestWorkspace owner)
    {
        if (!IsEditedEventTestCurrent(owner) || owner.Returning) return;
        owner.Returning = true; _ = ReturnAsync();
        async Task ReturnAsync()
        {
            try
            {
                var audio = _sceneAudioBinding ?? throw new InvalidOperationException("story.audio.scene_owner");
                var host = owner.Host ?? throw new InvalidOperationException("story.presentation.editor_test_owner");
                //The host owns black/retirement/audio/wait/reveal as one actual
                //operation; the outer editor callback runs only after its tail.
                await host.ReturnToEditor(audio.RestoreEventTestMusicAsync, () => IsReturningToTitle);
                if (IsEditedEventTestCurrent(owner))
                    CloseEditedEventTest(restore: true);
            }
            catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
            catch (Exception error) { FailEditedEventTest(owner, error); }
        }
    }

    private void FailEditedEventTest(EditedEventTestWorkspace owner, Exception error)
    {
        if (!IsEditedEventTestCurrent(owner)) return;
        CloseEditedEventTest(restore: true);
        RunUiAction(() => throw error);
    }

    private void CloseEditedEventTest(bool restore)
    {
        var owner = _editedEventTest; _editedEventTest = null;
        if (owner is null || owner.Retired) return;
        owner.Retired = true; owner.Lifetime.Cancel();
        owner.Host?.Abandon(); owner.Host?.QueueFree();
        if (owner.AudioEntered && _sceneAudioBinding is { } audio) _ = RestoreAsync(audio);
        else RestoreParent();

        void RestoreParent()
        {
            _titleEventEnvironment.PlaybackSetting = owner.SavedSetting;
            owner.Lifetime.Dispose();
            if (restore && !_failed && !_shuttingDown && IsEditedEventContentsCurrent &&
                ReferenceEquals(_editedEventContentsPage, owner.Parent)) RefreshEditedEventContents();
        }
        async Task RestoreAsync(SceneAudioBinding audio)
        {
            try { await audio.EndEventTest(restore && !_failed && !_shuttingDown); }
            catch (Exception error) { if (!_failed && !_shuttingDown) ShowFailure(error, startup: false); }
            finally { RestoreParent(); }
        }
    }
}
