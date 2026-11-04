using Godot;
using San14.Core;
using San14.Data;

/// <summary>Connects the original setup controls to an unsubmitted local draft.
/// Accept hands that draft to target selection, never to a world transaction.</summary>
internal sealed partial class DeploymentSetupPresenter : IDisposable
{
    private readonly DeploymentSetupView view;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private GameState world;
    private readonly ScenarioCityPreview scenario;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int source;
    private readonly Action<IReadOnlyList<DeploymentDraftRow>> accepted;
    private readonly Action<string> unavailable;
    private readonly Func<int>? scrollSpeed;
    private readonly UiSourceHeaderStack headers;
    private readonly Func<int,int> nextApplication;
    private readonly Dictionary<int,LoadedUiSpriteSource?> portraits=[];
    private readonly Dictionary<int,AreaAdministrationQuery<DeploymentOfficerPanePublication>> officerPanes=[];
    private int? displayedOfficer;
    private DeploymentPersonSelectionView? modal;
    private DeploymentCalculatorDialog? batchCalculator;
    private DeploymentAutomaticMenu? automaticMenu;
    private DeploymentAutomaticOptions? automaticOptions;
    private DeploymentBatchTroopEditor? batchTroops;
    private IReadOnlyList<int> batchOfficers=[];
    private long officerSelectionChanges;
    private Func<IReadOnlyList<int>,bool>? officerSelectionAdmission;
    private Func<IReadOnlyList<DeploymentDraftRow>,bool>? decisionAdmission;
    private bool ordinaryDecisionAllowed;
    private bool disposed;
    internal DeploymentSetupDraft Draft {get;}
    internal bool IsOfficerSelectionOpen=>!disposed&&modal?.IsVisibleInTree()==true;
    internal bool OfficerSelectionActive=>!disposed&&modal?.IsSelectionActive==true;
    //UnitMakeRootAction+180 is the five-strategy automatic pulldown.
    internal bool IsAutomaticMenuOpen=>!disposed&&automaticMenu?.IsVisibleInTree()==true;
    internal IReadOnlyList<int> OfficerSelectionSelectedOfficerIds=>modal?.SelectedOfficerIds??[];
    internal long OfficerSelectionChangedSequence=>officerSelectionChanges+(modal?.SelectionChangedSequence??0);
    internal Func<IReadOnlyList<int>,bool>? OfficerSelectionAdmission
    {
        get=>officerSelectionAdmission;
        set {officerSelectionAdmission=value;modal?.RefreshAdmission();}
    }
    internal bool FormationSelectionActive=>!disposed&&view.UnitInformation.FormationSelectionActive;
    internal bool UnitConfigurationPresent=>!disposed&&!view.UnitInformation.IsQueuedForDeletion()&&
        view.UnitInformation.IsInsideTree();
    internal bool UnitConfigurationActive=>UnitConfigurationPresent&&view.UnitInformation.IsVisibleInTree()&&
        view.UnitInformation.CanProcess()&&modal is null&&batchCalculator is null&&automaticMenu is null&&
        definitionModal is null&&!view.UnitInformation.HasCalculator;
    internal int? SelectedFormationId=>view.UnitInformation.SelectedFormationId;
    internal DeploymentTroopMarker? TroopMarker=>view.UnitInformation.TroopMarker;
    internal bool TryApplyTutorialTacticCue(int? markerId,bool enabled)=>
        UnitConfigurationPresent&&view.UnitInformation.TryApplyTutorialTacticCue(markerId,enabled);
    internal IReadOnlyList<DeploymentDraftRow> Rows=>Draft.Rows;
    internal int? FocusedOfficerId=>Draft.FocusedOfficerId;
    internal long OfficerSelectionAcceptedSequence {get;private set;}
    internal IReadOnlyList<int> LastAcceptedOfficerIds {get;private set;}=[];
    internal long LandFormationAcceptedSequence {get;private set;}
    internal DeploymentDraftRow? LastAcceptedLandFormationRow {get;private set;}
    internal long TroopsAcceptedSequence {get;private set;}
    internal IReadOnlyList<DeploymentDraftRow> LastAcceptedTroopsRows {get;private set;}=[];
    internal long SettingsChangedSequence {get;private set;}
    internal IReadOnlyList<DeploymentDraftRow> LastChangedRows {get;private set;}=[];
    // These counters belong to this presenter instance. Restoring or binding
    // rows does not accept an operation. This is a local draft handoff only.
    internal long SetupAcceptedSequence {get;private set;}
    internal IReadOnlyList<DeploymentDraftRow> LastAcceptedRows {get;private set;}=[];
    internal Func<IReadOnlyList<DeploymentDraftRow>,bool>? DecisionAdmission
    {
        get=>decisionAdmission;
        set {decisionAdmission=value;RefreshDecisionAdmission();}
    }

