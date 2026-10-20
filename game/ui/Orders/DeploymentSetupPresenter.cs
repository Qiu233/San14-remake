using Godot;
using San14.Core;
using San14.Data;

/// <summary>Connects the original setup controls to an unsubmitted local draft.
/// Accept hands that draft to target selection, never to a world transaction.</summary>
internal sealed class DeploymentSetupPresenter : IDisposable
{
    private readonly DeploymentSetupView view;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private readonly GameState world;
    private readonly ScenarioCityPreview scenario;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int source;
    private readonly Action<IReadOnlyList<DeploymentDraftRow>> accepted;
    private readonly Action<string> unavailable;
    private readonly Dictionary<int,LoadedSprite?> portraits=[];
    private DeploymentPersonSelectionView? modal;
    private DeploymentCalculatorDialog? batchCalculator;
    private DeploymentAutomaticMenu? automaticMenu;
    private DeploymentAutomaticOptions? automaticOptions;
    private DeploymentBatchTroopEditor? batchTroops;
    private IReadOnlyList<int> batchOfficers=[];
    private bool disposed;
    internal DeploymentSetupDraft Draft {get;}

    internal DeploymentSetupPresenter(DeploymentSetupView view,GameResourceRepository repository,PreviewText text,
        GameState world,ScenarioCityPreview scenario,ArmyCampaignRuleAdapter rules,int sourceLocationId,
        Action<IReadOnlyList<DeploymentDraftRow>> accepted,Action<string> unavailable,
        IReadOnlyList<DeploymentDraftRow>? acceptedRows=null)
    {
        this.view=view;this.repository=repository;this.text=text;this.world=world;this.scenario=scenario;this.rules=rules;
        source=sourceLocationId;this.accepted=accepted;this.unavailable=unavailable;
        Draft=new(world,source,rules);
        if(acceptedRows is not null)Draft.RestoreAcceptedRows(acceptedRows);
        RefreshAll();
    }

    internal void OpenOfficerSelection()
    {
        if(disposed||modal is not null||batchCalculator is not null||automaticMenu is not null||view.UnitInformation.HasCalculator)return;
        var query=rules.QueryDeploymentPersonBasicList(world,source,0);
        if(!query.Available){unavailable(query.UnresolvedInput??"deployment.person-list.unavailable");return;}
        var entries=DeploymentPersonListContent.Create(repository,text,world,scenario,query);
        var selection=new DeploymentPersonSelectionView();modal=selection;view.AddChild(selection);
        selection.Configure(repository,text,entries,query.MaximumSelection!.Value,
            Draft.Rows.Select(row=>row.Request.OfficerId),0,result=>
            {
                if(disposed||modal!=selection)return;
                modal=null;selection.Hide();selection.QueueFree();
                if(result is null)return;
                if(Draft.SelectOfficers(result) is { } error){unavailable(error);return;}
                RefreshAll();
            },(rows,column,ascending)=>rules.QueryDeploymentPersonOrder(world,rows,column,ascending),unavailable,
            ids=>rules.QueryDeploymentPersonFormationCapabilities(world,ids),
            (rows,formation,ascending)=>rules.QueryDeploymentPersonFormationOrder(world,rows,formation,ascending),
            (ids,selected)=>rules.QueryDeploymentPersonLinkage(world,ids,selected),
            (rows,selected,slot,ascending)=>rules.QueryDeploymentPersonLinkageOrder(world,rows,selected,slot,ascending),
            (ids,firstSlot)=>rules.QueryDeploymentPersonTactics(world,ids,firstSlot),
            (rows,slot,ascending)=>rules.QueryDeploymentPersonTacticOrder(world,rows,slot,ascending),
            id=>world.Tactics.GetValueOrDefault(id)?.Name,
            ids=>rules.QueryDeploymentPersonTraits(world,ids),
            (rows,slot,ascending)=>rules.QueryDeploymentPersonTraitOrder(world,rows,slot,ascending));
    }

    internal bool TryCancelModal()
    {
        if(disposed)return false;
        if(modal is { } selection){selection.RequestCancel();return true;}
        if(batchCalculator is { } calculator){calculator.RequestCancel();return true;}
        if(automaticMenu is { } automatic){automatic.RequestCancel();return true;}
        return view.UnitInformation.TryCancelCalculator();
    }

