using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The page retains saved display routes independently of its
/// fresh active selection routes. Withdrawal commits the saved groups.</summary>
internal sealed partial class UnitTargetMenuPresenter : IDisposable
{
    private readonly UnitOrderTargetView view;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly Func<int, Vector2?> project;
    private readonly Func<bool> current;
    private readonly Action<UnitTargetMenuOption, IReadOnlyList<int>, int?, int> requested;
    private readonly bool constructionAvailable;
    private readonly Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged;
    private UnitDraftRouteQuery routeQuery;
    internal UnitSelectionDraft Selection { get; } = new();
    internal IReadOnlyList<int> SelectedUnitIds => Selection.UnitIds;
    internal IReadOnlyList<ArmyWithdrawalSavedGroup> WithdrawalSavedGroups => Selection.SavedGroups
        .Select(group => new ArmyWithdrawalSavedGroup(group.OrderedUnitIds, group.PhysicalPoints,
            group.ActiveWaypointIndex, group.HasFinalTarget)).ToArray();
    internal UnitSelectionPartition? SelectedPartition => Selection.Partition;
    private ArmyCommandMenu? menu;
    private UnitsTargetMenu? publication;
    private int? selectedHex;
    private int? previewHex;
    private bool disposed;
    private UnitTargetMenuMessages? sourceMessages;
    internal bool IsTargetMenuOpen=>!disposed&&current()&&menu is { } popup&&
        !popup.IsQueuedForDeletion()&&popup.IsVisibleInTree();
    internal int? CurrentMenuHexId=>IsTargetMenuOpen?publication?.TargetHexId:null;
    internal IReadOnlyList<int> Waypoints => Selection.Waypoints;

    internal UnitTargetMenuPresenter(UnitOrderTargetView view, GameResourceRepository repository, PreviewText text,
        GameState world, ArmyCampaignRuleAdapter rules, int unitId, Func<int, Vector2?> project,
        Func<int, Vector2?> projectMarker, Func<bool> current,
        bool constructionAvailable, Action<UnitTargetMenuOption, IReadOnlyList<int>, int?, int> requested,
        Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged=null)
    {
        this.view = view; this.repository = repository; this.text = text; this.world = world; this.rules = rules;
        this.routePreviewChanged=routePreviewChanged;routeQuery=rules.CreateUnitDraftRouteQuery(world);
        InitializeSelection(unitId); this.project = project; this.current = current;
        this.constructionAvailable = constructionAvailable; this.requested = requested;
        view.ConfigureSourceRoute(repository, projectMarker); RefreshPoints();
    }

    internal void PickTarget(int hex, bool waypointShortcut = false)
    {
        if (disposed || !current() || Selection.SelectedCount == 0 || selectionDragging) return;
        selectedHex = hex; CloseMenu(); previewHex = hex;routePublicationDirty=true; RefreshPoints();
        // This is the genuinely constructed active singleton group, not the
        // old saved plan: each source insertion increments this local index.
        publication = ReadTargetMenu(hex);
        if (publication.CanOpen != true)
        {
            if (SelectedPartition == UnitSelectionPartition.Transport && publication.CanOpen is null)
                view.ApplyHint(text.Get("play_transport_setup_unavailable"));
            return;
        }
        if (waypointShortcut)
        {
            var waypoint = publication.Options.FirstOrDefault(option => option.ActionKey == "waypoint");
            if (waypoint is { Included: true, Enabled: true, PresentationUnresolvedInput: null } &&
                publication.TargetHexId is int point && AppendWaypoint(point)) RefreshPoints();
            return;
        }
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var entries = new List<ArmyCommandMenuEntry>();
        foreach (var option in publication.Options.Where(option => option.Included == true))
        {
            var captionBank = option.ActionKey == "capture"
                ? repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[0] : captions;
            string caption = captionBank.Records[option.CaptionDataIndex].Text ?? throw new InvalidDataException("Missing unit command caption.");
            bool executable = option.Action is { } action &&
                (PlayableWaypointMarch.IsSupportedAction(action) || action.ActionKey == "fire" && SelectedPartition == UnitSelectionPartition.Ordinary ||
                    action is { ActionKey: "capture", Target.Kind: DeploymentTargetKind.Unit } && SelectedPartition == UnitSelectionPartition.Ordinary) ||
                option.ActionKey == "waypoint" || option.ActionKey == "build" && constructionAvailable && Selection.SelectedCount > 0 &&
                    SelectedPartition == UnitSelectionPartition.Ordinary;
            bool enabled = option.Enabled == true && executable && UnitTargetMenuMessages.AllowsPresentation(option);
            entries.Add(new(option.ActionKey, caption, Icon(option.ActionKey), Hint(option), enabled, () =>
            {
                if (disposed || !current()) return;
                if (option.ActionKey == "waypoint")
                {
                    if (publication.TargetHexId is not int point || !AppendWaypoint(point)) return; previewHex = point; RefreshPoints(); CloseMenu();
                }
                else
                {
                    var waypoints = Selection.Waypoints;
                    int? target = publication.TargetHexId;
                    int clicked = publication.ClickedHexId;
                    CloseMenu();
                    requested(option, waypoints, target, clicked);
                }
            }, option.WaypointOrdinal));
        }
        if (entries.Count == 0) return;
        var popup = new ArmyCommandMenu(); menu = popup; view.AddSourceMenu(popup);
        popup.Configure(repository, text, entries, hint => { if (!disposed && menu == popup) view.ApplyHint(hint ?? text.Get("play_unit_target_hint")); });
        popup.Name = "UnitTargetCommandMenu";
        popup.TrackAnchor(() => current() && publication.TargetHexId is int target ? project(target) : null,
            view.ApplySourcePolicyPosition,
            footerBoundary: view.MenuFooterTop, policyAvoidanceHeight: view.SourcePolicyAvoidanceHeight);
    }

