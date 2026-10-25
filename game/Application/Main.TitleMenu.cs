using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private TitleMenuView? _titleMenu;
    private DeploymentTargetConfirmation? _titleConfirmation;

    private bool[] TitleAvailability() =>
        [CampaignSaves.HasAnyCampaign,
         true,false,false,_ui.SourceLocale == "TC",true,true,true];

    private void OpenTitleMenu()
    {
        if(_failed||_shuttingDown||_titleMenu is not null||_repository is null||_playerForceId.HasValue)return;
        CaptureApplicationCrtRandom();
        CloseNewGameSetup();CloseScenarioSelection();LeaveSceneAudioContext();
        // A title is not a preview campaign. NewGame and Load establish their
        // own actual session only when the corresponding operation succeeds.
        _session=null;_campaignComposition=null;_selectedForceId=null;_selectedCell=null;
        var view=new TitleMenuView();_titleMenu=view;AddChild(view);
        try
        {
            view.Configure(_repository,_ui,TitleAvailability(),
                ()=>!_failed&&!_shuttingDown&&_titleMenu==view&&!HasCampaignSavePage&&!HasEnvironmentSettings&&!HasTitleEditMenu&&!HasCollectionMenu&&_titleConfirmation is null,
                command=>RunUiAction(()=>ExecuteTitleCommand(command)),
                locale=>RunUiAction(()=>ChangeTitleLanguage(locale)));
            _browserContent.Hide();_browserContent.ProcessMode=ProcessModeEnum.Disabled;
        }
        catch { CloseTitleMenu();throw; }
    }

    private void ExecuteTitleCommand(TitleMenuCommand command)
    {
        if(_titleMenu is not { } view||HasCampaignSavePage||HasEnvironmentSettings||HasTitleEditMenu||HasCollectionMenu||_titleConfirmation is not null)return;
        switch(command)
        {
            case TitleMenuCommand.NewGame:
                // Tutorial induction is deferred with the unavailable tutorial
                // route. Do not fabricate acknowledged/completed preferences.
                CloseTitleMenu();OpenScenarioSelection();break;
            case TitleMenuCommand.Load:
                OpenCampaignSaves(false);if(HasCampaignSavePage)view.Hide();break;
            case TitleMenuCommand.Settings:
                OpenEnvironmentSettings();break;
            case TitleMenuCommand.Edit:
                OpenTitleEditMenu();break;
            case TitleMenuCommand.Collection:
                OpenCollectionMenu();break;
            case TitleMenuCommand.Exit:
                var question=new DeploymentTargetConfirmation();_titleConfirmation=question;AddChild(question);
                try
                {
                    question.ConfigureGameMessage(_repository!,_ui,0x2727,confirmed=>RunUiAction(()=>
                    {
                        if(_titleMenu!=view||_titleConfirmation!=question)return;
                        _titleConfirmation=null;question.Abandon();question.QueueFree();
                        if(confirmed)WindowCloseRequested();
                    }));
                    question.Name="TitleExitConfirmation";
                }
                catch { _titleConfirmation=null;question.Abandon();question.QueueFree();throw; }
                break;
        }
    }

    private void ChangeTitleLanguage(int locale)
    {
        if(_titleMenu is null||HasTitleEditMenu||HasCollectionMenu||locale==_locale.Selected||locale is <0 or >1)return;
        CloseTitleMenu();_locale.Select(locale);UpdateLocale();RefreshScenarios(loadPreview:false);OpenTitleMenu();
    }

    private bool ResumeTitleMenu()
    {
        if(_failed||_shuttingDown||_titleMenu is not { } view||_playerForceId.HasValue||
            HasCampaignSavePage||HasEnvironmentSettings||HasTitleEditMenu||HasCollectionMenu||_titleConfirmation is not null)return false;
        view.ApplyAvailability(TitleAvailability());view.Show();return true;
    }

    private void CloseTitleMenu()
    {
        CloseTitleEditMenu(resumeTitle: false);
        CloseCollectionMenu(resumeTitle: false);
        var question=_titleConfirmation;_titleConfirmation=null;
        if(question is not null){question.Abandon();question.QueueFree();}
        var view=_titleMenu;_titleMenu=null;
        if(view is not null&&GodotObject.IsInstanceValid(view))
        {view.Abandon();if(view.GetParent() is Node parent)parent.RemoveChild(view);view.QueueFree();}
    }
}
