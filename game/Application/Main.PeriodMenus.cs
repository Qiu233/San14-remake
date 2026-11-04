using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private enum PeriodMenuKind { Information, System }

    private sealed class PeriodMenuRequest(PeriodMenuKind kind, GameplaySession session,
        GameState committedWorld, MapSceneView map, CampaignPeriodPlayback period, Control pending)
    {
        internal readonly PeriodMenuKind Kind = kind;
        internal readonly GameplaySession Session = session;
        internal readonly GameState CommittedWorld = committedWorld;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignPeriodPlayback Period = period;
        internal readonly Control Pending = pending;
        internal GameState? BorrowedWorld;
        internal bool Attached, Closed;
    }

    private PeriodMenuRequest? periodMenu;
    private bool HasPeriodMenu => periodMenu is { Closed: false };

    private bool IsPeriodMenuCurrent(PeriodMenuRequest request) => !request.Closed &&
        ReferenceEquals(periodMenu, request) && ReferenceEquals(_session, request.Session) &&
        ReferenceEquals(_world, request.CommittedWorld) && ReferenceEquals(request.Session.World, request.CommittedWorld) &&
        _residentMap == request.Map && ReferenceEquals(_periodPlayback, request.Period) &&
        _advancingPeriod && !_failed && !_shuttingDown;

    // Only a checkpoint handed to Main may supply an inspection World. The
    // committed session remains the operation's anchor, never a replacement
    // for the actual advancing World displayed by this menu.
    private bool IsInspectionWorldCurrent(GameplaySession session, GameState world)
    {
        if (periodMenu is { Attached: true, BorrowedWorld: { } borrowed } request)
            return IsPeriodMenuCurrent(request) && ReferenceEquals(session, request.Session) &&
                ReferenceEquals(world, borrowed) && ReferenceEquals(request.Period.Candidate, borrowed);
        return !_advancingPeriod && ReferenceEquals(_session, session) &&
            ReferenceEquals(session.World, world) && ReferenceEquals(_world, world);
    }

    private bool TryAdoptPeriodMenuWorld(GameState previous, GameState replacement)
    {
        if (periodMenu is not { Attached: true } request || !IsPeriodMenuCurrent(request) ||
            !ReferenceEquals(request.BorrowedWorld, previous) ||
            !request.Period.TryAdoptMenuPresentationWorld(previous, replacement)) return false;
        request.BorrowedWorld = replacement;
        return true;
    }

    private bool TryRequestPeriodMenu(PeriodMenuKind kind)
    {
        if (!_advancingPeriod) return false;
        if (!GameplayMapKeyboardAdmitted() || _session is not { } session ||
            _residentMap is not { } map || _periodPlayback is not { } period) return true;
        var pending = new Control { Name = "PeriodMenuPreparation", MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 7 };
        var request = new PeriodMenuRequest(kind, session, session.World, map, period, pending);
        periodMenu = request;
        AddChild(pending);
        pending.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        pending.TreeExiting += () =>
        {
            if (!request.Attached && !request.Closed) ReleasePeriodMenu(request.Kind);
        };
        map.SetSourceCameraInputSuspended(true);
        if (period.Candidate is { } candidate) TryBeginPeriodMenuBorrow(period, candidate);
        else period.RequestMenuBorrow();
        return true;
    }

    private bool TryBeginPeriodMenuBorrow(CampaignPeriodPlayback owner, GameState borrowed)
    {
        if (periodMenu is not { Closed: false } request || !ReferenceEquals(request.Period, owner)) return false;
        if (!IsPeriodMenuCurrent(request)) { ReleasePeriodMenu(request.Kind); return false; }
        if (request.Attached) return true;
        // Keep the actual request pending until the audio host has accepted
        // this checkpoint's music; a settings page may replace its candidate.
        if (owner.MapMusicPresentationOpen || owner.TargetReactionPresentationOpen || owner.ArmyViewRetirementPresentationOpen || owner.BaseTierPresentationOpen) return true;
        if (!ReferenceEquals(owner.Candidate, borrowed))
            throw new InvalidOperationException("The menu checkpoint is not owned by Main.");
        owner.CancelMenuBorrow();
        owner.MenuPresentationOpen = true;
        request.BorrowedWorld = borrowed;
        request.Attached = true;
        try
        {
            if (request.Kind == PeriodMenuKind.Information) OpenInformationBrowserFromPeriod(borrowed);
            else OpenMapSystemMenuFromPeriod(borrowed);
        }
        catch
        {
            ReleasePeriodMenu(request.Kind);
            throw;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(request.Pending) && !request.Pending.IsQueuedForDeletion())
            {
                request.Pending.GetParent()?.RemoveChild(request.Pending);
                request.Pending.QueueFree();
            }
            if (GodotObject.IsInstanceValid(request.Map)) request.Map.SetSourceCameraInputSuspended(false);
        }
        return !request.Closed;
    }

    private void ReleasePeriodMenu(PeriodMenuKind kind)
    {
        if (periodMenu is not { Closed: false } request || request.Kind != kind) return;
        request.Closed = true;
        periodMenu = null;
        request.Period.CancelMenuBorrow();
        bool held = request.Attached && request.Period.MenuPresentationOpen;
        if (held) request.Period.MenuPresentationOpen = false;
        request.BorrowedWorld = null;
        if (GodotObject.IsInstanceValid(request.Pending) && !request.Pending.IsQueuedForDeletion())
        {
            request.Pending.GetParent()?.RemoveChild(request.Pending);
            request.Pending.QueueFree();
        }
        if (GodotObject.IsInstanceValid(request.Map)) request.Map.SetSourceCameraInputSuspended(false);
        // Return ordinary presentations deferred by this stack-top menu while
        // its last real checkpoint is still borrowed. IsPaused stays untouched.
        if (held && !_failed && !_shuttingDown && ReferenceEquals(_periodPlayback, request.Period) &&
            request.Period.Candidate is { } current)
            PublishPeriodPlayback(request.Period, request.Map, current);
    }

    private void ClosePeriodMenuForPeriod(CampaignPeriodPlayback? owner)
    {
        if (periodMenu is not { } request || !ReferenceEquals(request.Period, owner)) return;
        if (request.Attached)
        {
            if (request.Kind == PeriodMenuKind.System) CloseMapSystemMenu();
            else CloseCommandDetails(resumeParent: false);
        }
        ReleasePeriodMenu(request.Kind);
    }

    private bool HandlePeriodMenuBack(InputEvent input)
    {
        if (periodMenu is not { Closed: false } request) return false;
        bool cancel;
        if (input is InputEventKey { Keycode: Key.Escape } key) cancel = key.Pressed && !key.Echo;
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } mouse) cancel = !mouse.Pressed;
        else return false;
        if (!cancel) return true;
        if (!request.Attached) ReleasePeriodMenu(request.Kind);
        else if (_environmentConfirmation is { } environmentQuestion) environmentQuestion.RequestCancel();
        else if (_environmentSettings is { } environment) environment.RequestBack();
        else if (_scenarioSettingsView is { } scenario) scenario.RequestBack();
        else if (_mapSystemConfirmation is { } question) question.RequestCancel();
        else if (request.Kind == PeriodMenuKind.Information && _commandDetailsView is { } detail) detail.RequestClose();
        else if (request.Kind == PeriodMenuKind.System) RequestCloseMapSystemMenu();
        return true;
    }
}
