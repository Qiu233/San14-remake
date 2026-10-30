using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private TransferServiceProfileStore? applicationProfileStore;
    private CampaignServiceProfileOperation? applicationProfileOperation;

    /// <summary>Capture the host's real independent profile for this rules
    /// instance. Private workers prepare copies and never write this store.</summary>
    public void ConfigureApplicationProfile(TransferServiceProfileStore store)
    {
        applicationProfileStore = store ?? throw new ArgumentNullException(nameof(store));
        applicationProfileOperation = new(store.Current);
    }

    /// <summary>Called by the host only after adopting the actual candidate.
    /// A failed atomic profile save leaves both the store and world outbox
    /// intact. Replaying an old campaign only merges monotone progress.</summary>
    public void PublishPendingApplicationServices(GameState committed)
    {
        var changes = committed.PendingCampaignServices;
        if (changes.Events.Length == 0 && changes.Achievements.Length == 0 &&
            changes.CompletedScenarios.Length == 0 && changes.CompletedEndingTracks.Length == 0 &&
            changes.UnlockedBonusOfficerIndices.Length == 0 && changes.CompletedTutorialLessons.Length == 0 && !changes.Dirty) return;
        var store = applicationProfileStore ?? throw ApplicationProfileMissing("store");
        var before = store.Current;
        var operation = new CampaignServiceProfileOperation(before);
        operation.BeginOperation(changes);
        var merged = operation.Materialize(changes);
        bool changed = changes.Events.Any(id => before.ReadEventFlag(id) != true) ||
            changes.Achievements.Any(write => before.ReadPrimaryFlag(write.Id) != true) ||
            changes.CompletedScenarios.Any(id => before.ReadScenarioCompleted(id) != true) ||
            changes.CompletedEndingTracks.Any(id => before.ReadEndingTrack(id) is not >= 1) ||
            changes.UnlockedBonusOfficerIndices.Any(id => before.ReadBonusOfficerUnlocked(id) != true) ||
            changes.CompletedTutorialLessons.Any(id => before.ReadTutorialCompleted(id) != true) ||
            changes.Dirty && before.DirtyFlag != 1;
        //Saved per-attempt clock observations must not become the live
        //profile's provider for a later, unrelated completion.
        merged.AchievementClockSnapshotProvider = before.AchievementClockSnapshotProvider;
        applicationProfileOperation = new(changed ? store.Save(merged) : before);
    }

    internal GameState PrepareApplicationServiceCurrent(GameState state, SourceWorldContext current,
        Func<TransferServiceState, TransferServiceState> prepare)
    {
        var operation = applicationProfileOperation ?? throw ApplicationProfileMissing("store");
        BindApplicationServiceCurrent(state, current);
        operation.BeginOperation(state.PendingCampaignServices);
        var service = operation.Materialize(state.PendingCampaignServices);
        current.TransferService = service;
        var candidate = prepare(service);
        current.TransferService = candidate;
        return state with { PendingCampaignServices = operation.Capture(candidate) };
    }

    internal void BindApplicationServiceCurrent(GameState state, SourceWorldContext current)
    {
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        current.TransferModeStateReader = ReadApplicationTransferModeCurrent;
    }

    public CampaignStoryBindings CreateNormalTurnStoryBindings(IS14AudioEventExecutionState audio,
        Func<CampaignCameraSnapshot?> captureCamera, int? applicationUserIndex = null, bool? modalParentPresent = null)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(captureCamera);
        var application = ReadApplicationGameplay() ?? throw ApplicationProfileMissing("application_context");
        var profile = applicationProfileOperation?.Current ?? throw ApplicationProfileMissing("store");
        var mode = application.TutorialServicePresent switch
        {
            false => new TransferModeState(false, null),
            true => new TransferModeState(true, application.TutorialActive is bool active ? active ? 1 : 0 : null),
            _ => null
        };
        //No user-index value is fabricated when its service is absent; the
        //original short-circuit never reads BC on that route.
        return new(audio, profile, false, mode, application.UserSelectionServicePresent, applicationUserIndex, captureCamera) { ModalParentPresent = modalParentPresent };
    }

    private static InvalidDataException ApplicationProfileMissing(string field) => new("application.profile." + field);
}
