using Godot;
using San14.Core;
using San14.Data;

internal sealed record UnitFacilityTarget(int Id, string Caption, int Durability);

/// <summary>Local facility selection for the unit page. Quotes are supplied only
/// after a selected hex or definition changes; this view never queries a world.</summary>
internal partial class UnitFacilityOrdersView : Control
{
    private OptionButton? definitions, targets;
    private BaseButton? build, repair, demolish;
    private UiSourceText cost = null!, payer = null!, qualification = null!, durability = null!;
    private IReadOnlyList<FacilityConstructionChoice> choices = [];
    private IReadOnlyList<UnitFacilityTarget> facilities = [];
    private PreviewText ui = null!;
    private FacilityConstructionQuote? quote;
    private Func<int, int?>? readCapacity;
    private int? repairCapacity;
    private bool busy, abandoned;

    internal string? DefinitionKey => definitions is { Selected: >= 0 } menu ? choices[menu.Selected].DefinitionKey : null;
    internal string? DefinitionName => definitions is { Selected: >= 0 } menu ? choices[menu.Selected].Name : null;

    internal void Configure(GameResourceRepository repository, PreviewText text, AdministrationPageChrome chrome,
        MapFontProfile profile, Theme theme, IReadOnlyList<FacilityConstructionChoice> choices,
        IReadOnlyList<UnitFacilityTarget> facilities, Action selectionChanged, Func<int, int?> readCapacity,
        Action<UnitMapOrder, DeploymentTarget?> requested)
    {
        Name = "UnitFacilityOrders"; Position = new(1120, 476); Size = new(680, 340);
        MouseFilter = MouseFilterEnum.Ignore;
        ui = text; this.choices = choices; this.facilities = facilities; this.readCapacity = readCapacity;
        Line("FacilityConstructionCaption", text.Get("play_facility_construction"), 0);
        definitions = Choice("UnitFacilityDefinition", 44);
        foreach (var choice in choices) definitions.AddItem(choice.Name);
        definitions.ItemSelected += _ => { if (!busy && !abandoned) selectionChanged(); };
        build = Command("BuildMapUnitFacility", "play_facility_build", new(330, 8), () =>
            requested(UnitMapOrder.Build, null));
        cost = Line("FacilityConstructionCost", "", 99);
        payer = Line("FacilityConstructionPayer", "", 135);
        qualification = Line("FacilityConstructionQualification", "", 171);
        Line("ExistingFacilityCaption", text.Get("play_facility_existing"), 219);
        targets = Choice("UnitFacilityTarget", 254);
        foreach (var target in facilities) targets.AddItem(target.Caption);
        targets.ItemSelected += _ => { if (!busy && !abandoned) SelectFacility(); };
        repair = Command("RepairMapUnitFacility", "play_facility_repair", new(330, 218), () => Request(UnitMapOrder.Repair));
        demolish = Command("DemolishMapUnitFacility", "play_facility_demolish", new(510, 218), () => Request(UnitMapOrder.Demolish));
        durability = Line("FacilityCurrentDurability", "", 308);
        ApplyQuote(null, null);
        SelectFacility();

        void Request(UnitMapOrder action)
        {
            if (targets.Selected >= 0)
                requested(action, new(DeploymentTargetKind.Structure, facilities[targets.Selected].Id));
        }
        UiSourceText Line(string name, string value, int y)
        {
            var line = new UiSourceText { Name = name, Position = new(0, y), Size = new(650, 32) };
            AddChild(line); line.Configure(chrome.Font, profile); line.Text = value; return line;
        }
        OptionButton Choice(string name, int y)
        {
            var choice = new OptionButton { Name = name, Position = new(0, y), Size = new(310, 36), Theme = theme };
            AddChild(choice); return choice;
        }
        BaseButton Command(string name, string caption, Vector2 position, Action action)
        {
            var button = new UiSourceBottomButton { Name = name, Position = position };
            AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text.Get(caption), () =>
            { if (!busy && !abandoned) action(); });
            return button;
        }
    }

    internal void ApplyQuote(FacilityConstructionQuote? value, string? payerName)
    {
        quote = value;
        cost.Text = value?.GoldCost is int gold ? ui.Format("play_facility_cost", gold) : "";
        payer.Text = value?.CurrentGold is int current && payerName is not null
            ? ui.Format("play_facility_payer", payerName, current) : "";
        qualification.Text = ui.Get(QuoteStatus(value, choices.Count > 0)); Refresh();
    }

    internal static string QuoteStatus(FacilityConstructionQuote? value, bool hasChoices) => value switch
        {
            null => hasChoices ? "play_facility_pick_site" : "play_facility_unavailable",
            { Allowed: true } => "play_facility_ready",
            { Allowed: null } => "play_facility_unavailable",
            _ => value.Reason switch
            {
                "structure.site.scenery" => "play_facility_blocked_scenery",
                "structure.site.terrain" => "play_facility_blocked_terrain",
                "structure.site.occupied" => "play_facility_blocked_occupied",
                "structure.site.object_relation" or "structure.site.land_relation" => "play_facility_blocked_land",
                "structure.site.area_capacity" => "play_facility_blocked_capacity",
                "structure.site.separation" => "play_facility_blocked_separation",
                "structure.site.commander_or_policy" => "play_facility_blocked_policy",
                _ => "play_facility_blocked"
            }
        };

    internal void SetBusy(bool value) { busy = value; Refresh(); }
    internal void FocusConstruction() { if (!busy && !abandoned) definitions?.GrabFocus(); }
    private void SelectFacility()
    {
        int index = targets?.Selected ?? -1;
        repairCapacity = (uint)index < (uint)facilities.Count ? readCapacity?.Invoke(facilities[index].Id) : null;
        durability.Text = (uint)index < (uint)facilities.Count
            ? ui.Format("play_facility_durability", facilities[index].Durability,
                repairCapacity?.ToString() ?? ui.Get("detail_unknown")) : "";
        Refresh();
    }
    private void Refresh()
    {
        bool disabled = busy || abandoned;
        if (definitions is not null) definitions.Disabled = disabled || choices.Count == 0;
        if (targets is not null) targets.Disabled = disabled || facilities.Count == 0;
        if (build is not null) build.Disabled = disabled || DefinitionKey is null || quote?.Allowed != true;
        int index = targets?.Selected ?? -1;
        bool hasTarget = (uint)index < (uint)facilities.Count;
        if (repair is not null) repair.Disabled = disabled || !hasTarget || repairCapacity is not int capacity || facilities[index].Durability >= capacity;
        if (demolish is not null) demolish.Disabled = disabled || !hasTarget;
    }
    internal void Abandon() { abandoned = true; readCapacity = null; SetBusy(true); Hide(); }
}
