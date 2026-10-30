using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class PersonnelSoundWorkspace(GameplaySession session, GameState owner, MapSceneView map, bool intelligenceCue, bool battleCue)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState Owner { get; set; } = owner;
        internal bool IntelligenceCue { get; } = intelligenceCue;
        internal bool BattleCue { get; } = battleCue;
        internal MapSceneView Map { get; } = map;
        internal CancellationTokenSource Lifetime { get; } = new();
        internal bool Running { get; set; } = true;
    }
    private PersonnelSoundWorkspace? _personnelSound;
    private bool IsPersonnelSoundCurrent => _personnelSound is { } sound &&
        ReferenceEquals(_session, sound.Session) && ReferenceEquals(_residentMap, sound.Map) &&
        ReferenceEquals(PersonnelPresentationOwner(sound.Session), sound.Owner);

    private bool TryPresentPersonnelSound(PersonnelPresentationEntry entry, GameplaySession session,
        GameState owner, MapSceneView map, int soundId)
    {
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("Personnel sound needs its actual scene audio owner.");
        var sound = new PersonnelSoundWorkspace(session, owner, map,
            entry.Source == CampaignEffectSource.IntelligenceCommands && soundId == 6,
            entry.Source == CampaignEffectSource.ArmyBattleDefeat && soundId is 6 or 8);
        _personnelSound = sound;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
        _ = Dispatch();
        return true;

        async Task Dispatch()
        {
            try
            {
                //405E40 queues an ordinary SE cue, including composite L/R cues. Wait for
                //actual queue admission (including its real duplicate/full gate).
                await audio.PlayNormalSoundAsync(soundId, sound.Lifetime.Token);
                if (_personnelSound != sound || !IsPersonnelSoundCurrent) return;
                //The admitted cue keeps its actual holder through a pure
                //map-speech publication; it is not queued a second time.
                var receiptOwner = sound.Owner;
                if (!session.TryCompletePersonnelPresentation(receiptOwner, entry.BatchSequence, entry.RequestIndex)) return;
                ClosePersonnelSound();
                if (!TryOpenPersonnelPresentation()) { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
            }
            catch (OperationCanceledException) when (sound.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (_personnelSound == sound && IsPersonnelSoundCurrent)
                { ClosePersonnelSound(); ReportPlayFailure(error); }
            }
            finally
            {
                sound.Running = false;
                if (_personnelSound != sound) sound.Lifetime.Dispose();
            }
        }
    }

    private void ClosePersonnelSound()
    {
        var sound = _personnelSound; _personnelSound = null;
        if (sound is null) return;
        sound.Lifetime.Cancel();
        if (!sound.Running) sound.Lifetime.Dispose();
        if (GodotObject.IsInstanceValid(sound.Map) && _residentMap == sound.Map) sound.Map.SetDetailsOpen(false);
    }
}
