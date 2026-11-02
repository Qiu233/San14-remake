using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private NewGameSetupView? _newGameSetup;

    private void OpenNewGameSetup()
    {
        if(_failed||_shuttingDown||_newGameSetup is not null||_repository is null||
            _world is not {Phase:TurnPhase.Initializing}||_playerForceId.HasValue||_campaignComposition is null)return;
        if(TryOpenRegisteredOfficerNewGame()||TryOpenNewGameItemEditor())return;
        int[] selected=_scenarioList.GetSelectedItems();
        if(selected.Length!=1)throw new InvalidDataException("New-game scenario selection is missing.");
        BeginNewGameScenarioPreparation(selected[0]);
    }

    private void OpenPreparedNewGameSetup(PreparedNewGamePage prepared, NewGamePreparationTiming? timing = null)
    {
        if(_failed||_shuttingDown||_newGameSetup is not null||_repository is null||
            _world is not {Phase:TurnPhase.Initializing} world||_playerForceId.HasValue||_campaignComposition is null)return;
        if(TryOpenRegisteredOfficerNewGame()||TryOpenNewGameItemEditor())return;
        var repository=_repository;
        var composition=_campaignComposition;
        var previewSession=_session;
        var army=composition.PrepareArmyQueries(world);
        var publication=prepared.Preparation.ForceSelection;
        var content=prepared.Content;var header=prepared.Header;var map=prepared.Map;
        var view=new NewGameSetupView();_newGameSetup=view;AddChild(view);
        bool Admitted()=>!_failed&&!_shuttingDown&&_newGameSetup==view&&ReferenceEquals(_session,previewSession)&&
            _world is {Phase:TurnPhase.Initializing}&&!_playerForceId.HasValue;
        try
        {
            // AddChild precedes Configure: the source page's default selection
            // passes the same visible, live host admission as later input.
            view.Configure(repository,_ui,publication,header,content.Faces,null,content.ResolveRuler,
                _newGameOptions,prepared.Preparation.Settings,Admitted,
                id=>RunUiAction(()=>{if(Admitted())SelectNewGameForce(view,id);}),
                id=>RunUiAction(()=>{if(Admitted())PreviewNewGameForce(view,map,id);}),
                ()=>RunUiAction(()=>{if(Admitted())ReturnFromNewGameForceSelection();}),
                (id,options)=>RunUiAction(()=>{if(Admitted())ConfirmNewGameSetup(id,options);}),
                error=>{if(_newGameSetup==view)ShowFailure(error,startup:false);},
                diagnostic:timing is null?null:timing.Mark, resources:TryBorrowApplicationUiResources(view));
            if(_newGameSetup!=view)return;
            view.ConfigureConfirmation(content.ResolveConfirmationRuler,
                () => CaptureNewGameConfirmationExtras(composition, world, army), () => (int)CurrentListScrollSpeed());
            view.ConfigureGovernment(force=>BuildNewGameForceGovernment(composition,world,force));
            view.ConfigureNavigation(()=>RunUiAction(()=>{if(Admitted())OpenTitleMenu();}));
            view.ConfigureOfficers(force=>BuildNewGameForceOfficers(composition,world,force),CurrentListScrollSpeed);
            view.ConfigureGeographicAdvantages(preview =>
            {
                var result=army.PrepareNewGameGeographicAdvantageInformationPages(world,preview);
                return result.Available&&result.Value is {} pages?pages:
                    throw new InvalidDataException(result.UnresolvedInput??"New-game geographic information is unavailable.");
            });
            view.ConfigureForcePicker(force=>army.CreateNewGameForcePicker(world,publication,force));
            _browserContent.Hide();_browserContent.ProcessMode=ProcessModeEnum.Disabled;
            BeginNewGameMap(view,map);
        }
        catch { CloseNewGameSetup();throw; }
    }

    private void SelectNewGameForce(NewGameSetupView view,int forceId)
    {
        if(_newGameSetup!=view||_world is not { Phase:TurnPhase.Initializing })return;
        int index=ScenarioBrowserViews.FindForce(_shownForces,forceId);
        if(index<0)throw new InvalidDataException("Selected new-game force is unavailable.");
        // Keep the hidden browser's formal selection in sync. Hover never
        // enters this path and the old browser portrait is not this page's art.
        _force.Select(index);_selectedForceId=forceId;
    }

    private void PreviewNewGameForce(NewGameSetupView view,NewGameForceMapContent map,int? forceId)
    {
        if(_newGameSetup!=view||_world is not { Phase:TurnPhase.Initializing })return;
        view.ApplySourceMapSelection();
    }

    private void ConfirmNewGameSetup(int forceId,NewGameOptions options)
    {
        if(_newGameSetup is not {IsSettingsPage:true,IsConfirmationPage:true} view||view.SelectedForceId!=forceId||
            view.Publication.ByForceId.GetValueOrDefault(forceId)?.CanConfirm!=true||
            _world is not { Phase:TurnPhase.Initializing }||_playerForceId.HasValue)return;
        BeginConfirmedNewGamePreparation(view,forceId,options);
    }

    private NewGameConfirmationExtras CaptureNewGameConfirmationExtras(CampaignComposition composition,
        GameState world, ArmyCampaignRuleAdapter army)
    {
        var repository = _repository ?? throw new InvalidOperationException("Confirmation resources have retired.");
        var admission = CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current);
        var adoptedHistoricalIds = _historicalOfficerNewGameAdoptedIds.ToHashSet();
        // Match the captured profile order used by HistoricalOfficerScenario.Apply.
        // Selection order is a UI detail and cannot choose the summary's first name.
        IReadOnlyList<int>? historicalIds = composition.Scenario.InitialHistoricalOfficerEditsAdopted
            ? _historicalOfficerNewGameProfile?.Overrides
                .Where(person => adoptedHistoricalIds.Contains(person.Id)).Select(person => person.Id).ToArray()
            : [];
        var preview = army.CaptureNewGameConfirmation(world, admission, historicalIds);
        var data = repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        string Calendar(int id, int value)
        {
            string format = data[id].Text ?? throw new InvalidDataException("Confirmation calendar resource is missing.");
            return format.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        var rows = preview.OfficerRows.Select(row => new NewGameConfirmationRow(row.TitleId, row.Value, row.NonTrophy,
            row.UnresolvedInputs.Count == 0 ? null : string.Join("; ", row.UnresolvedInputs))).ToArray();
        return new(Calendar(0x569, world.Date.Year), Calendar(0x611, world.Date.Month), rows,
            preview.CollectionEligible, preview.UnresolvedInputs.Count == 0 ? null : string.Join("; ", preview.UnresolvedInputs));
    }

    private void CloseNewGameSetup()
    {
        CloseRegisteredOfficerNewGame();
        CloseNewGameItemAdoption();
        RetireNewGameMap();
        var view=_newGameSetup;_newGameSetup=null;
        if(view is not null&&GodotObject.IsInstanceValid(view))
        { view.Abandon();if(view.GetParent() is Node parent)parent.RemoveChild(view);view.QueueFree(); }
        if(!_failed&&!_shuttingDown&&_scenarioSelection is null)
        { _browserContent.ProcessMode=ProcessModeEnum.Inherit;_browserContent.Show(); }
    }
}
