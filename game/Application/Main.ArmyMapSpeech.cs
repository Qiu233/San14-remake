using Godot;
using San14.Core;
using San14.Client.Gameplay;
using San14.Data;

public partial class Main
{
    private ArmyMapSpeechPlayback? _armySpeechPlayback;
    private GameplaySession? _armySpeechSession;
    private MapSceneView? _armySpeechMap;
    private GameState? _armySpeechWorld;
    private readonly HashSet<long> _armySpeechFailedSequences = [];

    private void RefreshArmyMapSpeechBinding(GameplaySession session, GameState world, MapSceneView map)
    {
        RefreshOrdinaryTacticBinding(session, map);
        if (world.ArmyMapSpeech is not { } snapshot) return;
        if (_armySpeechSession != session || _armySpeechMap != map)
        {
            ReleaseArmyMapSpeechBinding();
            _armySpeechSession = session; _armySpeechMap = map;
            _armySpeechPlayback = new(snapshot);
            map.SetArmyMapSpeechDriver(delta => RunUiAction(() => AdvanceArmyMapSpeech(delta)),
                entry => _campaignComposition is { } composition && _armySpeechSession == session
                    ? composition.PrepareArmyQueries(PersonnelPresentationOwner(session))
                        .QueryBaseMapSpeechSpeaker(PersonnelPresentationOwner(session), entry)
                    : new(null, "map-speech.owned_portrait_session"));
        }
        else _armySpeechPlayback!.Synchronize(snapshot);
        if (!ReferenceEquals(_armySpeechWorld, world)) _armySpeechFailedSequences.Clear();
        _armySpeechWorld = world;
        map.PresentArmyMapSpeech(_armySpeechPlayback!.Visible, snapshot);
    }

