using San14.Core;
using San14.Data;

/// <summary>Definition selection and real map placement for unsubmitted rows.
/// This owner never allocates an Army or publishes a simulation command.</summary>
internal sealed partial class DeploymentTargetPresenter
{
    private readonly bool? tutorialRestrictsFacilities;
    private readonly Func<int> facilityScrollSpeed;
    private DeploymentFacilitySelectionQuery? facilityQuery;
    private UnitFacilitySelectionView? facilityPage;
    private FacilityConstructionChoice? facilityDefinition;
    private int? facilityInitialHex;
    internal bool IsFacilitySelectionOpen=>!disposed&&facilityPage is { } page&&
        !page.IsQueuedForDeletion()&&page.IsVisibleInTree();
    internal int? CurrentChoiceDefinitionKey=>IsFacilitySelectionOpen?facilityPage?.CurrentChoiceDefinitionKey:null;
    internal bool FacilitySelectionActive=>!disposed&&facilityPage?.SelectionActive==true;
    internal string? SelectedFacilityDefinitionKey=>disposed?null:facilityDefinition?.DefinitionKey;
    internal bool IsFacilityPlacementActive=>!disposed&&facilityQuery is not null&&facilityPage is null&&
        facilityDefinition is not null&&!view.IsQueuedForDeletion()&&view.IsVisibleInTree();
    internal long FacilityChoiceAcceptedSequence {get;private set;}
    internal string? LastAcceptedFacilityDefinitionKey {get;private set;}
    internal long ConstructionAssignedSequence {get;private set;}
    internal IReadOnlyList<DeploymentDraftRow> LastConstructionAssignedRows {get;private set;}=[];
    internal int? LastConstructionAssignedHexId {get;private set;}

    private void OpenFacilities()
    {
        if(disposed||confirmation is not null||facilityQuery is not null||Draft.SelectedRows.Count==0)return;
        CloseMenu();
        facilityQuery=rules.CreateDeploymentFacilitySelectionQuery(world,Draft.FacilityRows,tutorialRestrictsFacilities);
        ShowFacilityPage();
    }

    private void ShowFacilityPage()
    {
        if(facilityQuery is not { } query)return;
        facilityDefinition=null;facilityInitialHex=null;view.SetFacilitySelectionMode(true);modalChanged(true);
        var entries=query.Directory.Rows.Where(row=>row.OriginalIncluded!=false).Select(row=>
            new UnitFacilitySelectionEntry(new(row.DefinitionKey,row.Name??text.Get("play_facility_order_unavailable"),row.Category),
                row.OriginalIncluded==true?row.OriginalAllowed:null,row.OriginalReason,
                row.CommandSupported&&row.Name is not null)).ToArray();
        var child=new UnitFacilitySelectionView();facilityPage=child;view.AddChild(child);
        child.Configure(repository,text,entries,choice=>
        {
            if(disposed||facilityPage!=child)return;
            CloseFacilityPage();
            if(choice is null){CloseFacilities();return;}
            facilityDefinition=choice;view.SetFacilitySelectionMode(true,placing:true);modalChanged(false);
            view.ApplyHint(rules.QueryFacilityPlacementHint(text.SourceLocale,choice.Name,false));
            LastAcceptedFacilityDefinitionKey=choice.DefinitionKey;FacilityChoiceAcceptedSequence++;
        },query.PreselectedDefinitionKeys,query.Directory.Pages,facilityScrollSpeed);
        if(query.Directory.UnresolvedInput is not null||query.PreselectionUnresolvedInput is not null)
            child.ApplyHint(text.Get("play_facility_order_unavailable"));
    }

    private void PickFacilityTarget(int hex)
    {
        if(facilityPage is not null||facilityDefinition is not { } definition||facilityQuery is not { } query)return;
        var placement=facilityInitialHex is int first
            ?query.QueryContinuousPlacement(definition.DefinitionKey,first,hex)
            :query.QueryPlacement(definition.DefinitionKey,hex);
        if(placement is {OriginalAllowed:true,CommandUnresolvedInput:null,Action:not null})
        {
            if(definition.Category==3&&facilityInitialHex is null)
            {
                facilityInitialHex=hex;
                view.ApplyHint(rules.QueryFacilityPlacementHint(text.SourceLocale,definition.Name,true));
                return;
            }
            var officers=Draft.SelectedOfficers;
            Draft.AssignConstruction(placement,facilityInitialHex.HasValue?hex:null);
            LastConstructionAssignedRows=Array.AsReadOnly(Draft.Rows.Where(row=>officers.Contains(row.Request.OfficerId)).ToArray());
            LastConstructionAssignedHexId=placement.ClickedHexId;ConstructionAssignedSequence++;
            CloseFacilities();
            view.ApplySelection(Draft.SelectedOfficers);Refresh();
        }
        else
        {
            view.ApplyHint(text.Get(placement.UnresolvedInput is not null||placement.CommandUnresolvedInput is not null
                ?"play_facility_order_unavailable":"play_facility_blocked"));
        }
    }

    private void CancelFacilities()
    {
        if(facilityPage is not null)CloseFacilities();
        else if(facilityInitialHex.HasValue)
        {
            facilityInitialHex=null;
            view.ApplyHint(rules.QueryFacilityPlacementHint(text.SourceLocale,facilityDefinition!.Name,false));
        }
        else ShowFacilityPage();
    }

    private void CloseFacilityPage()
    {
        var old=facilityPage;facilityPage=null;
        if(old is null)return;
        old.Abandon();old.GetParent()?.RemoveChild(old);old.QueueFree();
    }

    private void CloseFacilities()
    {
        if(facilityQuery is null)return;
        CloseFacilityPage();facilityQuery=null;facilityDefinition=null;facilityInitialHex=null;
        view.SetFacilitySelectionMode(false);view.ApplyHint(null);modalChanged(false);RefreshPoints();
    }
}
