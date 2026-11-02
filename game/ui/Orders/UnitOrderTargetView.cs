using Godot;
using San14.Data;
using San14.Core;
using San14.Client.Gameplay;

internal enum UnitMapOrder { Stay, Withdraw, Enter, Attack, ChangeBehavior, ChangeWaypoints, Build, Repair, Demolish }

/// <summary>Map targeting with the original existing-army command menu,
/// policy panels and footer. The legacy non-native editor remains separate.</summary>
internal partial class UnitOrderTargetView : Control
{
    private AdministrationPageChrome? chrome;
    private UiSourceText? selected;
    private readonly List<BaseButton> actions = [];
    private OptionButton? entryChoice, attackChoice;
    private BaseButton? enter, attack;
    private DeploymentPolicyView policies = null!;
    private GameResourceRepository? sourceRepository;
    private TransportTargetOptionsView? transportPolicies;
    private bool sourceTransportMode, transportHasSelection, transportCanSave, facilitySelectionMode;
    private int? transportRaw41;
    private bool? transportReturnOfficer;
    private Vector2? sourcePolicyPosition = new Vector2(1510, 510);
    private Action<TransportTargetOption, int>? transportPolicyChanged;
    private Action? transportDefaultsRequested;
    private UnitFacilityOrdersView? facilityOrders;
    private DeploymentPolicyOptions policyOptions = null!;
    private DeploymentBehavior? behavior, initialBehavior;
    private readonly List<int> waypoints = [];
    private ItemList route = null!;
    private UiSourceText routeCount = null!;
    private BaseButton addWaypoint = null!, removeWaypoint = null!, clearWaypoints = null!, applyRoute = null!, applyBehavior = null!;
    private PreviewText ui = null!;
    private int? selectedHex;
    private UnitWaypointMarkerLayer? sourceRoute;
    private UnitSelectionBoxView? sourceSelectionBox;
    private bool canEditRoute, busy, abandoned, existingArmyMode;
    private Action? policyChanged;
    private DeploymentBehavior?[]? selectionBehaviors;
    private Action<DeploymentPolicySetting, bool>? selectionPolicyChanged;
    private bool sourceSingle = true, sourceFacilitiesAvailable = true;
    private bool sourceDetailsAvailable = true;
    private BaseButton? sourceDetails;
    private Action? detailsRequested;
    private BaseButton? sourceWithdraw;
    private Action? withdrawRequested;
    private bool canWithdraw;
    private BaseButton? sourceFacilities;
    private Action? facilitiesRequested;
    internal int[] Waypoints => waypoints.ToArray();
    internal DeploymentBehavior? Behavior => behavior;
    internal string? FacilityDefinitionKey => facilityOrders?.DefinitionKey;
    internal string? FacilityDefinitionName => facilityOrders?.DefinitionName;
    internal bool SourceTransportPoliciesActive => existingArmyMode && sourceTransportMode;
    internal int SourcePolicyAvoidanceHeight => SourceTransportPoliciesActive ? 160 : 240;
    internal void Configure(GameResourceRepository repository, PreviewText text, string unit, string destination,
        Action returned, Action accepted, MilitaryCommandView commands, Action<UnitMapOrder, DeploymentTarget?> requested,
        Theme commandTheme, DeploymentPolicyOptions policyOptions, DeploymentBehavior? behavior,
        IReadOnlyList<FacilityConstructionChoice> construction, IReadOnlyList<UnitFacilityTarget> facilities,
        Action constructionChanged, Func<int, int?> readFacilityCapacity, bool existingArmyMode = false,
        AdministrationPageChrome.Resources? resources = null)
    {
        ui = text; sourceRepository = repository; this.policyOptions = policyOptions; this.behavior = initialBehavior = behavior;
        this.existingArmyMode = existingArmyMode;
        if (existingArmyMode) Theme = commandTheme;
        policyChanged = () => requested(UnitMapOrder.ChangeBehavior, null);
        Name = "UnitOrderTarget"; ZIndex = 3; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, existingArmyMode ? 0x33A : 0x223, returned, accepted,
            returnOnly: existingArmyMode, compactReturn: existingArmyMode, resources: resources);
        if (!existingArmyMode) chrome.ApplyTitle(text.Get("play_unit_orders_title"));
        chrome.ApplyDefaultHint(text.Get("play_unit_target_hint"));
        var narration = chrome.Assets.SourceLayout(0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(narration.Nodes.Single(node => node.Type == 2),
            narration.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        if (existingArmyMode)
        {
            var footer = chrome.Assets.SourceLayout(0xD8913AB2);
            var captions = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
            foreach (var entry in new[] { (18, 0x35D, "UnitSourceDetails"), (17, 0x25A, "UnitSourceWithdraw"), (14, 0x262, "UnitSourceFacilityReference") })
            {
                var button = new UiSourceBottomButton { Name = entry.Item3, Position = footer.Rectangle(entry.Item1, new(0, 904)).Position };
                chrome.Canvas.AddChild(button);
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                    captions.Records[entry.Item2].Text ?? throw new InvalidDataException("Missing unit footer caption."),
                    () =>
                    {
                        if (busy || abandoned) return;
                        if (entry.Item1 == 18 && sourceDetailsAvailable) detailsRequested?.Invoke();
                        else if (entry.Item1 == 17 && canWithdraw) withdrawRequested?.Invoke();
                        else if (entry.Item1 == 14 && !sourceTransportMode && sourceFacilitiesAvailable) facilitiesRequested?.Invoke();
                    });
                // A secondary action becomes available only after the caller
                // attaches its actual consumer. Facility reference is pending.
                button.Disabled = true;
                if (entry.Item1 == 18) sourceDetails = button;
                if (entry.Item1 == 17) sourceWithdraw = button;
                if (entry.Item1 == 14) sourceFacilities = button;
            }
            ConfigureSourceWithdrawalTooltip();
            policies = new DeploymentPolicyView(); chrome.Canvas.AddChild(policies);
            policies.Configure(repository, text, ChangePolicy, () => { }, showSaveDefaults: false, resources: SourceResources);
            policies.Position = new(1510, 510);
            SetBusy(false);
            return;
        }
        Line("UnitCaption", unit, 110);
        Line("CurrentDestination", text.Format("play_unit_target_current", destination), 148);
        selected = Line("SelectedDestination", "", 186);
        Command("HoldUnitPosition", "play_military_stay_action", new(320, 235), () => requested(UnitMapOrder.Stay, null));
        Command("WithdrawMapUnit", "play_withdraw", new(500, 235), () => requested(UnitMapOrder.Withdraw, null));
        entryChoice = Choice("UnitEntryTarget", new(320, 330));
        foreach (var entry in commands.EntryTargets) entryChoice.AddItem(entry.Name);
        enter = Command("EnterMapUnit", "play_military_enter", new(650, 324), () =>
        {
            if (entryChoice.Selected >= 0)
                requested(UnitMapOrder.Enter, new(DeploymentTargetKind.Location, commands.EntryTargets[entryChoice.Selected].Id));
        });
        attackChoice = Choice("UnitAttackTarget", new(320, 420));
        foreach (var target in commands.AttackTargets) attackChoice.AddItem(target.Name);
        attack = Command("AttackMapUnit", "play_military_attack", new(650, 414), () =>
        {
            if (attackChoice.Selected >= 0) requested(UnitMapOrder.Attack, commands.AttackTargets[attackChoice.Selected].Target);
        });
        routeCount = Line("UnitRouteCaption", "", 520);
        route = new ItemList { Name = "UnitRoute", Position = new(320, 560), Size = new(500, 120), Theme = commandTheme };
        chrome.Canvas.AddChild(route);
        canEditRoute = commands.FocusedUnit?.Plan is { Completed: false };
        if (canEditRoute && commands.FocusedUnit?.Plan is { } plan)
            waypoints.AddRange(plan.Waypoints.Skip(plan.NextWaypointIndex));
        route.ItemSelected += _ => RefreshEditors();
        addWaypoint = Command("AddUnitWaypoint", "play_military_add_selected_hex", new(320, 700), () =>
        {
            if (selectedHex is int hex && waypoints.Count < PlayableWaypointMarch.MaximumWaypoints)
            { waypoints.Add(hex); RenderRoute(); }
        });
        removeWaypoint = Command("RemoveUnitWaypoint", "play_military_remove", new(500, 700), () =>
        {
            int index = route.GetSelectedItems().FirstOrDefault(-1);
            if ((uint)index < (uint)waypoints.Count)
            { waypoints.RemoveAt(index); RenderRoute(); }
        });
        clearWaypoints = Command("ClearUnitWaypoints", "play_military_clear", new(680, 700), () =>
        { waypoints.Clear(); RenderRoute(); });
        applyRoute = Command("ApplyUnitRoute", "play_military_apply_waypoints", new(860, 700), () => requested(UnitMapOrder.ChangeWaypoints, null));
        policies = new DeploymentPolicyView(); chrome.Canvas.AddChild(policies);
        policies.Configure(repository, text, ChangePolicy, () => { }, showSaveDefaults: false, resources: SourceResources);
        policies.Position = new(1120, 110);
        applyBehavior = Command("ApplyUnitBehavior", "play_military_apply_behavior", policies.Position + new Vector2(0, policies.Size.Y + 8),
            () => requested(UnitMapOrder.ChangeBehavior, null));
        facilityOrders = new UnitFacilityOrdersView(); chrome.Canvas.AddChild(facilityOrders);
        facilityOrders.Configure(repository, text, chrome, profile, commandTheme, construction, facilities, constructionChanged, readFacilityCapacity, requested);
        RenderRoute();
        SetBusy(false);

        UiSourceText Line(string name, string value, int y)
        {
            var line = new UiSourceText { Name = name, Position = new(320, y), Size = new(650, 32) };
            chrome.Canvas.AddChild(line); line.Configure(chrome.Font, profile); line.Text = value;
            return line;
        }
        BaseButton Command(string name, string caption, Vector2 position, Action action)
        {
            var button = new UiSourceBottomButton { Name = name, Position = position };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text.Get(caption), () =>
            { if (!abandoned) action(); });
            actions.Add(button); return button;
        }
        OptionButton Choice(string name, Vector2 position)
        {
            var choice = new OptionButton { Name = name, Position = position, Size = new(310, 36),
                Theme = commandTheme };
            chrome.Canvas.AddChild(choice); return choice;
        }
    }
    internal void ApplyTarget(int hex, string caption)
    { selectedHex = hex; if (selected is not null) selected.Text = caption; if (existingArmyMode) facilityOrders?.Hide(); chrome?.ApplyDecision(!busy, true); RefreshEditors(); }
    internal void ConfigureSourceDetails(Action requested)
    { detailsRequested = requested; RefreshSourceActions(); }
    internal void ConfigureSourceWithdraw(bool allowed, Action requested)
    { canWithdraw = allowed; withdrawRequested = requested; RefreshSourceActions(); }
    internal void ConfigureSourceFacilities(Action requested)
    { facilitiesRequested = requested; RefreshSourceActions(); }
    internal void ApplySourceSelection(bool single, bool canWithdraw, bool facilitiesAvailable, bool detailsAvailable = true)
    {
        if (abandoned || !existingArmyMode) return;
        sourceSingle = single; this.canWithdraw = canWithdraw; sourceFacilitiesAvailable = facilitiesAvailable;
        sourceDetailsAvailable = detailsAvailable;
        RefreshSourceActions();
    }
    internal void ConfigureSourceSelectionPolicies(DeploymentPolicyOptions options,
        IReadOnlyList<DeploymentBehavior?> behaviors, Action<DeploymentPolicySetting, bool> changed)
    {
        if (abandoned || !existingArmyMode) return;
        sourceTransportMode = false;
        policyOptions = options; selectionBehaviors = behaviors.ToArray(); selectionPolicyChanged = changed;
        RefreshEditors(); RefreshSourcePolicyVisibility(); RefreshSourceActions();
    }
    internal void ConfigureSourceTransportPolicies(bool hasSelection, int? raw41, bool? returnOfficer, bool canSave,
        Action<TransportTargetOption, int> changed, Action saveDefaults)
    {
        if (abandoned || !existingArmyMode) return;
        ArgumentNullException.ThrowIfNull(changed); ArgumentNullException.ThrowIfNull(saveDefaults);
        if (transportPolicies is null)
        {
            var panel = new TransportTargetOptionsView(); chrome!.Canvas.AddChild(panel);
            try
            {
                panel.Configure(sourceRepository!, ui, (option, value) =>
                {
                    if (TransportInputAdmitted()) transportPolicyChanged?.Invoke(option, value);
                }, () =>
                {
                    if (TransportInputAdmitted() && transportCanSave) transportDefaultsRequested?.Invoke();
                }, resources: SourceResources);
                panel.InputAdmission = TransportInputAdmitted;
                transportPolicies = panel;
            }
            catch
            {
                panel.Abandon(); panel.GetParent()?.RemoveChild(panel); panel.QueueFree(); throw;
            }
        }
        transportPolicyChanged = changed; transportDefaultsRequested = saveDefaults;
        ApplySourceTransportPolicies(hasSelection, raw41, returnOfficer, canSave);
    }
    internal void ApplySourceTransportPolicies(bool hasSelection, int? raw41, bool? returnOfficer, bool canSave)
    {
        if (abandoned || !existingArmyMode) return;
        if (transportPolicies is null) throw new InvalidOperationException("Transport policies are not configured.");
        if (raw41 is int value && value is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(raw41));
        sourceTransportMode = true; transportHasSelection = hasSelection; transportRaw41 = raw41;
        transportReturnOfficer = returnOfficer; transportCanSave = canSave;
        RefreshEditors(); RefreshSourcePolicyVisibility(); RefreshSourceActions();
    }
    private bool TransportInputAdmitted() => !abandoned && !busy && !facilitySelectionMode &&
        existingArmyMode && sourceTransportMode && transportHasSelection;
    private void RefreshSourceActions()
    {
        if (sourceDetails is not null) sourceDetails.Disabled = busy || abandoned || !sourceDetailsAvailable || detailsRequested is null;
        if (sourceWithdraw is not null) sourceWithdraw.Disabled = busy || abandoned || !canWithdraw || withdrawRequested is null;
        if (sourceFacilities is not null) sourceFacilities.Disabled = busy || abandoned || sourceTransportMode || !sourceFacilitiesAvailable || facilitiesRequested is null;
    }
    internal void SetFacilitySelectionMode(bool active, bool placing)
    {
        facilitySelectionMode = active; SetBusy(active);
        if (active) RefreshSourcePolicyVisibility(); else RestoreSourcePolicyPosition();
        if (sourceRoute is not null) sourceRoute.Visible = !active;
        facilityOrders?.Hide();
        if (active) chrome?.ApplyReturn(placing);
    }
    internal AdministrationPageChrome.Resources SourceResources =>
        new(chrome!.Assets, chrome.Font, chrome.SpriteShader);
    internal int MenuFooterTop => chrome!.MenuFooterTop;
    internal void AddSourceMenu(ArmyCommandMenu menu) => chrome!.Canvas.AddChild(menu);
    internal void ConfigureSourceRoute(GameResourceRepository repository, Func<int, Vector2?> project)
    {
        sourceRoute = new(); chrome!.Canvas.AddChild(sourceRoute);
        chrome.Canvas.MoveChild(sourceRoute, 0);
        sourceRoute.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, project);
    }
    internal UnitSelectionBoxView ConfigureSourceSelectionBox(GameResourceRepository repository)
    {
        if (abandoned || sourceSelectionBox is not null)
            throw new InvalidOperationException("Selection box is already configured or retired.");
        sourceSelectionBox = new(); chrome!.Canvas.AddChild(sourceSelectionBox);
        chrome.Canvas.MoveChild(sourceSelectionBox, 0);
        sourceSelectionBox.Configure(repository, chrome.Assets, chrome.SpriteShader);
        return sourceSelectionBox;
    }
    internal void ApplySourceWaypoints(IReadOnlyList<int> points, int? preview = null)
    {
        sourceRoute?.Apply(points, preview);
        RefreshEditors();
    }
    internal void FocusConstruction() { facilityOrders?.Show(); facilityOrders?.FocusConstruction(); }
    internal void ApplySourcePolicyPosition(Vector2? point)
    {
        if (!existingArmyMode) return;
        sourcePolicyPosition = point; RefreshSourcePolicyVisibility();
    }
    internal void RestoreSourcePolicyPosition()
    { if (existingArmyMode) { sourcePolicyPosition = new Vector2(1510, 510); RefreshSourcePolicyVisibility(); } }
    private void RefreshSourcePolicyVisibility()
    {
        if (!existingArmyMode) return;
        bool visible = !abandoned && !facilitySelectionMode && sourcePolicyPosition.HasValue;
        if (policies is not null)
        {
            policies.Visible = visible && !sourceTransportMode;
            if (sourcePolicyPosition is Vector2 position) policies.Position = position;
        }
        if (transportPolicies is not null)
        {
            transportPolicies.Visible = visible && sourceTransportMode;
            if (sourcePolicyPosition is Vector2 position) transportPolicies.Position = position;
        }
    }
    internal void ApplyCommittedBehavior(DeploymentBehavior? value)
    { behavior = initialBehavior = value; RefreshEditors(); }
    internal void ApplyHint(string text) => chrome?.ApplyDefaultHint(text);
    internal void ApplyConstructionQuote(FacilityConstructionQuote? quote, string? payer) => facilityOrders?.ApplyQuote(quote, payer);
    internal void SetBusy(bool value)
    {
        busy = value;
        facilityOrders?.SetBusy(value);
        chrome?.ApplyReturn(!value); chrome?.ApplyDecision(!value && selected?.Text.Length > 0, true);
        foreach (var button in actions) button.Disabled = value;
        RefreshSourceActions();
        if (entryChoice is not null) { entryChoice.Disabled = value || entryChoice.ItemCount == 0; if (enter is not null) enter.Disabled = entryChoice.Disabled; }
        if (attackChoice is not null) { attackChoice.Disabled = value || attackChoice.ItemCount == 0; if (attack is not null) attack.Disabled = attackChoice.Disabled; }
        RefreshEditors();
        RefreshWithdrawalTooltip();
    }
    private bool? SelectedPolicy(Func<DeploymentBehavior, bool> read)
    {
        if (selectionBehaviors is null) return behavior is null ? null : read(behavior);
        if (selectionBehaviors.Length == 0 || selectionBehaviors[0] is not { } first) return null;
        bool selected = read(first);
        foreach (var value in selectionBehaviors)
            if (value is null || read(value) != selected) return null;
        return selected;
    }
    private DeploymentPolicyPair[] PolicyPairs() =>
    [new(SelectedPolicy(value => value.AttackNearbyEnemies), policyOptions.AttackNearbyEnemiesAllowed, policyOptions.IgnoreNearbyEnemiesAllowed),
     new(SelectedPolicy(value => value.AllowRetreat), policyOptions.RetreatAllowed, policyOptions.RetreatForbiddenAllowed),
     new(SelectedPolicy(value => value.PursueRetreatingEnemies), policyOptions.PursuitAllowed, policyOptions.PursuitForbiddenAllowed),
     new(SelectedPolicy(value => value.AfterAction == DeploymentAfterAction.Retreat),
         policyOptions.RetreatAfterActionAllowed, policyOptions.StayAfterActionAllowed)];
    private void ChangePolicy(DeploymentPolicySetting setting, bool first)
    {
        if (busy || abandoned || existingArmyMode && sourceTransportMode || !policyOptions.Available) return;
        var pairs = PolicyPairs(); int index = (int)setting;
        if ((uint)index >= (uint)pairs.Length || (first ? pairs[index].FirstAllowed : pairs[index].SecondAllowed) != true) return;
        if (selectionBehaviors is not null)
        {
            if (selectionBehaviors.Length != 0 && policyOptions.HasSelection)
                selectionPolicyChanged?.Invoke(setting, first);
            return;
        }
        if (behavior is null) return;
        behavior = setting switch
        {
            DeploymentPolicySetting.EnemyApproach => behavior with { AttackNearbyEnemies = first },
            DeploymentPolicySetting.Retreat => behavior with { AllowRetreat = first },
            DeploymentPolicySetting.Pursuit => behavior with { PursueRetreatingEnemies = first },
            DeploymentPolicySetting.AfterAction => behavior with { AfterAction = first ? DeploymentAfterAction.Retreat : DeploymentAfterAction.Stay },
            _ => behavior
        };
        RefreshEditors();
        if (existingArmyMode) policyChanged?.Invoke();
    }
    private void RenderRoute()
    {
        route.Clear();
        for (int i = 0; i < waypoints.Count; i++) route.AddItem(ui.Format("play_military_waypoint", i + 1, waypoints[i]));
        routeCount.Text = ui.Format("play_military_waypoints", waypoints.Count, PlayableWaypointMarch.MaximumWaypoints);
        if (applyBehavior is not null) RefreshEditors();
    }
    private void RefreshEditors()
    {
        if (existingArmyMode)
        {
            if (policies is null) return;
            policies.Apply(!busy && !sourceTransportMode && (selectionBehaviors is null ? behavior is not null : selectionBehaviors.Length != 0) &&
                policyOptions.Available && policyOptions.HasSelection, PolicyPairs(), false);
            transportPolicies?.Apply(TransportInputAdmitted(), transportRaw41, transportReturnOfficer,
                !busy && transportCanSave);
            return;
        }
        if (applyBehavior is null) return;
        addWaypoint.Disabled = busy || selectedHex is null || waypoints.Count >= PlayableWaypointMarch.MaximumWaypoints;
        removeWaypoint.Disabled = busy || route.GetSelectedItems().Length == 0;
        clearWaypoints.Disabled = busy || waypoints.Count == 0;
        applyRoute.Disabled = busy || !canEditRoute;
        applyBehavior.Disabled = busy || behavior is null || behavior == initialBehavior || !policyOptions.Available || !policyOptions.HasSelection;
        policies.Apply(!busy && behavior is not null && policyOptions.Available && policyOptions.HasSelection, PolicyPairs(), false);
    }
    internal void Abandon() { abandoned = true; policyChanged = null; selectionPolicyChanged = null; selectionBehaviors = null; transportPolicyChanged = null; transportDefaultsRequested = null; detailsRequested = null; withdrawRequested = null; facilitiesRequested = null; SetBusy(true); sourceRoute?.Abandon(); sourceSelectionBox?.Abandon(); facilityOrders?.Abandon(); policies?.Abandon(); transportPolicies?.Abandon(); chrome?.Abandon(); Hide(); }
}
