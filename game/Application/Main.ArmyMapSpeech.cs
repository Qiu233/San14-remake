using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private ArmyMapSpeechPlayback? _armySpeechPlayback;
    private GameplaySession? _armySpeechSession;
    private MapSceneView? _armySpeechMap;
    private GameState? _armySpeechWorld;
    private readonly HashSet<long> _armySpeechFailedSequences = [];

    private void RefreshArmyMapSpeechBinding(GameplaySession session, GameState world, MapSceneView map)
    {
        if (world.ArmyMapSpeech is not { } snapshot) return;
        if (_armySpeechSession != session || _armySpeechMap != map)
        {
            CloseArmyMapSpeech();
            _armySpeechSession = session; _armySpeechMap = map;
            _armySpeechPlayback = new(snapshot);
            map.SetArmyMapSpeechDriver(delta => RunUiAction(() => AdvanceArmyMapSpeech(delta)));
        }
        else _armySpeechPlayback!.Synchronize(snapshot);
        if (!ReferenceEquals(_armySpeechWorld, world)) _armySpeechFailedSequences.Clear();
        _armySpeechWorld = world;
        map.PresentArmyMapSpeech(_armySpeechPlayback!.Visible);
    }

    private void AdvanceArmyMapSpeech(double delta)
    {
        if (_session is not { } session || _armySpeechSession != session ||
            _residentMap is not { } map || _armySpeechMap != map || _armySpeechPlayback is not { } playback ||
            _campaignComposition is not { } composition || _failed || _shuttingDown || IsReturningToTitle) return;
        var owner = PersonnelPresentationOwner(session);
        if (owner.ArmyMapSpeech is not { } snapshot) return;
        if (!ReferenceEquals(owner, _armySpeechWorld))
        { playback.Synchronize(snapshot); _armySpeechWorld = owner; _armySpeechFailedSequences.Clear(); }
        bool paused = _advancingPeriod || HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings ||
            HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null ||
            _diplomacyNegotiation is not null || !map.ArmySpeechManagerVisible;
        playback.Advance(delta, paused);
        if (_advancingPeriod)
        {
            //The worker owns its candidate and adoption identity until it ends.
            //Only the independent display clock may advance during that borrow.
            map.PresentArmyMapSpeech(playback.Visible);
            return;
        }
        //Only actual starts and completed balloons publish a new campaign owner.
        foreach (long sequence in playback.Finished.ToArray())
        {
            if (!session.TryCompleteArmyMapSpeech(owner, playback.CaptureSnapshot(), sequence)) break;
            owner = PersonnelPresentationOwner(session); playback.Synchronize(owner.ArmyMapSpeech!);
        }
        foreach (long sequence in playback.ReadyToStart.ToArray())
        {
            if (_armySpeechFailedSequences.Contains(sequence)) continue;
            var entry = snapshot.Entries.FirstOrDefault(row => row.Sequence == sequence);
            if (entry is null) continue;
            var rules = composition.PrepareArmyQueries(owner);
            if (!session.TryStartArmyMapSpeech(owner, playback.CaptureSnapshot(), sequence,
                map.HasArmySpeechLayout(entry.ArmyId), rules, out var result, new(_presentationRandom.State)))
            {
                _armySpeechFailedSequences.Add(sequence);
                if (result?.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                continue;
            }
            owner = PersonnelPresentationOwner(session); playback.Synchronize(owner.ArmyMapSpeech!);
            if (session.PendingNormalPeriod is null && owner.CampaignAppRandom32 is { } random)
                _presentationRandom.InstallCommittedState(random.State);
        }
        _armySpeechWorld = owner;
        map.PresentArmyMapSpeech(playback.Visible);
    }

    private (ArmyMapSpeechState? Snapshot, GameState? Owner) CaptureArmySpeechCheckpoint(GameplaySession session)
    {
        if (_armySpeechSession != session || _armySpeechPlayback is not { } playback) return (null, null);
        var owner = PersonnelPresentationOwner(session);
        if (owner.ArmyMapSpeech is not { } snapshot) return (null, null);
        playback.Synchronize(snapshot); _armySpeechWorld = owner;
        return (playback.CaptureSnapshot(), owner);
    }

    private void CloseArmyMapSpeech()
    {
        if (_armySpeechMap is { } map && GodotObject.IsInstanceValid(map)) map.SetArmyMapSpeechDriver(null);
        _armySpeechPlayback = null; _armySpeechSession = null; _armySpeechMap = null;
        _armySpeechWorld = null; _armySpeechFailedSequences.Clear();
    }
}
