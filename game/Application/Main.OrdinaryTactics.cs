using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record OrdinaryTacticEntry(TacticPresentationRequest Request, OrdinaryTacticMapNode Node);
    private GameplaySession? ordinaryTacticSession;
    private MapSceneView? ordinaryTacticMap;
    private readonly Dictionary<long, OrdinaryTacticEntry> ordinaryTacticNodes = [];
    private readonly HashSet<long> ordinaryTacticFailures = [];
    private readonly HashSet<long> ordinaryTacticSequences = [];
    private TacticPresentationRequest[]? ordinaryTacticEffects;
    private CancellationTokenSource ordinaryTacticLifetime = new();

    private static GameState OrdinaryTacticOwner(GameplaySession session) =>
        session.PendingManualTacticCandidate ?? session.PendingNormalPeriod?.Candidate ?? session.World;

    private void RefreshOrdinaryTacticBinding(GameplaySession session, MapSceneView map)
    {
        if (ordinaryTacticSession == session && ordinaryTacticMap == map) return;
        CloseOrdinaryTactics();
        ordinaryTacticSession = session; ordinaryTacticMap = map;
        map.SetOrdinaryTacticDriver(() => RunUiAction(AdvanceOrdinaryTactic));
    }

    private void AdoptOrdinaryTacticSession(GameplaySession previous, GameplaySession next, MapSceneView? map)
    {
        if (ordinaryTacticSession != previous || ordinaryTacticMap != map) return;
        // A successful worker adopts this same campaign's manager. The map
        // retains its real particles, audio attempts and per-node clocks.
        ordinaryTacticSession = next;
    }

    private void SynchronizeOrdinaryTacticNodes(TacticPresentationRequest[] effects)
    {
        if (ReferenceEquals(ordinaryTacticEffects, effects)) return;
        ordinaryTacticEffects = effects;
        ordinaryTacticSequences.Clear();
        foreach (var request in effects) ordinaryTacticSequences.Add(request.Sequence);
        foreach (long sequence in ordinaryTacticNodes.Keys.Where(sequence => !ordinaryTacticSequences.Contains(sequence)).ToArray())
        {
            ordinaryTacticNodes[sequence].Node.Dispose(); ordinaryTacticNodes.Remove(sequence);
        }
        ordinaryTacticFailures.IntersectWith(ordinaryTacticSequences);
    }

    private void AdvanceOrdinaryTactic()
    {
        if (_session is not { } session || ordinaryTacticSession != session || _residentMap is not { } map ||
            ordinaryTacticMap != map || _campaignComposition is not { } composition ||
            _advancingPeriod || _failed || _shuttingDown || IsReturningToTitle ||
            HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage ||
            HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null) return;
        var owner = OrdinaryTacticOwner(session);
        var manager = owner.ManualTacticIntervention;
        var effects = manager?.Effects ?? [];
        SynchronizeOrdinaryTacticNodes(effects);
        if (manager is null) return;

        // Install in the original manager order. A bank which is still being
        // prepared delays new starts, but never freezes already active nodes.
        foreach (var request in effects)
        {
            if (ordinaryTacticNodes.ContainsKey(request.Sequence)) continue;
            if (ordinaryTacticFailures.Contains(request.Sequence)) break;
            try
            {
                if (!TryStartOrdinaryTactic(session, composition, map, request)) break;
            }
            catch (Exception error)
            {
                ordinaryTacticFailures.Add(request.Sequence); GD.PushWarning(error.Message); break;
            }
        }

        bool retiredAny = false;
        foreach (var request in effects)
        {
            if (!ordinaryTacticNodes.TryGetValue(request.Sequence, out var entry) ||
                ordinaryTacticFailures.Contains(request.Sequence)) continue;
            try
            {
                entry.Node.Update(() => PublishOrdinaryTacticResults(session, composition, map, entry.Request));
                owner = OrdinaryTacticOwner(session); manager = owner.ManualTacticIntervention!;
                var playback = manager.OrdinaryPlaybacks.FirstOrDefault(row => row.Sequence == request.Sequence)
                    ?? throw new InvalidOperationException("tactic.playback.owner");
                if (!playback.ResultsPublished && entry.Node.Results is { IsCompleted: true } result)
                {
                    var admission = result.GetAwaiter().GetResult();
                    if (!admission.Completed) throw new InvalidDataException(admission.UnresolvedInput);
                    if (!session.TryCompleteOrdinaryTacticResults(owner, manager, request.Sequence))
                        throw new InvalidOperationException("tactic.result.receipt_owner");
                    owner = OrdinaryTacticOwner(session); manager = owner.ManualTacticIntervention!;
                    playback = manager.OrdinaryPlaybacks.Single(row => row.Sequence == request.Sequence);
                }
                if (!entry.Node.CanRetire || !playback.ResultsPublished) continue;
                //37F170 releases only this node's owned handles before unlink.
                var retired = entry.Node.BeginRetirement();
                if (!retired.IsCompleted) continue;
                retired.GetAwaiter().GetResult();
                entry.Node.Dispose();
                if (!session.TryCompleteOrdinaryTacticPresentation(owner, manager, request.Sequence))
                    throw new InvalidOperationException("tactic.retire.receipt_owner");
                ordinaryTacticNodes.Remove(request.Sequence); retiredAny = true;
            }
            catch (Exception error)
            {
                ordinaryTacticFailures.Add(request.Sequence); GD.PushWarning(error.Message);
            }
        }
        if (retiredAny) RefreshCurrentPlayWorld();
    }

    private bool TryStartOrdinaryTactic(GameplaySession session, CampaignComposition composition,
        MapSceneView map, TacticPresentationRequest request)
    {
        var owner = OrdinaryTacticOwner(session);
        var manager = owner.ManualTacticIntervention ?? throw new InvalidDataException("tactic.manager");
        var rules = composition.PrepareArmyQueries(owner);
        var publication = rules.QueryOrdinaryTacticPresentation(owner, request);
        var source = publication.Value ?? throw new InvalidDataException(publication.UnresolvedInput);
        OrdinaryTacticNamePublication? armyName = null;
        if (source.Beams.PrimaryView.Kind == OrdinaryTacticViewKind.Army)
        {
            var name = rules.QueryOrdinaryTacticName(owner, source);
            armyName = name.Value ?? throw new InvalidDataException(name.UnresolvedInput);
        }
        var audio = _sceneAudioBinding ?? throw new InvalidDataException("tactic.audio_scene");
        var playback = manager.OrdinaryPlaybacks.FirstOrDefault(row => row.Sequence == request.Sequence);
        if (playback is null)
        {
            composition.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
            if (_preferences.Voice.Enabled && source.Actors.Any(actor => actor.CasterSpatialVoiceId is not null) &&
                !audio.PrepareTacticSpatialVoiceBank(_preferences.Voice.Language)) return false;
            using var batch = audio.BeginTacticSpatialAudio(map.CaptureArmySpeechAudioContext(), _preferences.Voice.Enabled);
            rules.ConfigureArmyMapSpeechSpatialVoice(batch.TryReserve, map.SourceMainCameraTerrain);
            OrdinaryTacticAudioPreparation prepared;
            try { prepared = rules.PrepareOrdinaryTacticAudio(owner, source); }
            finally { rules.ConfigureArmyMapSpeechSpatialVoice(null, null); }
            if (!prepared.Available) throw new InvalidDataException(prepared.UnresolvedOwner);
            if (!session.TryBeginOrdinaryTacticPresentation(owner, manager, request.Sequence, prepared.State)) return false;
            owner = OrdinaryTacticOwner(session); manager = owner.ManualTacticIntervention!;
            playback = manager.OrdinaryPlaybacks.Single(row => row.Sequence == request.Sequence);
            batch.Commit();
            if (owner.CampaignAppRandom32 is { } random && session.PendingNormalPeriod is null)
                _presentationRandom.InstallCommittedState(random.State);
        }
        var node = new OrdinaryTacticMapNode(map, source, playback.ResultsPublished);
        try { map.StartOrdinaryTacticName(source, owner, session.PlayerForceId, armyName); }
        catch { node.Dispose(); throw; }
        ordinaryTacticNodes.Add(request.Sequence, new(request, node)); return true;
    }

    private Task<CaptureEffectAdmission> PublishOrdinaryTacticResults(GameplaySession session,
        CampaignComposition composition, MapSceneView map, TacticPresentationRequest request)
    {
        var current = OrdinaryTacticOwner(session);
        var results = composition.PrepareArmyQueries(current).QueryOrdinaryTacticResults(current, request, map.SourceMainCameraTerrain);
        var publication = results.Value ?? throw new InvalidDataException(results.UnresolvedInput);
        var dispatch = map.DispatchTacticResultEventsAsync(publication, _sceneAudioBinding!, ordinaryTacticLifetime.Token);
        map.PublishOrdinaryTacticNumbers(publication); return dispatch;
    }

    private void CloseOrdinaryTactics()
    {
        if (ordinaryTacticMap is { } map && GodotObject.IsInstanceValid(map)) map.SetOrdinaryTacticDriver(null);
        foreach (var entry in ordinaryTacticNodes.Values) entry.Node.Dispose();
        ordinaryTacticNodes.Clear(); ordinaryTacticEffects = null;
        ordinaryTacticFailures.Clear(); ordinaryTacticSequences.Clear();
        ordinaryTacticLifetime.Cancel(); ordinaryTacticLifetime.Dispose(); ordinaryTacticLifetime = new();
        ordinaryTacticSession = null; ordinaryTacticMap = null;
    }
}
