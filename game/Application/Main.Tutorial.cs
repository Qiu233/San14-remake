using San14.Data;

public partial class Main
{
    private TutorialSelectionView? _tutorialSelection;
    private TutorialSelectionPreparation? _tutorialPreparation;
    private readonly SemaphoreSlim tutorialPreparationGate = new(1, 1);
    private bool HasTutorialSelection => _tutorialSelection is not null;

    private sealed class TutorialSelectionPreparation(TutorialSelectionView view, int course)
    {
        internal readonly TutorialSelectionView View = view;
        internal readonly int Course = course;
        internal readonly CancellationTokenSource Lifetime = new();
        internal TutorialLessonPreparation? Prepared;
        internal GameResourceRepository? Repository;
        internal bool Preparing = true, Retired;
    }

    private void OpenTutorialSelection()
    {
        if (_failed || _shuttingDown || _repository is null || _titleMenu is not { } title ||
            HasTutorialSelection || HasCollectionMenu || HasTitleEditMenu || HasEnvironmentSettings ||
            HasCampaignSavePage || _titleConfirmation is not null) return;
        var profile = GetApplicationProfile().Current;
        // Older profiles retain unknown course bits. Opening this directory
        // reads progress; it never grants a lesson completion.
        var catalog = TutorialCatalog.Load(_repository, _ui.SourceLocale,
            new(ReadCourseCompleted: profile.ReadTutorialCompleted));
        var view = new TutorialSelectionView(); _tutorialSelection = view; AddChild(view); title.Hide();
        try
        {
            view.Configure(_repository, _ui, catalog,
                () => !_failed && !_shuttingDown && !HasTutorialLesson && _tutorialSelection == view && _titleMenu == title,
                () => RunUiAction(() => CloseTutorialSelection()),
                index => PrepareTutorialSelection(view, catalog, index), RunUiAction);
        }
        catch { CloseTutorialSelection(); throw; }
    }

    private void PrepareTutorialSelection(TutorialSelectionView view, TutorialCatalog catalog, int course)
    {
        RetireTutorialPreparation();
        if (_tutorialSelection != view || _repository is null) return;
        var owner = new TutorialSelectionPreparation(view, course); _tutorialPreparation = owner;
        var resourceSources = CaptureCampaignResourceSources();
        var inputs = new TutorialPreparationInputs(1, new(_presentationRandom.State), CaptureApplicationCrtRandom(),
            _preferences.Voice.Capture(), _newGameOptions);
        _ = PrepareAsync();

        async Task PrepareAsync()
        {
            try
            {
                await tutorialPreparationGate.WaitAsync(owner.Lifetime.Token);
                (TutorialLessonPreparation Prepared, GameResourceRepository Repository) result;
                try
                {
                    result = await Task.Run(() =>
                    {
                        owner.Lifetime.Token.ThrowIfCancellationRequested();
                        var repository = resourceSources.CreateReadScope();
                        return (catalog.PrepareLesson(repository, course, inputs), repository);
                    }, owner.Lifetime.Token);
                }
                finally { tutorialPreparationGate.Release(); }
                if (!IsTutorialPreparationCurrent(owner)) return;
                owner.Prepared = result.Prepared; owner.Repository = result.Repository;
                view.ApplyPreparation(result.Prepared);
                if (result.Prepared.UnresolvedOwner is null && course is >= 0 and <= 7)
                    BeginTutorialLesson(owner, result.Prepared);
            }
            catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (IsTutorialPreparationCurrent(owner)) view.ApplyPreparationFailure(error.Message);
            }
            finally
            {
                owner.Preparing = false;
                if (owner.Retired) owner.Lifetime.Dispose();
            }
        }
    }

    private bool IsTutorialPreparationCurrent(TutorialSelectionPreparation owner) =>
        !_failed && !_shuttingDown && !owner.Retired &&
        ReferenceEquals(_tutorialSelection, owner.View) && ReferenceEquals(_tutorialPreparation, owner);

    private void RetireTutorialPreparation()
    {
        var owner = _tutorialPreparation; _tutorialPreparation = null;
        if (owner is null || owner.Retired) return;
        owner.Retired = true; owner.Lifetime.Cancel(); owner.Prepared = null; owner.Repository = null;
        if (!owner.Preparing) owner.Lifetime.Dispose();
    }

    private void RequestTutorialBack()
    {
        if (HasTutorialLesson) { RequestTutorialLessonExit(); return; }
        _tutorialSelection?.RequestBack();
    }

    private void CloseTutorialSelection(bool resumeTitle = true)
    {
        CloseTutorialLesson(resumeDirectory: false);
        RetireTutorialPreparation();
        var view = _tutorialSelection; _tutorialSelection = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (resumeTitle) ResumeTitleMenu();
    }
}
