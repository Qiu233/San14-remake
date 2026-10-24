using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

/// <summary>The normal new-game host retains one force publication while the
/// supported ordinary settings remain a local draft until final confirmation.</summary>
internal partial class NewGameSetupView : Control
{
    private AdministrationPageChrome chrome = null!;
    private NewGameForceSelectionView forcePage = null!;
    private Control settings = null!, languageCanvas = null!;
    private OptionButton language = null!;
    private Label chosenForce = null!;
    private NewGameSettingsView? settingsChoices;
    private Func<bool>? admitted;
    private Func<NewGameForcePreview, LoadedSprite?>? resolveRuler;
    private Action<int>? selectionChanged;
    private Action<int?>? previewChanged;
    private Action? returned;
    private Action<int, NewGameOptions>? accepted;
    private Action<int>? languageChanged;
    private Action<Exception>? failed;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets rulerAssets = null!;
    private Shader rulerShader = null!;
    private ImageTexture? previewMapTexture;
    private GovernanceRosterPage? governmentPage;
    private NewGameForceGovernancePreview? governmentPublication;
    private Func<int, NewGameForceGovernmentContent>? resolveGovernment;
    private NewGameGeographicAdvantagesView? geographicPage;
    private DeploymentTargetConfirmation? abortConfirmation;
    private Action? aborted;
    private NewGameForceOfficersView? officersPage;
    private Func<int, NewGameForceOfficersContent>? resolveOfficers;
    private NewGameForcePickerView? forcePickerPage;
    private Func<int?, NewGameForcePickerPreview>? resolveForcePicker;
    private bool HasModal => governmentPage is not null || geographicPage is not null || abortConfirmation is not null || officersPage is not null || forcePickerPage is not null;
    private bool abandoned, busy;
    internal int? SelectedForceId => forcePage?.SelectedForceId;
    internal NewGameForceSelectionPreview Publication => forcePage.Publication;
    internal bool IsSettingsPage { get; private set; }
    internal NewGameOptions Draft { get; private set; } = null!;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        NewGameForceSelectionPreview publication, NewGameForcePageHeader header,
        IReadOnlyList<NewGameForceFaceContent> faces, Texture2D? map,
        Func<NewGameForcePreview, LoadedSprite?> resolveRuler, NewGameOptions initial, NewGameSettingsPreview settingsPublication,
        Func<bool> admitted, Action<int> selectionChanged, Action<int?> previewChanged,
        Action returned, Action<int, NewGameOptions> accepted, Action<int> languageChanged, Action<Exception> failed)
    {
        this.repository=repository;this.text=text;this.resolveRuler=resolveRuler;Draft=initial;
        this.admitted=admitted;this.selectionChanged=selectionChanged;this.previewChanged=previewChanged;
        this.returned=returned;this.accepted=accepted;this.languageChanged=languageChanged;this.failed=failed;
        Name="NewGameSetup";ZIndex=2;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color=new("#292d2c"), AnchorRight=1, AnchorBottom=1,
            MouseFilter=MouseFilterEnum.Ignore });
        rulerAssets=new(repository);rulerShader=UiSourceSprite.CreateShader();
        forcePage=new NewGameForceSelectionView();AddChild(forcePage);
        chrome=new AdministrationPageChrome { Name="NewGameSettings" };AddChild(chrome);
        chrome.Configure(repository,text,0xC3,RequestBack,RequestConfirm,showFooterHint:true);
        chrome.Canvas.Theme=new Theme { DefaultFont=new SystemFont { FontNames=["Microsoft YaHei","DFKai-SB"] } };
        settings=new Control { Name="SettingsPage",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(settings);
        LabelAt(settings,"ScenarioCaption",header.Title,new(447,770),new(1026,38),28);
        chosenForce=LabelAt(settings,"ChosenForce","",new(447,816),new(1026,38),28);
        ConfigureOptions(settingsPublication);
        // Language remains available on both pages, using the same design canvas.
        languageCanvas=new Control { Name="SetupLanguageCanvas",Size=new(1920,1080),
            ZIndex=3,MouseFilter=MouseFilterEnum.Ignore };AddChild(languageCanvas);
        language=new OptionButton { Name="SetupLanguage",Position=new(1580,105),Size=new(250,45) };
        language.AddItem(text.LocaleSimplified);language.AddItem(text.LocaleTraditional);
        language.Select(text.SourceLocale=="TC"?1:0);languageCanvas.AddChild(language);
        language.ItemSelected+=index=>{if(ReadAdmission())this.languageChanged?.Invoke((int)index);};
        Resized+=FitLanguage;FitLanguage();
        chrome.Hide();
        // The host has already added this view to its live tree. The source
        // page's real default selection must pass the normal admission gate.
        forcePage.Configure(repository,text,publication,header,faces,map,CreateRulerPortrait,
            ()=>ReadAdmission()&&!IsSettingsPage,SelectForce,PreviewForce,
            _=>SelectPage(true),()=>{if(ReadAdmission())this.returned?.Invoke();});
    }

    private void ConfigureOptions(NewGameSettingsPreview publication)
    {
        settingsChoices = new NewGameSettingsView(); settings.AddChild(settingsChoices); settings.MoveChild(settingsChoices,0);
        settingsChoices.Configure(repository, text, chrome, publication, ReadSettingsAdmission,
            options => { if (ReadSettingsAdmission()) Draft = options; });
    }

    private Control? CreateRulerPortrait(NewGameForcePreview row)
    {
        NewGameRulerPortrait? portrait=null;
        try
        {
            if(resolveRuler?.Invoke(row) is not { } sprite)return null;
            portrait=new NewGameRulerPortrait();
            portrait.Configure(repository,rulerAssets,rulerShader,sprite);
            return portrait;
        }
        catch(Exception error)
        {
            portrait?.Free();
            // The normal host reports the resource failure and retires this
            // page. An unresolved route never becomes an accepted empty image.
            if(failed is not { } report)throw;
            report(error);return null;
        }
    }
    private void SelectForce(int id)
    {
        if(!ReadAdmission()||IsSettingsPage)return;
        var row=Publication.ByForceId[id];
        chosenForce.Text=text.Format("new_game_selected_force",row.RulerName??row.ForceDisplayName??text.Get("detail_unknown"));
        selectionChanged?.Invoke(id);
    }
    private void PreviewForce(int? id)
    {if(ReadAdmission()&&!IsSettingsPage)previewChanged?.Invoke(id);}
    // The wrapper owns raster textures; the source view only borrows them.
    internal void ApplyMap(ImageTexture texture)
    {
        if(abandoned){texture.Dispose();return;}
        var previous=previewMapTexture;previewMapTexture=texture;
        forcePage.ApplyMap(texture);previous?.Dispose();
    }

    private bool CanConfirm() => SelectedForceId is int force&&Publication.ByForceId.GetValueOrDefault(force)?.CanConfirm==true;
    private void SelectPage(bool options)
    {
        if(!ReadAdmission()||options&&!CanConfirm())return;
        if(options&&!IsSettingsPage)forcePage.PreviewForce(null);
        IsSettingsPage=options;forcePage.Visible=!options;chrome.Visible=options;
        chrome.ApplyTitle(text.Get("new_game_settings"));
        chrome.ApplyDefaultHint(text.Get("new_game_settings_hint"));
        chrome.ApplyDecision(!busy&&CanConfirm(),true);
    }
    internal void RestoreSelection(int? forceId,bool settingsPage)
    {
        if(!ReadAdmission())return;
        if(IsSettingsPage)SelectPage(false);
        if(forceId is int id)forcePage.SelectForce(id);
        if(settingsPage)SelectPage(true);
    }
    internal void RequestConfirm()
    {
        if(!ReadAdmission()||!CanConfirm())return;
        if(!IsSettingsPage)forcePage.RequestConfirm();
        else if(SelectedForceId is int force)accepted?.Invoke(force,Draft);
    }
    internal void RequestBack()
    {
        if (ReadBaseAdmission() && forcePickerPage is { } forces) { forces.RequestBack(); return; }
        if (ReadBaseAdmission() && officersPage is { } people) { people.RequestBack(); return; }
        if (ReadBaseAdmission() && abortConfirmation is { } question) { question.RequestCancel(); return; }
        if (ReadBaseAdmission() && geographicPage is { } geography) { CloseGeographicAdvantages(geography); return; }
        if(ReadBaseAdmission()&&governmentPage is { } government){CloseGovernment(government);return;}
        if(!ReadAdmission())return;
        if(IsSettingsPage)SelectPage(false);
        else forcePage.RequestBack();
    }
    internal void SetBusy(bool value)
    {
        if(abandoned)return;
        busy=value;forcePage.SetBusy(value||HasModal);chrome.ApplyReturn(!value&&!HasModal);
        chrome.ApplyDecision(!value&&!HasModal&&CanConfirm(),true);language.Disabled=value||HasModal;
        governmentPage?.SetBusy(value);geographicPage?.SetBusy(value);
        officersPage?.SetBusy(value);
        forcePickerPage?.SetBusy(value);
        if(abortConfirmation is { } question)question.ProcessMode=value?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        settingsChoices?.SetBusy(value||HasModal);
    }
    private bool ReadBaseAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private bool ReadAdmission()=>!HasModal&&ReadBaseAdmission();
    private bool ReadSettingsAdmission()=>ReadAdmission()&&IsSettingsPage;
    internal void ConfigureGovernment(Func<int,NewGameForceGovernmentContent> resolver)
    {
        resolveGovernment=resolver;
        forcePage.ConfigureGovernment(OpenGovernment);
    }
    internal void ConfigureNavigation(Action aborted)
    {
        this.aborted = aborted;
        forcePage.ConfigureNavigation(OpenAbortConfirmation, OpenGeographicAdvantages);
    }
    internal void ConfigureOfficers(Func<int, NewGameForceOfficersContent> resolver)
    { resolveOfficers = resolver; forcePage.ConfigureOfficers(OpenOfficers); }
    internal void ConfigureForcePicker(Func<int?, NewGameForcePickerPreview> resolver)
    { resolveForcePicker = resolver; forcePage.ConfigureForceOverview(OpenForcePicker); }
    private void OpenForcePicker()
    {
        if (!ReadAdmission() || IsSettingsPage || resolveForcePicker is null) return;
        NewGameForcePickerView? page = null;
        try
        {
            var content = resolveForcePicker(SelectedForceId);
            page = new NewGameForcePickerView(); forcePickerPage = page; AddChild(page);
            page.Configure(repository, text, content, force =>
            {
                if (abandoned || forcePickerPage != page) return;
                CloseForcePicker(page);
                // Restore the parent's ordinary admission before entering its
                // formal selection path. The picker never writes the world.
                if (ReadAdmission() && force is int id) forcePage.SelectForce(id);
            });
            SetBusy(busy);
        }
        catch (Exception error)
        {
            if (page is not null) CloseForcePicker(page);
            if (failed is not { } report) throw;
            report(error);
        }
    }
    private void CloseForcePicker(NewGameForcePickerView page)
    {
        if (forcePickerPage != page) return;
        forcePickerPage = null; page.Abandon();
        if (page.GetParent() == this) RemoveChild(page);
        page.QueueFree();
        if (!abandoned) SetBusy(busy);
    }
    private void OpenOfficers(int forceId)
    {
        if (!ReadAdmission() || IsSettingsPage || resolveOfficers is null) return;
        NewGameForceOfficersView? page = null;
        try
        {
            var content = resolveOfficers(forceId);
            page = new NewGameForceOfficersView(); officersPage = page; AddChild(page);
            page.Configure(repository, text, content, () => CloseOfficers(page),
                error => { if (failed is { } report) report(error); else throw error; });
            SetBusy(busy);
        }
        catch (Exception error)
        {
            if (page is not null) CloseOfficers(page);
            if (failed is not { } report) throw;
            report(error);
        }
    }
    private void CloseOfficers(NewGameForceOfficersView page)
    {
        if (officersPage != page) return;
        officersPage = null; page.Abandon();
        if (page.GetParent() == this) RemoveChild(page);
        page.QueueFree();
        if (!abandoned) SetBusy(busy);
    }
    private void OpenAbortConfirmation()
    {
        if (!ReadAdmission() || IsSettingsPage) return;
        var question = new DeploymentTargetConfirmation(); abortConfirmation = question; AddChild(question);
        try
        {
            SetBusy(busy);
            question.ConfigureGameMessage(repository, text, 0x273F, confirmed =>
            {
                if (abandoned || abortConfirmation != question) return;
                CloseAbortConfirmation(question);
                if (confirmed && ReadBaseAdmission()) aborted?.Invoke();
            });
            question.Name = "NewGameAbortConfirmation";
        }
        catch { CloseAbortConfirmation(question); throw; }
    }
    private void CloseAbortConfirmation(DeploymentTargetConfirmation question)
    {
        if (abortConfirmation != question) return;
        abortConfirmation = null; question.Abandon();
        if (question.GetParent() == this) RemoveChild(question);
        question.QueueFree();
        if (!abandoned) SetBusy(busy);
    }
    private void OpenGeographicAdvantages(NewGameForcePreview publication)
    {
        if (!ReadAdmission() || IsSettingsPage || publication.CanOpenGeographicAdvantages != true) return;
        var page = new NewGameGeographicAdvantagesView(); geographicPage = page; AddChild(page);
        try
        {
            page.Configure(repository, text, publication, () => CloseGeographicAdvantages(page));
            SetBusy(busy);
        }
        catch (Exception error)
        {
            CloseGeographicAdvantages(page);
            if (failed is not { } report) throw;
            report(error);
        }
    }
    private void CloseGeographicAdvantages(NewGameGeographicAdvantagesView page)
    {
        if (geographicPage != page) return;
        geographicPage = null; page.Abandon();
        if (page.GetParent() == this) RemoveChild(page);
        page.QueueFree();
        if (!abandoned) SetBusy(busy);
    }
    private void OpenGovernment(int forceId)
    {
        if(!ReadAdmission()||IsSettingsPage||resolveGovernment is null)return;
        GovernanceRosterPage? page=null;
        try
        {
            var content=resolveGovernment(forceId);
            governmentPublication=content.Source;
            page=new GovernanceRosterPage();governmentPage=page;AddChild(page);
            page.Configure(repository,text,chrome.Canvas.Theme,content.Roster,
                ()=>CloseGovernment(page),()=>CloseGovernment(page),_=>{});
            page.ApplyEvaluation(content.Evaluation);
            page.SetMeta("new_game_read_only",true);page.SetMeta("force_id",forceId);
            SetBusy(busy);
        }
        catch(Exception error)
        {
            if(page is not null)CloseGovernment(page);
            if(failed is not { } report)throw;
            report(error);
        }
    }
    private void CloseGovernment(GovernanceRosterPage page)
    {
        if(governmentPage!=page)return;
        governmentPage=null;governmentPublication=null;page.Abandon();
        if(page.GetParent()==this)RemoveChild(page);
        page.QueueFree();
        if(!abandoned)SetBusy(busy);
    }
    internal void Abandon()
    {
        if(abandoned)return;
        abandoned=true;ClearCallbacks();
        if(forcePickerPage is { } forces)CloseForcePicker(forces);
        if(officersPage is { } people)CloseOfficers(people);
        if(abortConfirmation is { } question)CloseAbortConfirmation(question);
        if(geographicPage is { } geography)CloseGeographicAdvantages(geography);
        if(governmentPage is { } government)CloseGovernment(government);
        forcePage?.Abandon();chrome?.Abandon();
        previewMapTexture?.Dispose();previewMapTexture=null;
        settingsChoices?.Abandon();
        if(language is not null)language.Disabled=true;
        Hide();
    }
    private void ClearCallbacks()
    {admitted=null;resolveRuler=null;resolveGovernment=null;resolveOfficers=null;resolveForcePicker=null;aborted=null;selectionChanged=null;previewChanged=null;returned=null;accepted=null;languageChanged=null;failed=null;}
    public override void _ExitTree()
    {
        ClearCallbacks();forcePage?.ApplyMap(null);previewMapTexture?.Dispose();previewMapTexture=null;
        rulerAssets?.Dispose();rulerShader?.Dispose();
    }
    private void FitLanguage()
    {
        if(languageCanvas is null)return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);
        languageCanvas.Scale=Vector2.One*scale;languageCanvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    private static Label LabelAt(Control parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
    {
        var label=new Label { Name=name,Text=value,Position=position,Size=size,AutowrapMode=TextServer.AutowrapMode.WordSmart,
            VerticalAlignment=VerticalAlignment.Top,MouseFilter=MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",fontSize);parent.AddChild(label);return label;
    }
}