    internal bool TryHandleAutomaticCancelInput(InputEvent input)=>
        !disposed&&automaticMenu?.TryHandleCancelInput(input)==true;

    private void OpenAutomaticMenu(IReadOnlyList<bool> available,Action<int> apply)
    {
        if(disposed||modal is not null||batchCalculator is not null||automaticMenu is not null||view.UnitInformation.HasCalculator)return;
        var menu=new DeploymentAutomaticMenu();automaticMenu=menu;view.AddAutomaticMenu(menu);
        try
        {
            menu.Configure(repository,text,available,strategy=>
            {
                if(disposed||automaticMenu!=menu)return;
                automaticMenu=null;menu.QueueFree();
                if(strategy is int value)apply(value);
            },view.ApplyBalloon);
        }
        catch{automaticMenu=null;menu.QueueFree();throw;}
    }

    private void RefreshAll()
    {
        var figures=rules.QueryDeploymentDraftFigures(world,Draft.Rows);
        var routes=rules.QueryOfficerPortraitRoutes(world,Draft.Rows.Select(row=>row.Request.OfficerId).ToArray());
        portraits.Clear();var sprites=new LocalizedUiSpriteSource(repository);
        foreach(var (officer,route) in routes)
            portraits[officer]=route.Kind==OfficerPortraitRouteKind.StockNamed?
                sprites.LoadTexturePackSprite(route.ResourceName!,route.SpriteName!):null;
        var contents=Draft.Rows.Select(row=>DeploymentSummaryContent.Create(repository,text,world,row,
            portraits.GetValueOrDefault(row.Request.OfficerId),figures[row.Request.OfficerId])).ToArray();
        view.ApplyDraftSummaries(repository,contents,Draft.FocusedOfficerId,officer=>
        {if(disposed)return;Draft.FocusOfficer(officer);RefreshAll();},officer=>
        {if(disposed)return;if(Draft.RemoveOfficer(officer) is { } error)unavailable(error);else RefreshAll();},OpenOfficerSelection);
        RefreshUnit();RefreshHeader();
    }

    private void RefreshUnit()
    {
        if(Draft.FocusedOfficerId is not int officer)
        {
            view.UnitInformation.ApplySelection(null,null,null,null,null);return;
        }
        if(Draft.WorkingRow is not { } working)
        {
            var row=Draft.Rows.Single(row=>row.Request.OfficerId==officer);
            string reason=Draft.PaneUnresolvedInput??"deployment.land_edit.unavailable";
            view.UnitInformation.ApplySelection(row,null,new(officer,null,null,null,reason,reason,reason),
                portraits.GetValueOrDefault(officer),null);
            view.UnitInformation.ApplyLandFormationChoices(null,0,null);return;
        }
        var rows=Draft.Rows.Select(row=>row.Request.OfficerId==officer?working:row).ToArray();
        int ordinal=Array.FindIndex(rows,row=>row.Request.OfficerId==officer);
        var info=rules.QueryDeploymentDraftUnitInfo(world,source,rows,ordinal);
        var figures=rules.QueryDeploymentDraftFigures(world,rows)[officer];
        view.UnitInformation.ApplySelection(working,info,figures,portraits.GetValueOrDefault(officer),value=>
        {
            if(disposed)return;
            if(Draft.SetTroops(value) is { } error){unavailable(error);RefreshUnit();return;}
            RefreshEditedSummary(officer,value);RefreshUnit();RefreshHeader();
        },result=>
        {
            if(disposed)return;
            int value=result??working.Request.Troops;
            if(Draft.SetTroops(value) is { } error){unavailable(error);RefreshUnit();return;}
            RefreshEditedSummary(officer,value);
            RefreshUnit();RefreshHeader();
        });
        view.UnitInformation.ApplyLandFormationChoices(rules.QueryDeploymentLandFormationChoices(world,officer),
            working.Request.FormationId,formation=>
            {if(disposed)return;if(Draft.SetLandFormation(formation) is { } error)unavailable(error);else RefreshAll();});
    }

