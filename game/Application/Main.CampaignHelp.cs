using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class CampaignHelpRequest(EventEditorHelpView view, GameplaySession session,
        GameState committedWorld, MapSceneView map, CampaignPeriodPlayback? period)
    {
        internal readonly EventEditorHelpView View = view;
        internal readonly GameplaySession Session = session;
        internal readonly GameState CommittedWorld = committedWorld;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignPeriodPlayback? Period = period;
        internal readonly TaskCompletionSource<bool> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal GameState? BorrowedWorld;
        internal bool Attached, Closed;
    }
    private CampaignHelpRequest? campaignHelp;
    private bool HasCampaignHelp => campaignHelp is { Closed: false };

    private bool IsCampaignHelpCurrent(CampaignHelpRequest request) => !request.Closed &&
        ReferenceEquals(campaignHelp, request) && ReferenceEquals(_session, request.Session) &&
        ReferenceEquals(_world, request.CommittedWorld) && _residentMap == request.Map &&
        (request.Period is null || ReferenceEquals(_periodPlayback, request.Period)) && !_failed && !_shuttingDown;

    private void OpenCampaignHelp()
    {
        if (_repository is not { } repository || _campaignComposition?.Army is not { } rules ||
            _session is not { PlayerForceId: not null } session || _residentMap is not { } map ||
            !GameplayMapKeyboardAdmitted()) return;
        var period = _advancingPeriod ? _periodPlayback : null;
        if (_advancingPeriod && period is null) return;
        string locale = _ui.SourceLocale;
        var resourceSources = CaptureCampaignResourceSources();
        var view = new EventEditorHelpView();
        var request = new CampaignHelpRequest(view, session, session.World, map, period);
        campaignHelp = request;
        view.TreeExiting += () => ReleaseCampaignHelp(request, retireView: false);

        if (period is null) Attach(session.World);
        else
        {
            // Until the worker reaches its actual stage barrier, the real
            // pending page captures input but leaves its source-frame driver
            // running. No worker World is read here.
            view.BeginCampaignPreparation(() => IsCampaignHelpCurrent(request), () => CloseCampaignHelp(request));
            AddChild(view);
            map.SetSourceCameraInputSuspended(true);
            if (period.Candidate is { } borrowed) TryBeginCampaignHelpBorrow(period, borrowed);
            else period.RequestMenuBorrow();
        }
        if (!request.Closed) _ = PrepareAsync();

        void Attach(GameState owned)
        {
            OpenCommandDetails(view, _ =>
            {
                request.Attached = true;
                request.BorrowedWorld = owned;
                view.BeginCampaignPreparation(() => IsCampaignHelpCurrent(request), () => CloseCampaignHelp(request));
                request.Ready.TrySetResult(true);
            });
        }

        async Task PrepareAsync()
        {
            try
            {
                // Exclusive CPU resource owner: no Godot objects, World or RNG
                // enter this worker. The real pending page remains cancellable.
                var catalog = await Task.Run(() =>
                    resourceSources.CreateReadScope().LoadCampaignHelpCatalog(locale));
                if (!await request.Ready.Task || !IsCampaignHelpCurrent(request) || _ui.SourceLocale != locale ||
                    !GodotObject.IsInstanceValid(view) || !view.IsInsideTree()) return;
                var owned = request.BorrowedWorld ?? throw new InvalidOperationException("Help has no owned borrow.");
                if (request.Period is not null)
                {
                    var random = owned.CampaignAppRandom32 ?? throw new InvalidDataException("campaign.help.App32_unavailable");
                    _presentationRandom.InstallCommittedState(random.State);
                }
                // Exactly one real greeting expansion per actual open. Known
                // null is the original no-speaker pane, not an invalid page.
                AreaAdministrationQuery<CampaignHelpAdviser> adviser;
                try { adviser = rules.QueryCampaignHelpAdviser(owned, _presentationRandom.Next); }
                finally
                {
                    // A source branch can draw before a later input becomes
                    // unavailable. Those real draws still belong to this World.
                    if (request.Period is { } owner)
                    {
                        var replacement = owned with { CampaignAppRandom32 = new(_presentationRandom.State) };
                        if (!owner.TryAdoptMenuPresentationWorld(owned, replacement))
                            throw new InvalidOperationException("The Help playback borrow has retired.");
                        request.BorrowedWorld = replacement;
                    }
                }
                if (!adviser.Available) throw new InvalidDataException(adviser.UnresolvedInput);
                view.Configure(repository, _ui, catalog, () => IsCampaignHelpCurrent(request),
                    () => CloseCampaignHelp(request), adviser.Value, CurrentListScrollSpeed);
                view.RegisterHeader(_sourceHeaders);
            }
            catch (Exception error)
            {
                if (!IsCampaignHelpCurrent(request)) return;
                CloseCampaignHelp(request);
                ReportPlayFailure(error);
            }
        }
    }

    private bool TryBeginCampaignHelpBorrow(CampaignPeriodPlayback owner, GameState borrowed)
    {
        if (campaignHelp is not { Closed: false } request || !ReferenceEquals(request.Period, owner)) return false;
        if (!IsCampaignHelpCurrent(request) || !GodotObject.IsInstanceValid(request.View) ||
            !request.View.IsInsideTree() || request.View.IsQueuedForDeletion())
        { CloseCampaignHelp(request); return false; }
        if (request.Attached) return true;
        if (owner.MapMusicPresentationOpen || owner.TargetReactionPresentationOpen || owner.ArmyViewRetirementPresentationOpen || owner.BaseTierPresentationOpen) return true;
        owner.CancelMenuBorrow();
        owner.MenuPresentationOpen = true;
        OpenCommandDetails(request.View, _ =>
        {
            request.Attached = true;
            request.BorrowedWorld = borrowed;
            request.Map.SetSourceCameraInputSuspended(false);
            request.Ready.TrySetResult(true);
        }, allowDuringPeriod: true);
        return request.Attached && !request.Closed;
    }

    private void CloseCampaignHelp(CampaignHelpRequest request) => ReleaseCampaignHelp(request, retireView: true);

    private void ReleaseCampaignHelp(CampaignHelpRequest request, bool retireView)
    {
        if (request.Closed) return;
        request.Closed = true;
        bool held = request.Period?.MenuPresentationOpen == true;
        request.Period?.CancelMenuBorrow();
        if (request.Period is { } period) period.MenuPresentationOpen = false;
        request.BorrowedWorld = null;
        request.Ready.TrySetResult(false);
        if (ReferenceEquals(campaignHelp, request)) campaignHelp = null;
        if (GodotObject.IsInstanceValid(request.Map)) request.Map.SetSourceCameraInputSuspended(false);
        if (retireView && GodotObject.IsInstanceValid(request.View))
        {
            if (_commandDetailsView == request.View) CloseCommandDetails();
            else { request.View.Abandon(); request.View.QueueFree(); }
        }
        // Process any ordinary presentation deferred by this stack-top page
        // while the checkpoint is still safely borrowed. IsPaused is untouched.
        if (held && !_shuttingDown && !_failed && request.Period is { } owner &&
            ReferenceEquals(_periodPlayback, owner) && owner.Candidate is { } current)
            PublishPeriodPlayback(owner, request.Map, current);
    }

    private void CloseCampaignHelpForPeriod(CampaignPeriodPlayback? owner)
    {
        if (campaignHelp is { } request && ReferenceEquals(request.Period, owner)) CloseCampaignHelp(request);
    }

    private bool HandleCampaignHelpBack(InputEvent input)
    {
        if (campaignHelp is not { Closed: false } request) return false;
        if (input is InputEventKey { Keycode: Key.Escape } key)
        {
            if (key.Pressed && !key.Echo) request.View.RequestBack();
            return true;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } mouse)
        {
            if (!mouse.Pressed) request.View.RequestBack();
            return true;
        }
        return false;
    }
}
