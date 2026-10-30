using Godot;
using San14.Core;
using San14.Data;

/// <summary>Owns definition selection and map placement for the retained current army selection.
/// Queries run on a definition/target change; only the host issues the order.</summary>
internal sealed class UnitFacilitySelectionPresenter : IDisposable
{
    private readonly UnitOrderTargetView parent;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private readonly GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly IReadOnlyList<int> unitIds;
    private readonly DeploymentFacilitySelectionQuery selectionQuery;
    private readonly IReadOnlyList<UnitFacilitySelectionEntry> entries;
    private readonly IReadOnlyList<int> waypoints;
    private readonly Func<bool> current;
    private readonly Func<int> scrollSpeed;
    private readonly Action<bool> modal;
    private readonly Func<FacilityConstructionChoice, int, int?, IReadOnlyList<int>, bool> submit;
    private readonly Action closed;
    private readonly int? capturedTargetHex;
    private UnitFacilitySelectionView? page;
    private FacilityConstructionChoice? definition;
    private int? quotedHex;
    private FacilityConstructionQuote? quote;
    private int? initialHex;
    private bool disposed;
    internal bool IsFacilitySelectionOpen=>!disposed&&current()&&page is { } selection&&
        !selection.IsQueuedForDeletion()&&selection.IsVisibleInTree();
    internal int? CurrentChoiceDefinitionKey=>IsFacilitySelectionOpen?page?.CurrentChoiceDefinitionKey:null;
    internal bool FacilitySelectionActive=>!disposed&&current()&&page?.SelectionActive==true;
    internal string? SelectedFacilityDefinitionKey=>disposed?null:definition?.DefinitionKey;
    internal bool IsFacilityPlacementActive=>!disposed&&current()&&page is null&&definition is not null&&
        !parent.IsQueuedForDeletion()&&parent.IsVisibleInTree();
    internal long FacilityChoiceAcceptedSequence {get;private set;}
    internal string? LastAcceptedFacilityDefinitionKey {get;private set;}
    internal long ConstructionSubmittedSequence {get;private set;}
    internal string? LastSubmittedDefinitionKey {get;private set;}
    internal int? LastSubmittedHexId {get;private set;}
    internal int? LastSubmittedUnitId {get;private set;}
    internal IReadOnlyList<int> LastSubmittedUnitIds {get;private set;} = [];

    internal UnitFacilitySelectionPresenter(UnitOrderTargetView parent, GameResourceRepository repository, PreviewText text,
        GameState world, ArmyCampaignRuleAdapter rules, IReadOnlyList<int> unitIds,
        DeploymentFacilitySelectionQuery selectionQuery, IReadOnlyList<UnitFacilitySelectionEntry> entries,
        IReadOnlyList<int> waypoints, Func<bool> current, Action<bool> modal,
        Func<FacilityConstructionChoice, int, int?, IReadOnlyList<int>, bool> submit, Action closed, Func<int> scrollSpeed,int? capturedTargetHex = null)
    {
        this.parent = parent; this.repository = repository; this.text = text; this.world = world;
        this.rules = rules; this.unitIds = unitIds.ToArray(); this.selectionQuery = selectionQuery; this.entries = entries; this.waypoints = waypoints.ToArray();
        this.current = current; this.modal = modal; this.submit = submit; this.closed = closed;
        this.capturedTargetHex = capturedTargetHex;this.scrollSpeed=scrollSpeed;
    }

    internal void Open()
    {
        if (disposed || !current()) return;
        definition = null; initialHex=null; quotedHex = null; quote = null;
        parent.SetFacilitySelectionMode(true, placing: false); modal(true);
        var child = new UnitFacilitySelectionView(); page = child; parent.AddChild(child);
        child.Configure(repository, text, entries, choice =>
        {
            if (disposed || page != child || !current()) return;
            ClosePage();
            if (choice is null) { Dispose(); return; }
            definition = choice;
            parent.SetFacilitySelectionMode(true, placing: true); modal(false);
            parent.ApplyHint(rules.QueryFacilityPlacementHint(text.SourceLocale,choice.Name,false));
            LastAcceptedFacilityDefinitionKey=choice.DefinitionKey;FacilityChoiceAcceptedSequence++;
            if (capturedTargetHex is int target)
            {
                PickTarget(target);
                if (!disposed&&!initialHex.HasValue) { Open(); page?.ApplyHint(text.Get("play_facility_blocked")); }
            }
        },selectionQuery.PreselectedDefinitionKeys,selectionQuery.Directory.Pages,scrollSpeed);
    }

    internal bool PickTarget(int hex)
    {
        if (disposed) return false;
        if (page is not null) return true;
        if (definition is not { } selected) return false;
        if (!current()) return true;
        if (quotedHex != hex)
        {
            var placement = initialHex is int first
                ? selectionQuery.QueryContinuousPlacement(selected.DefinitionKey, first, hex)
                : selectionQuery.QueryPlacement(selected.DefinitionKey, hex);
            quote = new(placement.OriginalAllowed, placement.GoldCost, null, null,
                placement.OriginalReason, placement.UnresolvedInput);
            quotedHex = hex;
        }
        if (quote?.Allowed == true)
        {
            if(selected.Category==3&&initialHex is null)
            {
                initialHex=hex;quotedHex=null;quote=null;
                parent.ApplyHint(rules.QueryFacilityPlacementHint(text.SourceLocale,selected.Name,true));
                return true;
            }
            if (submit(selected, initialHex??hex, initialHex.HasValue?hex:null, waypoints))
            {
                LastSubmittedDefinitionKey=selected.DefinitionKey;LastSubmittedHexId=initialHex??hex;
                LastSubmittedUnitIds=unitIds;
                LastSubmittedUnitId=unitIds.Count==1?unitIds[0]:null;ConstructionSubmittedSequence++;
            }
            else if (!disposed)
                parent.ApplyHint(text.Get("play_facility_blocked"));
        }
        else parent.ApplyHint(text.Get(UnitFacilityOrdersView.QuoteStatus(quote, entries.Count > 0)));
        return true;
    }

    internal bool TryCancel()
    {
        if (disposed) return false;
        if (page is not null) { Dispose(); return true; }
        if(initialHex.HasValue)
        {
            initialHex=null;quotedHex=null;quote=null;
            parent.ApplyHint(rules.QueryFacilityPlacementHint(text.SourceLocale,definition!.Name,false));
            return true;
        }
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
