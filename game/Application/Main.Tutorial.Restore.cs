using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record TutorialLoadPreparation(GameplaySession Session,
        CampaignComposition Composition, TutorialLessonSession Lesson, TutorialCatalog Catalog);
    private TutorialLoadPreparation? _tutorialLoadPreparation;

    // ShowScenario calls this only after it has validated and constructed the
    // replacement campaign. A failed read therefore keeps the old lesson live.
    private void RetireTutorialLessonForLoad(CampaignLoadResult campaign)
    {
        TutorialLoadPreparation? prepared = null;
        if (campaign.Session.World.TutorialLessonProgress is not null)
        {
            if (_repository is null || campaign.Composition.Army is not { } rules)
                throw new InvalidDataException("tutorial.main.restore_owner");
            var audio = new CampaignNormalEventAudioInputs(_ui.SourceLocale,
                readApplicationField1AC: () => 0, readApplicationPointerPresent: () => true);
            var lesson = TutorialLessonSession.Restore(campaign.Session.World, rules, new(_repository, audio));
            if (campaign.Session.PlayerForceId != lesson.PlayerForceId)
                throw new InvalidDataException("tutorial.main.restore_player");
            var catalog = TutorialCatalog.Load(_repository, _ui.SourceLocale,
                new(ReadCourseCompleted: GetApplicationProfile().Current.ReadTutorialCompleted));
            prepared = new(campaign.Session, campaign.Composition, lesson, catalog);
        }
        RetireTutorialLessonForReturn();
        _tutorialLoadPreparation = prepared;
    }

    // The normal loader owns content validation, rules, pending transactions,
    // application attachment and random restoration. Borrow those exact owners;
    // this path never prepares a fresh lesson or enters strategy again.
    private void RestoreTutorialLessonAfterLoad()
    {
        if (_session?.World.TutorialLessonProgress is null)
        { _tutorialLoadPreparation = null; return; }
        if (_repository is null || _tutorialLoadPreparation is not { } prepared ||
            !ReferenceEquals(_session, prepared.Session) ||
            !ReferenceEquals(_campaignComposition, prepared.Composition) || HasTutorialLesson)
            throw new InvalidDataException("tutorial.main.restore_owner");

        var session = prepared.Session; var composition = prepared.Composition;
        var lesson = prepared.Lesson;
        composition.ConfigureApplicationProfile(GetApplicationProfile());

        // The original title instance is absent when loading from a normal
        // campaign. Retain a real course directory for the cleanup return,
        // without opening a TitleMenu that would remove the loaded session.
        if (_tutorialSelection is null)
        {
            var view = new TutorialSelectionView(); _tutorialSelection = view; AddChild(view);
            try
            {
                view.Configure(_repository, _ui, prepared.Catalog, () => false,
                    RequestTutorialLessonExit, _ => { }, RunUiAction);
                view.Hide();
            }
            catch
            {
                _tutorialSelection = null; view.Abandon(); view.QueueFree();
                throw;
            }
        }

        var owner = AttachTutorialLessonHost(lesson, session, composition);
        _tutorialLoadPreparation = null;
        owner.ApplicationContext = lesson.ReadApplicationContext();
        owner.ExitCleaned = lesson.IsExitCleanupCompleted;
        if (owner.ExitCleaned) { _mapHost.Hide(); _tutorialSelection.Show(); }
        else _mapHost.Show();
        _browserContent.ProcessMode = ProcessModeEnum.Inherit; _browserContent.Show();
        // Observe after the actual resident map is ready. Existing Story text,
        // NextStep, correction, notices and App32 remain the saved values.
        owner.LastProgress = null;
    }
}
