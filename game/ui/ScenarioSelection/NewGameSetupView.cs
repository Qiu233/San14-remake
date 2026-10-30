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
    private Control settings = null!;
    private NewGameSettingsView? settingsChoices;
    private UiSourceRoundButton settingsAbort = null!;
    private NewGameForcePageHeader scenarioHeader = null!;
    private NewGameConfirmationView? confirmationPage;
    private Func<NewGameForcePreview, LoadedSprite>? resolveConfirmationRuler;
    private Func<NewGameConfirmationExtras>? resolveConfirmationExtras;
    private Func<int>? confirmationScrollSpeed;
    internal bool IsConfirmationPage => confirmationPage is not null;
    private Func<bool>? admitted;
    private Func<NewGameForcePreview, LoadedSprite?>? resolveRuler;
    private Action<int>? selectionChanged;
    private Action<int?>? previewChanged;
    private Action? returned;
    private Action<int, NewGameOptions>? accepted;
    private Action<Exception>? failed;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets rulerAssets = null!;
    private MapFont? pageFont;
    private Shader rulerShader = null!;
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
        Action returned, Action<int, NewGameOptions> accepted, Action<Exception> failed,
        Action<string>? diagnostic = null)
    {
        Mark(diagnostic,"setup-view-begin");
        this.repository=repository;this.text=text;this.resolveRuler=resolveRuler;Draft=initial;scenarioHeader=header;
        this.admitted=admitted;this.selectionChanged=selectionChanged;this.previewChanged=previewChanged;
        this.returned=returned;this.accepted=accepted;this.failed=failed;
        Name="NewGameSetup";ZIndex=2;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color=new("#292d2c"), AnchorRight=1, AnchorBottom=1,
            MouseFilter=MouseFilterEnum.Ignore });
        rulerAssets=new(repository);rulerShader=UiSourceSprite.CreateShader();
        pageFont=new(repository,text.SourceLocale);
        var resources=new AdministrationPageChrome.Resources(rulerAssets,pageFont,rulerShader);
        forcePage=new NewGameForceSelectionView();AddChild(forcePage);
        Mark(diagnostic,"setup-settings-chrome-begin");
        chrome=new AdministrationPageChrome { Name="NewGameSettings" };AddChild(chrome);
        chrome.Configure(repository,text,0xBB,RequestBack,RequestConfirm,showFooterHint:true,threeButtonFooter:true,resources:resources);
        chrome.Canvas.Theme=new Theme();
        var messages = new AdministrationMessages(repository, text.SourceLocale);
        chrome.ApplyDefaultHint(messages.Format(0x2978));
        chrome.ApplyReturnHint(messages.Format(0x297A));
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        settingsAbort = new UiSourceRoundButton { Name = "AbortNewGameSettings",
            Position = footer.Rectangle(3, new(0, 904)).Position, InputAdmission = ReadSettingsAdmission };
        chrome.Canvas.AddChild(settingsAbort);
        settingsAbort.ConfigureAbort(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale, OpenAbortConfirmation);
        settingsAbort.MouseEntered += () => { if (ReadSettingsAdmission()) chrome.ApplyHoverHint(messages.Format(0x297B)); };
        settingsAbort.MouseExited += () => chrome.ApplyHoverHint(null);
        settings=new Control { Name="SettingsPage",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(settings);
        Mark(diagnostic,"setup-settings-chrome-end");
        Mark(diagnostic,"setup-settings-rows-begin");
        ConfigureOptions(settingsPublication);
        Mark(diagnostic,"setup-settings-rows-end");
        chrome.Hide();
        // The host has already added this view to its live tree. The source
        // page's real default selection must pass the normal admission gate.
        Mark(diagnostic,"setup-force-page-begin");
        forcePage.Configure(repository,text,publication,header,faces,map,CreateRulerPortrait,
            ()=>ReadAdmission()&&!IsSettingsPage,SelectForce,PreviewForce,
            _=>SelectPage(true),()=>{if(ReadAdmission())this.returned?.Invoke();},resources);
        Mark(diagnostic,"setup-force-page-end");

        // The host may attach its opt-in request clock. Diagnostics neither
        // survive Configure nor become a prerequisite for a normal page.
        static void Mark(Action<string>? diagnostic, string stage)
        {
            try { diagnostic?.Invoke(stage); }
            catch(Exception) { }
        }
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
        selectionChanged?.Invoke(id);
    }
    private void PreviewForce(int? id)
    {if(ReadAdmission()&&!IsSettingsPage)previewChanged?.Invoke(id);}

    private bool CanConfirm() => SelectedForceId is int force&&Publication.ByForceId.GetValueOrDefault(force)?.CanConfirm==true;
    private void SelectPage(bool options)
    {
        if(!ReadAdmission()||options&&!CanConfirm())return;
        if(options&&!IsSettingsPage)forcePage.PreviewForce(null);
        IsSettingsPage=options;forcePage.Visible=!options;chrome.Visible=options;
        chrome.ApplyDecision(!busy&&CanConfirm(),true);
    }
    internal void RestoreSelection(int? forceId,bool settingsPage)
    {
        if(!ReadAdmission())return;
        if (confirmationPage is not null) { CloseConfirmation(); return; }
        if(IsSettingsPage)SelectPage(false);
        if(forceId is int id)forcePage.SelectForce(id);
        if(settingsPage)SelectPage(true);
    }
    internal void RequestConfirm()
    {
        if(!ReadAdmission()||!CanConfirm())return;
        if (confirmationPage is { } review) review.RequestConfirm();
        else if(!IsSettingsPage)forcePage.RequestConfirm();
        else OpenConfirmation();
    }
    internal void RequestBack()
    {
        if (ReadBaseAdmission() && forcePickerPage is { } forces) { forces.RequestBack(); return; }
        if (ReadBaseAdmission() && officersPage is { } people) { people.RequestBack(); return; }
        if (ReadBaseAdmission() && abortConfirmation is { } question) { question.RequestCancel(); return; }
        if (ReadBaseAdmission() && geographicPage is { } geography) { CloseGeographicAdvantages(geography); return; }
        if(ReadBaseAdmission()&&governmentPage is { } government){CloseGovernment(government);return;}
        if(!ReadAdmission())return;
        if (confirmationPage is not null) { CloseConfirmation(); return; }
        if(IsSettingsPage)SelectPage(false);
        else forcePage.RequestBack();
    }
    internal void SetBusy(bool value)
    {
        if(abandoned)return;
        busy=value;forcePage.SetBusy(value||HasModal||mapLoading is not null);chrome.ApplyReturn(!value&&!HasModal);
        settingsAbort.Disabled=value||HasModal;
        chrome.ApplyDecision(!value&&!HasModal&&CanConfirm(),true);
        governmentPage?.SetBusy(value);geographicPage?.SetBusy(value);
        officersPage?.SetBusy(value);
        forcePickerPage?.SetBusy(value);
        if(abortConfirmation is { } question)question.ProcessMode=value?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        settingsChoices?.SetBusy(value||HasModal);
        confirmationPage?.SetBusy(value||HasModal);
    }
    private bool ReadBaseAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private bool ReadAdmission()=>mapLoading is null&&!HasModal&&ReadBaseAdmission();
    private bool ReadSettingsAdmission()=>ReadAdmission()&&IsSettingsPage&&!IsConfirmationPage;
    internal void ConfigureConfirmation(Func<NewGameForcePreview, LoadedSprite> ruler,
        Func<NewGameConfirmationExtras> extras, Func<int> scrollSpeed)
    { resolveConfirmationRuler = ruler; resolveConfirmationExtras = extras; confirmationScrollSpeed = scrollSpeed; }
    private void OpenConfirmation()
    {
        if (!ReadSettingsAdmission() || !CanConfirm() || SelectedForceId is not int force || settingsChoices is null) return;
        if (resolveConfirmationRuler is null || resolveConfirmationExtras is null || confirmationScrollSpeed is null)
            throw new InvalidOperationException("The new-game confirmation owner is missing.");
        NewGameConfirmationView? page = null;
        try
        {
            var ruler = resolveConfirmationRuler(Publication.ByForceId[force]);
            var extras = resolveConfirmationExtras();
            var rows = settingsChoices.CaptureConfirmationSettings();
            page = new NewGameConfirmationView(); confirmationPage = page; AddChild(page);
            page.Configure(repository, text, scenarioHeader, Publication.ByForceId[force], ruler, rows, extras,
                () => ReadAdmission() && confirmationPage == page,
                () => { if (ReadAdmission() && confirmationPage == page) CloseConfirmation(); },
                () => { if (ReadAdmission() && confirmationPage == page && SelectedForceId == force) accepted?.Invoke(force, Draft); },
                OpenAbortConfirmation, confirmationScrollSpeed);
            chrome.Hide();
        }
        catch (Exception error)
        {
            if (page is not null) CloseConfirmation();
            if (failed is not { } report) throw;
            report(error);
        }
    }
    private void CloseConfirmation()
    {
        var page = confirmationPage; confirmationPage = null;
        if (page is not null)
        {
            page.Abandon(); if (page.GetParent() == this) RemoveChild(page); page.QueueFree();
        }
        if (!abandoned) { chrome.Show(); SetBusy(busy); }
    }
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
            },confirmationScrollSpeed,()=>ReadBaseAdmission()&&forcePickerPage==page);
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
        if (!ReadAdmission()) return;
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
        CloseConfirmation();
        if(forcePickerPage is { } forces)CloseForcePicker(forces);
        if(officersPage is { } people)CloseOfficers(people);
        if(abortConfirmation is { } question)CloseAbortConfirmation(question);
        if(geographicPage is { } geography)CloseGeographicAdvantages(geography);
        if(governmentPage is { } government)CloseGovernment(government);
        mapLoading?.Abandon();
        forcePage?.Abandon();chrome?.Abandon();
        settingsChoices?.Abandon();
        Hide();
    }
    private void ClearCallbacks()
    {resolveConfirmationRuler=null;resolveConfirmationExtras=null;confirmationScrollSpeed=null;admitted=null;resolveRuler=null;resolveGovernment=null;resolveOfficers=null;resolveForcePicker=null;aborted=null;selectionChanged=null;previewChanged=null;returned=null;accepted=null;failed=null;}
    public override void _ExitTree()
    {
        ClearCallbacks();forcePage?.ApplyMap(null);
        // Children retire their drawing owners before this page releases borrowed resources.
        pageFont?.Dispose();pageFont=null;
        rulerAssets?.Dispose();rulerAssets=null!;rulerShader?.Dispose();rulerShader=null!;
    }
}
