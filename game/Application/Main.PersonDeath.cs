using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class PersonDeathSoundWorkspace(GameplaySession session, GameState owner, MapSceneView map)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState Owner { get; } = owner;
        internal MapSceneView Map { get; } = map;
        internal CancellationTokenSource Lifetime { get; } = new();
        internal bool Running { get; set; } = true;
    }
    private PersonDeathSoundWorkspace? _personDeathSound;
    private bool IsPersonDeathSoundCurrent => _personDeathSound is { } sound &&
        ReferenceEquals(_session, sound.Session) && ReferenceEquals(_residentMap, sound.Map) &&
        ReferenceEquals(PersonnelPresentationOwner(sound.Session), sound.Owner);

    private bool TryPresentPersonDeathSound(PersonnelPresentationEntry entry, GameplaySession session,
        GameState owner, MapSceneView map, int soundId)
    {
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("Death sound needs its actual scene audio owner.");
        var sound = new PersonDeathSoundWorkspace(session, owner, map);
        _personDeathSound = sound;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
        _ = Dispatch();
        return true;

        async Task Dispatch()
        {
            try
            {
                //405E40(...,22,0) is the ordinary SE manager call. Wait for
                //actual queue admission (including its real duplicate/full gate).
                await audio.PlayNormalSoundAsync(soundId, sound.Lifetime.Token);
                if (_personDeathSound != sound || !IsPersonDeathSoundCurrent) return;
                if (!session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex)) return;
                ClosePersonDeathSound();
                if (!TryOpenPersonnelPresentation()) { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
            }
            catch (OperationCanceledException) when (sound.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (_personDeathSound == sound && IsPersonDeathSoundCurrent)
                { ClosePersonDeathSound(); ReportPlayFailure(error); }
            }
            finally
            {
                sound.Running = false;
                if (_personDeathSound != sound) sound.Lifetime.Dispose();
            }
        }
    }

    private void ClosePersonDeathSound()
    {
        var sound = _personDeathSound; _personDeathSound = null;
        if (sound is null) return;
        sound.Lifetime.Cancel();
        if (!sound.Running) sound.Lifetime.Dispose();
        if (GodotObject.IsInstanceValid(sound.Map) && _residentMap == sound.Map) sound.Map.SetDetailsOpen(false);
    }
}
