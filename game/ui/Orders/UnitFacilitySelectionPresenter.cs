using Godot;
using San14.Core;
using San14.Data;

/// <summary>Owns definition selection and map placement for one current army.
/// Queries run on a definition/target change; only the host issues the order.</summary>
internal sealed class UnitFacilitySelectionPresenter : IDisposable
{
    private readonly UnitOrderTargetView parent;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private readonly GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int unitId;
    private readonly IReadOnlyList<UnitFacilitySelectionEntry> entries;
    private readonly IReadOnlyList<int> waypoints;
    private readonly Func<bool> current;
    private readonly Action<bool> modal;
    private readonly Func<FacilityConstructionChoice, int, IReadOnlyList<int>, bool> submit;
    private readonly Action closed;
    private readonly int? capturedTargetHex;
    private UnitFacilitySelectionView? page;
    private FacilityConstructionChoice? definition;
    private int? quotedHex;
    private FacilityConstructionQuote? quote;
    private bool disposed;

    internal UnitFacilitySelectionPresenter(UnitOrderTargetView parent, GameResourceRepository repository, PreviewText text,
        GameState world, ArmyCampaignRuleAdapter rules, int unitId, IReadOnlyList<UnitFacilitySelectionEntry> entries,
        IReadOnlyList<int> waypoints, Func<bool> current, Action<bool> modal,
        Func<FacilityConstructionChoice, int, IReadOnlyList<int>, bool> submit, Action closed, int? capturedTargetHex = null)
    {
        this.parent = parent; this.repository = repository; this.text = text; this.world = world;
        this.rules = rules; this.unitId = unitId; this.entries = entries; this.waypoints = waypoints.ToArray();
        this.current = current; this.modal = modal; this.submit = submit; this.closed = closed;
        this.capturedTargetHex = capturedTargetHex;
    }

    internal void Open()
    {
        if (disposed || !current()) return;
        definition = null; quotedHex = null; quote = null;
        parent.SetFacilitySelectionMode(true, placing: false); modal(true);
        var child = new UnitFacilitySelectionView(); page = child; parent.AddChild(child);
        child.Configure(repository, text, entries, choice =>
        {
            if (disposed || page != child || !current()) return;
            ClosePage();
            if (choice is null) { Dispose(); return; }
            definition = choice;
            parent.SetFacilitySelectionMode(true, placing: true); modal(false);
            parent.ApplyHint(choice.Name + "  " + text.Get("play_facility_pick_site"));
            if (capturedTargetHex is int target)
            {
                PickTarget(target);
                if (!disposed) { Open(); page?.ApplyHint(text.Get("play_facility_blocked")); }
            }
        });
    }

    internal bool PickTarget(int hex)
    {
        if (disposed) return false;
        if (page is not null) return true;
        if (definition is not { } selected) return false;
        if (!current()) return true;
        if (quotedHex != hex)
        {
            quote = rules.QueryFacilityConstructionQuote(world, unitId, hex, selected.DefinitionKey);
            quotedHex = hex;
        }
        if (quote?.Allowed == true)
        {
            if (!submit(selected, hex, waypoints) && !disposed)
                parent.ApplyHint(text.Get("play_facility_blocked"));
        }
        else parent.ApplyHint(text.Get(UnitFacilityOrdersView.QuoteStatus(quote, entries.Count > 0)));
        return true;
    }

    internal bool TryCancel()
    {
        if (disposed) return false;
        if (page is not null) { Dispose(); return true; }
        if (definition is not null) { Open(); return true; }
        return false;
    }

    private void ClosePage()
    {
        var old = page; page = null;
        if (old is null) return;
        old.Abandon(); old.GetParent()?.RemoveChild(old); old.QueueFree();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ClosePage(); definition = null;
        parent.SetFacilitySelectionMode(false, placing: false); modal(false); closed();
    }
}