    private string? Hint(UnitTargetMenuOption option)
    {
        if (!UnitTargetMenuMessages.AllowsPresentation(option)) return null;
        int global = option.TooltipMessageId;
        if (global == 0x56C1 && option.ActionKey == "capture")
            return (sourceMessages ??= new(repository, text.SourceLocale)).FalseReportHint(option.SuccessRate, text.Get("detail_unknown"));
        if (global == 0x56A9)
            return option.SuccessRate is int rate
                ? (sourceMessages ??= new(repository, text.SourceLocale)).FireHint(rate) : null;
        var record = repository.LoadMessages(text.SourceLocale, MessageKind.Game).Messages.Sections[global / 10000].Records[global % 10000];
        // Parameterized hints require their real inputs. A known static literal
        // can be displayed without inventing formatter arguments or a rate.
        return record.Literals.Count == 1 ? record.Literals[0].Text : null;
    }
    private static string? Icon(string key) => key switch
    {
        "move" or "stay" => "000_cmn_icon_bt_kidou",
        "enter" => "000_cmn_icon_bt_nyujo",
        "attack" => "000_cmn_icon_bt_battle",
        "build" or "repair" or "demolish" => "000_cmn_icon_bt_build",
        "occupy" => "000_cmn_icon_bt_senryo",
        "fire" => "000_cmn_icon_bt_kakei",
        _ => null
    };
    private void UndoWaypoint()
    {
        if (disposed || !current() || !Selection.Undo()) return;
        foreach (var segments in transportSegments.Values)
            if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
        candidateRoutes.Clear();routePublicationDirty=true;
        RefreshPoints();
        CloseMenu();
    }
    private void RefreshPoints()
    {
        view.ApplySourceWaypoints(Selection.Waypoints,previewHex);
        PublishRoutes();
    }
    internal void HoverTarget(int? hex)
    {
        // The scene may retain/re-publish a candidate while a menu or modal
        // owns input. Re-admit it on return; a missing ray leaves the source
        // binding intact, rather than clearing the tail marker.
        if (disposed || !current() || selectionDragging || menu is not null || hex is null || previewHex == hex) return;
        previewHex = hex;routePublicationDirty=true; RefreshPoints();
    }
    internal void AcceptPolicyWorld(GameState previous, GameState replacement)
    {
        if (disposed || !ReferenceEquals(world, previous)) return;
        world = replacement;
        routeQuery=rules.CreateUnitDraftRouteQuery(replacement);
        transportSegments.Clear();candidateRoutes.Clear();routePublicationDirty=true;
    }
    internal void RefreshSelectedTarget()
    { if (selectedHex is int hex && menu is not null) PickTarget(hex); }
    internal bool TryCancelOverlay()
    {
        if (disposed || !current()) return false;
        if (menu is not null) { CloseMenu(); return true; }
        if (Selection.FirstWaypointIndex > 0) { UndoWaypoint(); return true; }
        return false;
    }
    private void CloseMenu()
    {
        var previous = menu; menu = null;
        if (previous is not null)
        {
            previous.Abandon();
            previous.GetParent()?.RemoveChild(previous);
            previous.QueueFree();
        }
        view.RestoreSourcePolicyPosition();
    }
    internal void SetBusy(bool value) => menu?.SetBusy(value);
    internal void CloseOverlay() => CloseMenu();
    public void Dispose() { if (disposed) return; disposed = true; ResetSelectionGesture(); CloseMenu();routePreviewChanged?.Invoke(Array.Empty<MapRoutePreviewPath>()); }
}