    internal DeploymentSetupPresenter(DeploymentSetupView view,GameResourceRepository repository,PreviewText text,
        GameState world,ScenarioCityPreview scenario,ArmyCampaignRuleAdapter rules,int sourceLocationId,
        Action<IReadOnlyList<DeploymentDraftRow>> accepted,Action<string> unavailable,UiSourceHeaderStack headers,
        Func<int,int> nextApplication,
        IReadOnlyList<DeploymentDraftRow>? acceptedRows=null,Func<int>? scrollSpeed=null)
    {
        this.view=view;this.repository=repository;this.text=text;this.world=world;this.scenario=scenario;this.rules=rules;
        source=sourceLocationId;this.accepted=accepted;this.unavailable=unavailable;
        this.scrollSpeed=scrollSpeed;
        this.headers=headers;this.nextApplication=nextApplication;
        Draft=new(world,source,rules);
        if(acceptedRows is not null)Draft.RestoreAcceptedRows(acceptedRows);
        RefreshAll();
    }

    internal void AdoptSpeechWorld(GameState expected,GameState replacement)
    {
        if(disposed||!ReferenceEquals(world,expected))return;
        Draft.AdoptSpeechWorld(expected,replacement);world=replacement;
    }

    internal void OpenOfficerSelection()
    {
        if(disposed||definitionModal is not null||modal is not null||batchCalculator is not null||automaticMenu is not null||view.UnitInformation.HasCalculator)return;
        var query=rules.QueryDeploymentPersonBasicList(world,source,0);
        if(!query.Available){unavailable(query.UnresolvedInput??"deployment.person-list.unavailable");return;}
        var entries=DeploymentPersonListContent.Create(repository,text,world,scenario,query);
        var selection=new DeploymentPersonSelectionView { TransitionFailure=view.TransitionFailure };modal=selection;view.AddChild(selection);
        try
        {
            view.BeginChildSelection(selection, keepBackdrop: true);
            selection.Configure(repository,text,entries,query.MaximumSelection!.Value,
                Draft.Rows.Select(row=>row.Request.OfficerId),scrollSpeed?.Invoke()??0,result=>
                {
                    if(disposed||modal!=selection)return;
                    RetireOfficerSelection(selection);
                    if(result is null)return;
                    var previous=Draft.Rows;
                    if(Draft.SelectOfficers(result) is { } error){unavailable(error);return;}
                    LastAcceptedOfficerIds=Array.AsReadOnly(Draft.Rows.Select(row=>row.Request.OfficerId).ToArray());
                    OfficerSelectionAcceptedSequence++;
                    RecordSettingsChange(previous);
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
                (rows,slot,ascending)=>rules.QueryDeploymentPersonTraitOrder(world,rows,slot,ascending),currentScrollSpeed:scrollSpeed,
                selectionAdmission:ids=>OfficerSelectionAdmission?.Invoke(ids)??true,commonBackdrop:view.CommonBackdrop,resources:view.SourceResources);
            selection.RegisterHeader(headers);
        }
        catch
        {
            RetireOfficerSelection(selection);
            throw;
        }
    }

    private void RetireOfficerSelection(DeploymentPersonSelectionView? expected = null)
    {
        if(modal is not { } selection || expected is not null && !ReferenceEquals(expected,selection))return;
        modal=null;
        if(GodotObject.IsInstanceValid(selection))
        {
            officerSelectionChanges+=selection.SelectionChangedSequence;
            selection.Abandon();
            if(GodotObject.IsInstanceValid(view))view.EndChildSelection(selection, restore: !disposed);
            if(!selection.IsQueuedForDeletion())selection.QueueFree();
        }
        else if(GodotObject.IsInstanceValid(view))view.EndChildSelection(selection, restore: !disposed);
    }

    internal bool TryCancelModal()
    {
        if(disposed)return false;
        if(definitionModal is { } definition){definition.RequestCancel();return true;}
        if(modal is { } selection){selection.RequestCancel();return true;}
        if(batchCalculator is { } calculator){calculator.RequestCancel();return true;}
        if(automaticMenu is { } automatic){automatic.RequestCancel();return true;}
        return view.UnitInformation.TryCancelCalculator();
    }

    internal bool TryHandleAutomaticCancelInput(InputEvent input)=>
        !disposed&&automaticMenu?.TryHandleCancelInput(input)==true;

    private void OpenAutomaticMenu(IReadOnlyList<bool> available,Action<int> apply)
    {
        if(disposed||definitionModal is not null||modal is not null||batchCalculator is not null||automaticMenu is not null||view.UnitInformation.HasCalculator)return;
        var menu=new DeploymentAutomaticMenu();automaticMenu=menu;view.AddAutomaticMenu(menu);
        try
        {
            menu.Configure(repository,text,available,strategy=>
            {
                if(disposed||automaticMenu!=menu)return;
                automaticMenu=null;menu.QueueFree();
                if(strategy is int value)apply(value);
            },view.ApplyBalloon,resources:view.SourceResources);
        }
        catch{automaticMenu=null;menu.QueueFree();throw;}
    }

    private void RefreshAll()
    {
        if(disposed||definitionModal is not null)return;
        var figures=rules.QueryDeploymentDraftFigures(world,Draft.Rows);
        var routes=rules.QueryOfficerPortraitRoutes(world,Draft.Rows.Select(row=>row.Request.OfficerId).ToArray());
        portraits.Clear();
        foreach(var (officer,route) in routes)
            portraits[officer]=route.Kind==OfficerPortraitRouteKind.StockNamed?
                view.SourceResources.Assets.Portraits.RequireSource(route.ResourceName!,route.SpriteName!):null;
        var contents=Draft.Rows.Select(row=>DeploymentSummaryContent.Create(repository,text,world,row,
            portraits.GetValueOrDefault(row.Request.OfficerId),figures[row.Request.OfficerId])).ToArray();
        view.ApplyDraftSummaries(repository,contents,Draft.FocusedOfficerId,officer=>
        {if(disposed||definitionModal is not null)return;Draft.FocusOfficer(officer);RefreshAll();},officer=>
        {
            if(disposed||definitionModal is not null)return;
            var previous=Draft.Rows;
            if(Draft.RemoveOfficer(officer) is { } error)unavailable(error);
            else {RecordSettingsChange(previous);RefreshAll();}
        },OpenOfficerSelection,
            scrollSpeed?.Invoke()??0,scrollSpeed);
        RefreshUnit();RefreshHeader();
    }

    private void RefreshUnit()
    {
        //The campaign world is borrowed and immutable for this setup. Read
        //each selected Person once; troop/formation/tactic edits reuse its pane.
        if (displayedOfficer != Draft.FocusedOfficerId)
        {
            displayedOfficer = Draft.FocusedOfficerId;
            if (displayedOfficer is int id)
            {
                if (!officerPanes.TryGetValue(id,out var pane))
                    officerPanes[id] = pane = rules.QueryDeploymentOfficerPane(world,id);
                view.OfficerInformation.Apply(pane);
            }
            else view.OfficerInformation.Apply(null);
        }
        if(Draft.FocusedOfficerId is not int officer)
        {
            view.UnitInformation.ApplySelection(null,null,null,null,null);
            view.UnitInformation.ApplyTacticChoices(null,null);
            view.UnitInformation.ApplyDefinitionButtons(null,null);
            RefreshUnitPresentation(null,null,null);return;
        }
        if(Draft.WorkingRow is not { } working)
        {
            var row=Draft.Rows.Single(row=>row.Request.OfficerId==officer);
            string reason=Draft.PaneUnresolvedInput??"deployment.land_edit.unavailable";
            view.UnitInformation.ApplySelection(row,null,new(officer,null,null,null,reason,reason,reason),
                portraits.GetValueOrDefault(officer),null);
            view.UnitInformation.ApplyLandFormationChoices(null,0,null);
            view.UnitInformation.ApplyTacticChoices(null,null);
            view.UnitInformation.ApplyDefinitionButtons(null,null);
            RefreshUnitPresentation(officer,null,null);return;
        }
        var rows=Draft.Rows.Select(row=>row.Request.OfficerId==officer?working:row).ToArray();
        int ordinal=Array.FindIndex(rows,row=>row.Request.OfficerId==officer);
        var info=rules.QueryDeploymentDraftUnitInfo(world,source,rows,ordinal);
        var figures=rules.QueryDeploymentDraftFigures(world,rows)[officer];
        view.UnitInformation.ApplySelection(working,info,figures,portraits.GetValueOrDefault(officer),value=>
        {
            if(disposed||definitionModal is not null)return;
            var previous=Draft.Rows;
            if(Draft.SetTroops(value) is { } error){unavailable(error);RefreshUnit();return;}
            RecordTroopsAccepted(previous);
            RefreshEditedSummary(officer,value);RefreshUnit();RefreshHeader();
        },result=>
        {
            if(disposed||definitionModal is not null)return;
            int value=result??working.Request.Troops;
            var previous=Draft.Rows;
            if(Draft.SetTroops(value) is { } error){unavailable(error);RefreshUnit();return;}
            // The ordinary cancel path restores the old value. It is not an
            // accepted calculator operation, even when that write succeeds.
            if(result.HasValue)RecordTroopsAccepted(previous);
            RefreshEditedSummary(officer,value);
            RefreshUnit();RefreshHeader();
        });
        view.UnitInformation.ApplyLandFormationChoices(rules.QueryDeploymentLandFormationChoices(world,officer,prepareBalloons:true),
            working.Request.FormationId,formation=>
            {
                if(disposed||definitionModal is not null)return;
                var previous=Draft.Rows;
                if(Draft.SetLandFormation(formation) is { } error)unavailable(error);
                else
                {
                    LastAcceptedLandFormationRow=Draft.Rows.Single(row=>row.Request.OfficerId==officer);
                    LandFormationAcceptedSequence++;
                    RecordSettingsChange(previous);RefreshAll();
                }
            },()=>UnitConfigurationActive);
        view.UnitInformation.ApplyTacticChoices(rules.QueryDeploymentTacticChoices(world,officer,
            working.Request.FormationId,working.Request.TacticIds,prepareBalloons:true),occurrence=>
            {
                if(disposed||definitionModal is not null)return;
                var previous=Draft.Rows;
                if(Draft.ToggleTacticOccurrence(occurrence) is { } error)unavailable(error);
                else {RecordSettingsChange(previous);RefreshAll();}
            },()=>UnitConfigurationActive);
        view.UnitInformation.ApplyDefinitionButtons(OpenFormationDefinitions,OpenTacticDefinitions);
        RefreshUnitPresentation(officer,working.Request.FormationId,info.UsesSpecialNavalTacticText);
    }

    private void RefreshHeader()
    {
        var resources=rules.QueryDeploymentDraftResources(world,source,Draft.Rows);view.ApplyDraftResources(resources);
        bool canDecide=Draft.Rows.Count>0&&Draft.Rows.All(row=>row.Request.Troops>0)&&
            resources.Gold is {RemainingGold:>=0}&&resources.DecisionCommandBooks is int books&&
            books>=Draft.Rows.Count;
        ordinaryDecisionAllowed=canDecide;RefreshDecisionAdmission();
        var batch=ReadBatchTroops();
        // Ordinary Make always exposes the parent menu, including empty Make.
        // Only its five choices depend on the automatic producer's results.
        view.ApplyBatchActions(true,batch.Enabled==true,batch.Enabled==true,RequestBatch,Draft.Rows.Count>0);
    }

    internal void RefreshDecisionAdmission()=>view.ApplyDraftDecision(CanAcceptDecision(),AcceptSetupDraft);

    private bool CanAcceptDecision()=>!disposed&&definitionModal is null&&modal is null&&batchCalculator is null&&
        automaticMenu is null&&!view.UnitInformation.HasCalculator&&ordinaryDecisionAllowed&&
        (decisionAdmission?.Invoke(Draft.Rows)??true);

    private void AcceptSetupDraft()
    {
        if(!CanAcceptDecision())return;
        var rows=Draft.Rows;
        accepted(rows);
        LastAcceptedRows=rows;SetupAcceptedSequence++;
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
        if(disposed||definitionModal is not null||modal is not null||batchCalculator is not null||automaticMenu is not null||view.UnitInformation.HasCalculator)return;
        if(action==DeploymentSetupBatchAction.Automatic)
        {
            // The source's five permission trials share their own formation
            // counts. Local Make rows are not that source's external group.
            var options=automaticOptions??=rules.QueryDeploymentAutomaticOptions(world,source);
            if(!options.Available){unavailable(options.UnresolvedInput??"deployment.automatic.unavailable");return;}
            OpenAutomaticMenu(options.Options!.Select(option=>option.Enabled).ToArray(),ordinal=>
            {
                var previous=Draft.Rows;
                if(Draft.ComposeAutomatically((DeploymentAutomaticStyle)ordinal) is { } error)
                    unavailable(error);
                else {RecordSettingsChange(previous);RefreshAll();}
            });
        }
        else if(action==DeploymentSetupBatchAction.MaximumTroops)
        {
            if(ReadBatchTroops().Enabled!=true)return;
            var previous=Draft.Rows;
            if(Draft.MaximizeTroops() is { } error)unavailable(error);
            else {RecordTroopsAccepted(previous);RefreshAll();}
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
                    var previous=Draft.Rows;
                    if(Draft.SetAllTroops(troops) is { } error)unavailable(error);
                    else {RecordTroopsAccepted(previous);RefreshAll();}
                },new(351,612),resources:view.SourceResources);
            }
            catch{batchCalculator=null;dialog.QueueFree();throw;}
        }
    }

    private void RecordTroopsAccepted(IReadOnlyList<DeploymentDraftRow> previous)
    {
        LastAcceptedTroopsRows=Draft.Rows;TroopsAcceptedSequence++;
        RecordSettingsChange(previous);
    }

    private void RecordSettingsChange(IReadOnlyList<DeploymentDraftRow> previous)
    {
        var rows=Draft.Rows;
        bool changed=previous.Count!=rows.Count||previous.Zip(rows).Any(pair=>
        {
            var before=pair.First.Request;var after=pair.Second.Request;
            return before.OfficerId!=after.OfficerId||before.FormationId!=after.FormationId||
                before.Troops!=after.Troops||pair.First.NavalFormationId!=pair.Second.NavalFormationId||
                before.Behavior!=after.Behavior||!before.TacticIds.SequenceEqual(after.TacticIds);
        });
        if(!changed)return;
        LastChangedRows=rows;SettingsChangedSequence++;
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
        RetireDefinitionSelection();
        RetireOfficerSelection();
        if(batchCalculator is { } calculator){batchCalculator=null;calculator.Abandon();calculator.QueueFree();}
        if(automaticMenu is { } automatic){automaticMenu=null;automatic.Abandon();automatic.QueueFree();}
        view.UnitInformation.AbandonInteraction();
        view.OfficerInformation.Abandon();officerPanes.Clear();
        AbandonUnitPresentations();
    }
}
