using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Transport's single tag1 target row. Menus and intermediate points
/// are local. The caller publishes only after decision, Yes and narration.</summary>
internal sealed class TransportTargetPresenter : IDisposable
{
    private readonly DeploymentTargetView view;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int source, force;
    private readonly Func<bool> current;
    private readonly Func<int, Vector2?> project;
    private readonly Action<DispatchTransportArmyOrder> accepted;
    private readonly Action back;
    private readonly Action<string> unavailable;
    private readonly Action<bool> modalChanged;
    private readonly Func<bool, int, Action<GameState>, bool>? saveDefaults;
    private readonly Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged;
    private TransportDraftRouteQuery routeQuery;
    private readonly TransportTargetOptionsView options;
    private readonly List<TransportDraftMarch> segments = [];
    private IReadOnlyList<TransportDraftMarch> savedSegments = [];
    private (int Start,int Goal,TransportDraftMarch Route)? candidateSegment;
    private bool routePublicationDirty=true;
    private IReadOnlyList<MapRoutePreviewPath> routePublication=[];
    private ArmyCommandMenu? menu;
    private DeploymentTargetConfirmation? confirmation;
    private int? previewHex;
    private bool disposed;
    internal TransportTargetDraft Draft { get; }

    internal TransportTargetPresenter(DeploymentTargetView view, GameResourceRepository repository, PreviewText text,
        GameState world, ArmyCampaignRuleAdapter rules, int source, int force, TransportSetupSelection selection,
        San14.Data.TransportSetupView setup, TransportQuantityView amounts, UiSourceHeaderStack headers,
        Func<bool> current, Func<int, Vector2?> project, Func<int, Vector2?> projectMarker, Func<int> scrollSpeed,
        Action<DispatchTransportArmyOrder> accepted, Action back, Action<string> unavailable, Action<bool> modalChanged,
        Func<bool, int, Action<GameState>, bool>? saveDefaults = null,
        Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged = null,
        AdministrationPageChrome.Resources? uiResources=null)
    {
        this.view = view; this.repository = repository; this.text = text; this.world = world; this.rules = rules;
        this.source = source; this.force = force; this.current = current; this.project = project;
        this.accepted = accepted; this.back = back; this.unavailable = unavailable;
        this.modalChanged = modalChanged; this.saveDefaults = saveDefaults;
        this.routePreviewChanged=routePreviewChanged;
        routeQuery=rules.CreateTransportDraftRouteQuery(world,source,selection.OfficerId,selection.Troops,selection.NavalFormationId);
        var card = rules.QueryTransportTargetCard(world, source, selection.OfficerId, selection.Troops, selection.NavalFormationId);
        if (card.HasCurrentHex != true || card.CurrentHex is not int start)
            throw new InvalidDataException(card.PositionUnresolvedInput ?? "transport.target.current-hex");
        Draft = new(selection, start);
        var portrait = rules.QueryOfficerPortraitRoutes(world, [selection.OfficerId]).GetValueOrDefault(selection.OfficerId);
        var content = DeploymentTargetContent.CreateTransport(repository, text, world, selection.OfficerId,
            selection.Troops, card, portrait, 0, false);
        var city = world.Cities[source];
        view.Configure(repository, text, city, setup.SourceTroops ?? throw new InvalidDataException("Transport source troops are unknown."),
            setup.CommandBooks, [new(source, city.Name, [content])], [selection.OfficerId], scrollSpeed(),
            ids => Select(ids.Contains(selection.OfficerId)), () => { }, RequestBack, scrollSpeed, transportMode: true,resources:uiResources);
        view.ApplyTransportResources(amounts.RemainingTroops, amounts.RemainingGold, amounts.RemainingSupplies,
            setup.CommandBooks, setup.CommandBooksAfter, setup.CommandBookCost);
        view.RegisterHeader(headers); view.ConfigureSourceRoute(repository, projectMarker);
        view.ConfigureReturnHint(new AdministrationMessages(repository,text.SourceLocale).Format(0x2733),Admitted);
        options = new TransportTargetOptionsView(); view.AddPolicyPanel(options);
        options.InputAdmission=Admitted;
        options.HintChanged=hint=>{if(Admitted())view.ApplyHint(hint);};
        options.Configure(repository, text, ChangeOption, SaveDefaults,resources:view.SourceResources); RestoreOptions(); Refresh();
    }
    private bool Admitted() => !disposed && current() && confirmation is null;
    internal bool AllowPointer(int hex) => Admitted() && (uint)hex < (uint)(world.MapWidth * world.MapHeight);
    private void Select(bool selected)
    {
        if (!Admitted()) return;
        if(Draft.Selected==selected)return;
        CloseMenu(); Draft.Select(selected); segments.Clear(); candidateSegment=null; routePublicationDirty=true; previewHex = null; Refresh();
    }
    private TransportTargetMenu ReadMenu(int hex) => rules.QueryTransportTargetMenu(world, source,
        Draft.Selection.OfficerId, Draft.Selection.NavalFormationId, hex,
        new(Draft.Selected, Draft.Selected ? Draft.ActiveWaypointIndex : null,
            segments.Select(segment => segment.SegmentStatus).ToArray()));
    internal void PickTarget(int hex, bool waypointShortcut = false)
    {
        if (!AllowPointer(hex) || !Draft.Selected) return;
        CloseMenu(); var publication = ReadMenu(hex); previewHex = publication.TargetHexId ?? hex; RefreshPoints();
        if (publication.CanOpen is null) { unavailable(publication.UnresolvedInput ?? "transport.target.menu"); return; }
        if (publication.CanOpen != true) return;
        if (waypointShortcut)
        {
            if (publication.Options.FirstOrDefault(option => option.ActionKey == "waypoint") is { Included: true, Enabled: true } &&
                publication.TargetHexId is int point) Append(point);
            return;
        }
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var entries = publication.Options.Where(option => option.Included == true).Select(option =>
            new ArmyCommandMenuEntry(option.ActionKey, data.Records[option.CaptionDataIndex].Text
                ?? throw new InvalidDataException("Missing transport command caption."),
                option.ActionKey == "move" ? "000_cmn_icon_bt_kidou" : option.ActionKey == "enter" ? "000_cmn_icon_bt_nyujo" : null,
                Hint(option), option.Enabled == true && option.PresentationUnresolvedInput is null,
                () =>
                {
                    if (!Admitted() || publication.TargetHexId is not int target) return;
                    CloseMenu();
                    if (option.ActionKey == "waypoint") { Append(target); return; }
                    if (option.Action is not { } action) return;
                    var route = March(target);
                    savedSegments=segments.Append(route).ToArray();
                    int? days = null;
                    if (savedSegments.All(segment => segment.Status == DeploymentDraftRouteStatus.Found && segment.Path is not null))
                    {
                        //Each source segment omits exactly its last point;
                        //integrate the assembled list once, never sum rounded days.
                        var etaPath = savedSegments.SelectMany(segment =>
                        {
                            var path = segment.Path!;
                            return path.Take(path.Count - 1);
                        }).ToArray();
                        var eta = routeQuery.QueryEta(etaPath);
                        days = eta.Days;
                        if (eta.UnresolvedInput is { } missing) unavailable(missing);
                    }
                    Draft.Assign(action, target, days);
                    segments.Clear();candidateSegment=null;routePublicationDirty=true; previewHex = null; Refresh();
                }, option.WaypointOrdinal)).ToArray();
        if (entries.Length == 0) return;
        var popup = new ArmyCommandMenu(); menu = popup; view.AddMapMenu(popup);
        popup.Configure(repository, text, entries, value => { if (!disposed && menu == popup) view.ApplyHint(value); },resources:view.SourceResources);
        popup.TrackAnchor(() => current() && publication.TargetHexId is int target ? project(target) : null,
            PositionOptions, view.MenuFooterTop, policyAvoidanceHeight: 160);
    }
    private string? Hint(UnitTargetMenuOption option)
    {
        var record = repository.LoadMessages(text.SourceLocale, MessageKind.Game)
            .Messages.Sections[option.TooltipMessageId / 10000].Records[option.TooltipMessageId % 10000];
        return record.Literals.Count == 1 ? record.Literals[0].Text : null;
    }
    private TransportDraftMarch March(int target)
    {
        int start = Draft.Waypoints.LastOrDefault(Draft.CurrentHex);
        if(candidateSegment is {} existing&&existing.Start==start&&existing.Goal==target)return existing.Route;
        var route=routeQuery.QuerySegment(start,target);
        candidateSegment=(start,target,route);routePublicationDirty=true;return route;
    }
    private void Append(int target)
    {
        if (Draft.ActiveWaypointIndex >= 5) return;
        var route = March(target);
        if (!Draft.Append(target)) return;
        segments.Add(route);candidateSegment=null;routePublicationDirty=true; previewHex = target; Refresh();
        if (route.Status == DeploymentDraftRouteStatus.Unresolved)
            unavailable(route.UnresolvedInput ?? "transport.target.segment");
    }
    private void Refresh()
    {
        if (menu is null) RestoreOptions();
        view.ApplySelection(Draft.Selected ? [Draft.Selection.OfficerId] : []);
        view.ApplyProgress([new(Draft.Selection.OfficerId, Draft.Selected ? 0 : Draft.EstimatedDays, Draft.CanDecide)]);
        view.ApplyDecision(Draft.CanDecide, RequestDecision); RefreshPoints(); RefreshOptions();
    }
    private void RefreshPoints()
    {
        view.ApplySourceWaypoints(Draft.Waypoints,Draft.Selected?previewHex:null);
        if(Draft.Selected&&previewHex is int target)March(target);
        if(routePublicationDirty)
        {
            var paths=new List<MapRoutePreviewPath>();
            foreach(var route in savedSegments)paths.Add(PreviewPath(route,1));
            if(Draft.Selected)
            {
                foreach(var route in segments)paths.Add(PreviewPath(route,0));
                if(candidateSegment is {} tail)paths.Add(PreviewPath(tail.Route,0));
            }
            routePublication=paths.AsReadOnly();routePublicationDirty=false;
        }
        routePreviewChanged?.Invoke(routePublication);
    }
    private static MapRoutePreviewPath PreviewPath(TransportDraftMarch route,int bank)=>
        new(route,route.Status,route.Path,route.StartSurfaceCode,route.EndSurfaceCode,bank,0,
            route.PresentationUnresolvedInput??route.UnresolvedInput)
        {ControlStartHex=route.ControlStartHex,ControlGoalHex=route.ControlGoalHex,
            ControlStartSurfaceCode=route.ControlStartSurfaceCode,ControlGoalSurfaceCode=route.ControlGoalSurfaceCode};
    private void RefreshOptions()
    {
        int? behavior = TransportSetupCurrentInputs.Read(world, TransportSetupCurrentInputs.BehaviorPreferenceKey);
        int? returns = TransportSetupCurrentInputs.Read(world, TransportSetupCurrentInputs.ReturnOfficerPreferenceKey);
        bool canSave = saveDefaults is not null && Draft.SourceBehaviorChoice is 1 or 2 && Draft.ReturnOfficerToSource.HasValue &&
            behavior.HasValue && returns.HasValue && (behavior != Draft.SourceBehaviorChoice || (returns == 1) != Draft.ReturnOfficerToSource);
        options.Apply(Draft.Selected, Draft.SourceBehaviorChoice, Draft.ReturnOfficerToSource, canSave);
    }
    private void ChangeOption(TransportTargetOption option, int value)
    {
        if (!Admitted() || !Draft.Selected) return;
        if (option == TransportTargetOption.BehaviorChoice) Draft.ChangeSourceBehavior(value);
        else Draft.ChangeReturnOfficer(value == 1);
        RefreshOptions(); view.ApplyDecision(Draft.CanDecide, RequestDecision);
    }
    private void SaveDefaults()
    {
        if (!Admitted() || !Draft.Selected || saveDefaults is null || Draft.ReturnOfficerToSource is not bool returns ||
            Draft.SourceBehaviorChoice is not int behavior || behavior is < 1 or > 2) return;
        if (saveDefaults(returns, behavior, replacement =>
            {world=replacement;routeQuery=rules.CreateTransportDraftRouteQuery(world,source,Draft.Selection.OfficerId,Draft.Selection.Troops,Draft.Selection.NavalFormationId);})) RefreshOptions();
    }
    internal void AcceptPresentationWorld(GameState previous, GameState replacement)
    {
        if (disposed || !ReferenceEquals(world, previous))
            throw new InvalidOperationException("Transport Target belongs to another world.");
        world = replacement;
        routeQuery = rules.CreateTransportDraftRouteQuery(world, source, Draft.Selection.OfficerId,
            Draft.Selection.Troops, Draft.Selection.NavalFormationId);
        // Keep the current menu, confirmation, edited options, waypoints and
        // published route segments. Pure speech does not change those facts.
    }
    internal void HoverTarget(int? hex)
    {
        if (!Admitted() || !Draft.Selected || menu is not null || hex is null || previewHex == hex) return;
        previewHex = hex; RefreshPoints();
    }
    private void RequestDecision()
    { if (Admitted() && Draft.CanDecide) Confirm(DeploymentTargetConfirmationKind.Orders, () => accepted(Draft.CreateOrder(force, source))); }
    private void RequestBack()
    {
        if (!Admitted()) return;
        if (Draft.Action is not null) Confirm(DeploymentTargetConfirmationKind.Discard, back); else back();
    }
    private void Confirm(DeploymentTargetConfirmationKind kind, Action yes)
    {
        CloseMenu(); var modal = new DeploymentTargetConfirmation(); confirmation = modal; view.AddChild(modal); modalChanged(true);
        try { modal.Configure(repository, text, kind, result =>
        {
            if (disposed || confirmation != modal) return;
            confirmation = null; modal.Hide(); modal.QueueFree(); modalChanged(false); if (result && Admitted()) yes();
        },resources:view.SourceResources); }
        catch { confirmation = null; modal.QueueFree(); modalChanged(false); throw; }
    }
    internal bool TryCancelOverlay()
    {
        if (confirmation is { } modal) { modal.RequestCancel(); return true; }
        if (menu is not null) { CloseMenu(); return true; }
        if (Draft.Undo()) { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);candidateSegment=null;routePublicationDirty=true; Refresh(); return true; }
        return false;
    }
    private void PositionOptions(Vector2? position)
    { options.Visible = position.HasValue && Draft.Selected; if (position is Vector2 point) options.Position = point; }
    private void RestoreOptions() { options.Position = new(1510, 510); options.Visible = Draft.Selected; }
    private void CloseMenu()
    {
        var previous = menu; menu = null;
        if (previous is not null) { previous.Abandon(); previous.GetParent()?.RemoveChild(previous); previous.QueueFree(); }
        if (!disposed) RestoreOptions(); view.ApplyHint(null);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; CloseMenu(); options.Abandon();
        routePreviewChanged?.Invoke(Array.Empty<MapRoutePreviewPath>());
        if (confirmation is { } modal) { confirmation = null; modal.Abandon(); modal.QueueFree(); modalChanged(false); }
    }
}
