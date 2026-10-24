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
            _world is not { Phase:TurnPhase.Initializing } world||_playerForceId.HasValue||_campaignComposition is null)return;
        var repository=_repository;
        var composition=_campaignComposition;
        var previewSession=_session;
        var army=composition.PrepareArmyQueries(world);
        // This ordinary new-game route owns no task scene, task index or
        // CCC person-selection service. Capture once for this page lifetime.
        var publication=army.CaptureNewGameForceSelection(world,ArmyFlagPaneNames.Load(composition.Scenario),new(false,null,false));
        var content=new NewGameForcePageContent(repository,army,world,publication);
        var metadata=composition.Scenario.Metadata;
        var messages=repository.LoadMessages(_ui.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        string yearSuffix=messages[0x2AB].Text??throw new InvalidDataException("Scenario year suffix is missing.");
        string monthSuffix=messages[0x2AC].Text??throw new InvalidDataException("Scenario month suffix is missing.");
        var header=new NewGameForcePageHeader(
            string.Format(CultureInfo.InvariantCulture,"{0,3}{1}",world.Date.Year,yearSuffix),
            string.Format(CultureInfo.InvariantCulture,"{0,2}{1}",world.Date.Month,monthSuffix),
            metadata.Title??throw new InvalidDataException("Scenario title is missing."),new Dictionary<int,string>());
        var map=MapOverviewView.Capture(_mapOverview??throw new InvalidDataException("Scenario map preview is missing."),world);
        var view=new NewGameSetupView();_newGameSetup=view;AddChild(view);
        bool Admitted()=>!_failed&&!_shuttingDown&&_newGameSetup==view&&ReferenceEquals(_session,previewSession)&&
            _world is {Phase:TurnPhase.Initializing}&&!_playerForceId.HasValue;
        try
        {
            // AddChild precedes Configure: the source page's default selection
            // passes the same visible, live host admission as later input.
            view.Configure(repository,_ui,publication,header,content.Faces,null,content.ResolveRuler,
                _newGameOptions,army.CaptureNewGameSettings(_newGameOptions),Admitted,
                id=>RunUiAction(()=>{if(Admitted())SelectNewGameForce(view,id);}),
                id=>RunUiAction(()=>{if(Admitted())PreviewNewGameForce(view,map,id);}),
                ()=>RunUiAction(()=>{if(Admitted()){CloseNewGameSetup();OpenScenarioSelection();}}),
                (id,options)=>RunUiAction(()=>{if(Admitted())ConfirmNewGameSetup(id,options);}),
                locale=>RunUiAction(()=>{if(Admitted())ChangeNewGameLanguage(locale);}),
                error=>{if(_newGameSetup==view)ShowFailure(error,startup:false);});
            if(_newGameSetup!=view)return;
            view.ConfigureGovernment(force=>BuildNewGameForceGovernment(composition,world,force));
            view.ConfigureNavigation(()=>RunUiAction(()=>{if(Admitted())OpenTitleMenu();}));
            view.ConfigureOfficers(force=>BuildNewGameForceOfficers(composition,world,force));
            view.ConfigureForcePicker(force=>army.CreateNewGameForcePicker(publication,force));
            _browserContent.Hide();_browserContent.ProcessMode=ProcessModeEnum.Disabled;
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

    private void PreviewNewGameForce(NewGameSetupView view,MapOverviewView map,int? forceId)
    {
        if(_newGameSetup!=view||_world is not { Phase:TurnPhase.Initializing })return;
        using Image image=MapOverviewRaster.Draw(map,_mapMode.Selected==0,forceId,null,_mapSurface);
        view.ApplyMap(ImageTexture.CreateFromImage(image));
    }

    private void ConfirmNewGameSetup(int forceId,NewGameOptions options)
    {
        if(_newGameSetup is not {IsSettingsPage:true} view||view.SelectedForceId!=forceId||
            view.Publication.ByForceId.GetValueOrDefault(forceId)?.CanConfirm!=true||
            _world is not { Phase:TurnPhase.Initializing }||_playerForceId.HasValue)return;
        view.SetBusy(true);
        // Reinitialize at most once, after the player confirms the complete
        // draft. Previewing option choices never reloads the selected scenario.
        if(options!=_newGameOptions)
        {
            int[] selection=_scenarioList.GetSelectedItems();
            if(selection.Length!=1)throw new InvalidDataException("New-game scenario selection is missing.");
            _newGameOptions=options;ShowScenario(selection[0]);
        }
        SelectNewGameForce(view,forceId);StartPlayable();
        if(_playerForceId==forceId)CloseNewGameSetup();
        else if(_newGameSetup==view)view.SetBusy(false);
    }

    private void ChangeNewGameLanguage(int locale)
    {
        if(_newGameSetup is not { } view||locale==_locale.Selected||locale is <0 or >1)return;
        int[] selected=_scenarioList.GetSelectedItems();
        int number=selected.Length==1?_shownScenarios[selected[0]].Selection.Number:-1;
        int? force=view.SelectedForceId;bool settings=view.IsSettingsPage;
        _newGameOptions=view.Draft;view.SetBusy(true);
        CloseNewGameSetup();_locale.Select(locale);UpdateLocale();RefreshScenarios(number);
        OpenNewGameSetup();
        // Locale owns a new normal preview/publication. Explicit continuation
        // restores the user's draft and selection, unlike a fresh page entry.
        _newGameSetup?.RestoreSelection(force,settings);
    }

    private void CloseNewGameSetup()
    {
        var view=_newGameSetup;_newGameSetup=null;
        if(view is not null&&GodotObject.IsInstanceValid(view))
        { view.Abandon();if(view.GetParent() is Node parent)parent.RemoveChild(view);view.QueueFree(); }
        if(!_failed&&!_shuttingDown&&_scenarioSelection is null)
        { _browserContent.ProcessMode=ProcessModeEnum.Inherit;_browserContent.Show(); }
    }
}