    private void RefreshHeader()
    {
        var resources=rules.QueryDeploymentDraftResources(world,source,Draft.Rows);view.ApplyDraftResources(resources);
        bool canDecide=Draft.Rows.Count>0&&Draft.Rows.All(row=>row.Request.Troops>0)&&
            resources.Gold is {RemainingGold:>=0}&&resources.DecisionCommandBooks is int books&&
            books>=Draft.Rows.Count;
        view.ApplyDraftDecision(canDecide,()=>{if(!disposed)accepted(Draft.Rows);});
        var batch=ReadBatchTroops();
        // Ordinary Make always exposes the parent menu, including empty Make.
        // Only its five choices depend on the automatic producer's results.
        view.ApplyBatchActions(true,batch.Enabled==true,batch.Enabled==true,RequestBatch,Draft.Rows.Count>0);
    }

    private DeploymentBatchTroopEditor ReadBatchTroops()
    {
        var officers=Draft.Rows.Select(row=>row.Request.OfficerId).ToArray();
        // This Make page reads one immutable world. Local formation/troop
        // edits change neither commander capacities nor the base stock.
        if(batchTroops is null||!batchOfficers.SequenceEqual(officers))
        {
            batchTroops=rules.QueryDeploymentBatchTroopEditor(world,source,Draft.Rows);
            batchOfficers=officers;
        }
        return batchTroops;
    }

    private void RequestBatch(DeploymentSetupBatchAction action)
    {
        if(disposed||modal is not null||batchCalculator is not null||automaticMenu is not null||view.UnitInformation.HasCalculator)return;
        if(action==DeploymentSetupBatchAction.Automatic)
        {
            // The source's five permission trials share their own formation
            // counts. Local Make rows are not that source's external group.
            var options=automaticOptions??=rules.QueryDeploymentAutomaticOptions(world,source);
            if(!options.Available){unavailable(options.UnresolvedInput??"deployment.automatic.unavailable");return;}
            OpenAutomaticMenu(options.Options!.Select(option=>option.Enabled).ToArray(),ordinal=>
            {
                if(Draft.ComposeAutomatically((DeploymentAutomaticStyle)ordinal) is { } error)
                    unavailable(error);
                else RefreshAll();
            });
        }
        else if(action==DeploymentSetupBatchAction.MaximumTroops)
        {
            if(ReadBatchTroops().Enabled!=true)return;
            if(Draft.MaximizeTroops() is { } error)unavailable(error);else RefreshAll();
        }
        else if(action==DeploymentSetupBatchAction.TroopsTogether)
        {
            var options=ReadBatchTroops();
            if(options.Enabled!=true)return;
            if(!options.Available){unavailable(options.UnresolvedInput!);return;}
            var dialog=new DeploymentCalculatorDialog();batchCalculator=dialog;view.AddChild(dialog);
            try
            {
                dialog.Configure(repository,text,options.InitialValue!.Value,options.Minimum!.Value,options.Maximum!.Value,result=>
                {
                    if(disposed||batchCalculator!=dialog)return;
                    batchCalculator=null;dialog.QueueFree();
                    if(result is not int troops)return;
                    if(Draft.SetAllTroops(troops) is { } error)unavailable(error);else RefreshAll();
                },new(351,612));
            }
            catch{batchCalculator=null;dialog.QueueFree();throw;}
        }
    }

    private void RefreshEditedSummary(int officer,int troops)
    {
        var figures=rules.QueryDeploymentDraftFigures(world,Draft.Rows)[officer];
        var row=Draft.Rows.Single(row=>row.Request.OfficerId==officer);
        var content=DeploymentSummaryContent.Create(repository,text,world,row,portraits.GetValueOrDefault(officer),figures);
        view.ApplyDraftTroops(officer,troops,figures,content.TacticNames);
    }

    public void Dispose()
    {
        if(disposed)return;disposed=true;
        if(modal is { } selection){modal=null;selection.Abandon();selection.QueueFree();}
        if(batchCalculator is { } calculator){batchCalculator=null;calculator.Abandon();calculator.QueueFree();}
        if(automaticMenu is { } automatic){automaticMenu=null;automatic.Abandon();automatic.QueueFree();}
        view.UnitInformation.AbandonInteraction();
    }
}
