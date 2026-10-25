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
        RefreshOrdinaryTacticBinding(session, map);
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
            _diplomacyNegotiation is not null || !map.MapSpeechManagerVisible;
        playback.Advance(delta, paused);
        if (_advancingPeriod)
        {
            //The worker owns its candidate and adoption identity until it ends.
            //Only the independent display clock may advance during that borrow.
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
                if (session.PendingNormalPeriod is null && owner.CampaignAppRandom32 is { } random)
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
        map.PresentArmyMapSpeech(playback.Visible, owner.ArmyMapSpeech!);
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