    private void AdvanceArmyMapSpeech(double delta)
    {
        if (_session is not { } session || _armySpeechSession != session ||
            _residentMap is not { } map || _armySpeechMap != map || _armySpeechPlayback is not { } playback ||
            _campaignComposition is not { } composition || _failed || _shuttingDown || IsReturningToTitle) return;
        var owner = PersonnelPresentationOwner(session);
        if (owner.ArmyMapSpeech is not { } snapshot) return;
        var commandOwner = owner;
        if (!ReferenceEquals(owner, _armySpeechWorld))
        { playback.Synchronize(snapshot); _armySpeechWorld = owner; _armySpeechFailedSequences.Clear(); }
        bool paused = _advancingPeriod || HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings ||
            HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null ||
            _diplomacyNegotiation is not null || !map.MapSpeechManagerVisible;
        playback.Advance(delta, paused);
        if (_advancingPeriod || HasDuelPresentation)
        {
            //The worker or duel host owns its exact prepared candidate until
            //completion. Only the independent display clock advances here.
            map.PresentArmyMapSpeech(playback.Visible, snapshot);
            return;
        }
        //Saved text keeps its original selected sounds. Restoring an unaccepted
        //queue never re-runs the formatter or consumes another random draw.
        foreach (var entry in snapshot.Entries)
        {
            if (entry.TextAtSource is null || entry.SpatialVoicesAccepted ||
                entry.SpatialVoices is not { IsEmpty: false } voices || _armySpeechFailedSequences.Contains(entry.Sequence)) continue;
            try
            {
                var audio = _sceneAudioBinding ?? throw new InvalidDataException("army-map-speech.audio_scene");
                bool prepared = true;
                foreach (var language in voices.Select(voice => voice.Voice.Language).Distinct())
                    prepared &= audio.PrepareArmySpeechBank(language == PersonnelVoiceLanguage.Japanese
                        ? NarrationVoiceLanguage.Japanese : NarrationVoiceLanguage.Chinese);
                if (!prepared) continue;
                using var batch = audio.BeginArmySpeechAudio(map.CaptureArmySpeechAudioContext(), true);
                bool reserved = true;
                foreach (var voice in voices) reserved &= batch.TryReserve(voice);
                if (!reserved) continue;
                batch.Commit();
                if (session.TryCompleteArmyMapSpeechVoices(owner, entry.Sequence))
                { owner = PersonnelPresentationOwner(session); playback.Synchronize(owner.ArmyMapSpeech!); }
                else
                { batch.Abort(); throw new InvalidOperationException("army-map-speech.voice_receipt_owner"); }
            }
            catch (Exception error)
            { _armySpeechFailedSequences.Add(entry.Sequence); GD.PushWarning(error.Message); }
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
            var entry = owner.ArmyMapSpeech!.Entries.FirstOrDefault(row => row.Sequence == sequence);
            if (entry is null) continue;
            var rules = composition.PrepareArmyQueries(owner);
            try
            {
                if (map.SourceMainCameraTerrain is null)
                    throw new InvalidDataException("army-map-speech.source_Landscape_not_ready");
                //All finite roots use the same installed memory bank. Bind the
                //real admission owner before any formatter can select Voice2/3/4/5.
                var audio = _sceneAudioBinding ?? throw new InvalidDataException("army-map-speech.audio_scene");
                if (_preferences.Voice.Enabled && entry.ActionCode is not (15 or 16) &&
                    !audio.PrepareArmySpeechBank(_preferences.Voice.Language)) continue;
                using var batch = audio.BeginArmySpeechAudio(map.CaptureArmySpeechAudioContext(), _preferences.Voice.Enabled);
                rules.ConfigureArmyMapSpeechSpatialVoice(batch.TryReserve, map.SourceMainCameraTerrain);
                if (!session.TryStartArmyMapSpeech(owner, playback.CaptureSnapshot(), sequence,
                    map.PrepareArmySpeechLayout(entry), rules, out var result, new(_presentationRandom.State)))
                {
                    _armySpeechFailedSequences.Add(sequence);
                    if (result?.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                    continue;
                }
                owner = PersonnelPresentationOwner(session); playback.Synchronize(owner.ArmyMapSpeech!);
                if ((session.PendingNormalPeriod is null || IntelligenceNarrationContinuation(owner) is not null ||
                    BaseCapturePersonPrefixProgress.IsPresentationPending(owner) || HiredArmyCaptureProgress.IsPresentationPending(owner)) &&
                    owner.CampaignAppRandom32 is { } random)
                    _presentationRandom.InstallCommittedState(random.State);
                if (owner.ArmyMapSpeech!.Entries.FirstOrDefault(row => row.Sequence == sequence) is
                    { TextAtSource: not null, SpatialVoices: { IsEmpty: false } })
                {
                    batch.Commit();
                    if (session.TryCompleteArmyMapSpeechVoices(owner, sequence))
                    { owner = PersonnelPresentationOwner(session); playback.Synchronize(owner.ArmyMapSpeech!); }
                    else
                    { batch.Abort(); throw new InvalidOperationException("army-map-speech.voice_receipt_owner"); }
                }
            }
            catch (Exception error)
            { _armySpeechFailedSequences.Add(sequence); GD.PushWarning(error.Message); }
            finally { rules.ConfigureArmyMapSpeechSpatialVoice(null, null); }
        }
        _armySpeechWorld = owner;
        AdoptRetainedSpeechWorld(session, map, commandOwner, owner);
        map.PresentArmyMapSpeech(playback.Visible, owner.ArmyMapSpeech!);
    }

    private void AdoptRetainedSpeechWorld(GameplaySession session, MapSceneView map,
        GameState previous, GameState replacement)
    {
        // This entry belongs only to the synchronous speech commit above.
        // Shared admission is checked once; each live page retains its own
        // strict predecessor, view and parent lifecycle below.
        if (ReferenceEquals(previous, replacement) || _session != session || _residentMap != map) return;
        //Real NoWait intelligence/battle cues retain their queue-admission
        //holder through this exact speech-only commit, outside a typed wait.
        if (_personnelSound is { } sound && (sound.IntelligenceCue || sound.BattleCue || BaseCapturePersonPrefixProgress.IsPresentationPending(previous)) &&
            sound.Session == session && sound.Map == map &&
            ReferenceEquals(sound.Owner, previous) && ReferenceEquals(PersonnelPresentationOwner(session), replacement) &&
            ReferenceEquals(previous.NativePeriodContinuation, replacement.NativePeriodContinuation) &&
            ReferenceEquals(previous.PersonnelCommands, replacement.PersonnelCommands) &&
            (!sound.BattleCue || ReferenceEquals(previous.ArmyDefeatContinuation, replacement.ArmyDefeatContinuation)) &&
            ReferenceEquals(previous.CampaignEffectRequests, replacement.CampaignEffectRequests)) sound.Owner = replacement;
        if ((session.PendingNormalPeriod?.Candidate ?? session.PendingArmyProgressCandidate) is { } pendingCandidate)
        {
            //Only this synchronous speech publication may adopt a retained
            //wait presentation. Its continuation and outbox owners are unchanged.
            if (!ReferenceEquals(pendingCandidate, replacement) ||
                IntelligenceNarrationContinuation(previous) is null && !BaseCapturePersonPrefixProgress.IsPresentationPending(previous) &&
                    !HiredArmyCaptureProgress.IsPresentationPending(previous) ||
                BaseCapturePersonPrefixProgress.IsPresentationPending(previous) &&
                    !ReferenceEquals(previous.BaseCaptureContinuation, replacement.BaseCaptureContinuation) ||
                HiredArmyCaptureProgress.IsPresentationPending(previous) &&
                    (!ReferenceEquals(previous.ArmyDefeatContinuation, replacement.ArmyDefeatContinuation) ||
                        !ReferenceEquals(previous.NativePeriodContinuation?.Cursor, replacement.NativePeriodContinuation?.Cursor)) ||
                !ReferenceEquals(previous.NativePeriodContinuation, replacement.NativePeriodContinuation) ||
                !ReferenceEquals(previous.PersonnelCommands, replacement.PersonnelCommands) ||
                !ReferenceEquals(previous.CampaignEffectRequests, replacement.CampaignEffectRequests)) return;
            map.AdoptSpeechWorld(previous, replacement);
            if ((_personnelPresentationIntelligenceWait || _personnelPresentationBaseCaptureWait || _personnelPresentationHiredCaptureWait) && _personnelPresentation is not null &&
                _personnelPresentationSession == session && _personnelPresentationMap == map &&
                ReferenceEquals(_personnelPresentationWorld, previous))
                _personnelPresentationWorld = replacement;
            if (_duelCaptureAnnouncement is { RetainedHiredOwner: true, Playback: null } capture &&
                capture.Session == session && capture.Map == map && ReferenceEquals(capture.World, previous))
                capture.World = replacement;
            return;
        }
        if (!ReferenceEquals(session.World, replacement)) return;
        map.AdoptSpeechWorld(previous, replacement);
        AdoptUnitOrderSpeechWorld(session, previous, replacement);
        AdoptMapSystemSpeechWorld(session, previous, replacement);
        AdoptAreaAdministrationSpeechWorld(session, previous, replacement);
        AdoptCityDomesticSpeechWorld(session, previous, replacement);
        AdoptForceExperienceSpeechWorld(session, previous, replacement);
        AdoptGovernanceRosterSpeechWorld(session, previous, replacement);
        AdoptGovernanceNarrationSpeechWorld(session, previous, replacement);
        AdoptEnemyFootholdSpeechWorld(session, previous, replacement);
        AdoptProposalsSpeechWorld(session, previous, replacement);
        AdoptCityMerchantSpeechWorld(session, previous, replacement);
        AdoptCityOfficerMovementSpeechWorld(session, previous, replacement);
        AdoptTransportSetupSpeechWorld(session, previous, replacement);
        AdoptPersonnelItemSpeechWorld(session, previous, replacement);
        AdoptOfficerOfficeSpeechWorld(session, previous, replacement);
        AdoptPersonnelAdministrationSpeechWorld(session, previous, replacement);
        AdoptPersonnelGroupsSpeechWorld(session, previous, replacement);
        AdoptTalentOrdersSpeechWorld(session, previous, replacement);
        AdoptTalentAutoSearchSpeechWorld(session, previous, replacement);
        AdoptDiplomacyOrdersSpeechWorld(session, previous, replacement);
        AdoptForeignTradeSpeechWorld(session, previous, replacement);
        AdoptDistrictAdministrationSpeechWorld(session, previous, replacement);
        AdoptDoctrineResearchSpeechWorld(session, previous, replacement);
        AdoptStrategicOrdersSpeechWorld(session, previous, replacement);
        AdoptIntelligenceSpeechWorld(session, previous, replacement);
        AdoptSceneryInvestmentSpeechWorld(session, previous, replacement);
        AdoptMilitaryPagesSpeechWorld(session, previous, replacement);
        AdoptCommandDetailsSpeechWorld(session, previous, replacement);
    }

    private static PersonnelIntelligenceNarrationPending? IntelligenceNarrationContinuation(GameState owner)
    {
        if (owner.NativePeriodContinuation?.Cursor?.AwaitingIntelligenceTaskId is not int taskId) return null;
        return owner.PersonnelCommands?.Intelligences?.GetValueOrDefault(taskId)?.NarrationProgress?.Pending is
            { Phase: PersonnelIntelligenceNarrationPhase.AwaitingNarration or PersonnelIntelligenceNarrationPhase.ReadyToResume } wait
            ? wait : null;
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
        CloseOrdinaryTactics();
        ReleaseArmyMapSpeechBinding();
    }

    private void ReleaseArmyMapSpeechBinding()
    {
        if (_armySpeechMap is { } map && GodotObject.IsInstanceValid(map)) map.SetArmyMapSpeechDriver(null);
        _armySpeechPlayback = null; _armySpeechSession = null; _armySpeechMap = null;
        _armySpeechWorld = null; _armySpeechFailedSequences.Clear();
    }
}
