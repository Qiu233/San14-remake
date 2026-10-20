using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

/// <summary>Owns one Domestic draft and its child snapshot. Only the host's
/// final commit callback can submit an order to the campaign session.</summary>
internal sealed class AreaAdministrationPresenter : IDisposable
{
    private readonly AreaAdministrationParentView parent;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private readonly AreaAdministrationDraft draft;
    private readonly AdministrationMessages messages;
    private readonly AreaAdministrationOfficerContent officers;
    private readonly Func<bool> current;
    private readonly Action<Action> dispatch;
    private readonly Action returned, commitRequested;
    private readonly Action<bool> mapInputChanged;
    private readonly Action<string> unavailable;
    private readonly Func<int,int> presentationRandom;
    private AreaAdministrationView? child;
    private DeploymentTargetConfirmation? confirmation;
    private UiSourcePulldownMenu? automaticMenu;
    private bool disposed, busy;
    private int? speechPersonPhysical;
    private bool speechPointerUnavailable;

    internal AreaAdministrationDraft Draft => draft;
    internal bool HasChild => child is not null;

    internal AreaAdministrationPresenter(AreaAdministrationParentView parent,
        GameResourceRepository repository, PreviewText text, AreaAdministrationDraft draft,
        Func<bool> current, Action<Action> dispatch, Action returned, Action commitRequested,
        Action<bool> mapInputChanged, Action<string> unavailable, Func<int,int> presentationRandom,Func<int>? scrollSpeed=null)
    {
        this.parent=parent;this.repository=repository;this.text=text;this.draft=draft;
        this.current=current;this.dispatch=dispatch;this.returned=returned;
        this.commitRequested=commitRequested;this.mapInputChanged=mapInputChanged;this.unavailable=unavailable;
        this.presentationRandom=presentationRandom;
        messages=new(repository,text.SourceLocale);officers=new(repository);
        parent.Configure(repository,text,Entries(),scrollSpeed?.Invoke()??0,
            area=>Run(()=>OpenArea(area)),()=>Run(RequestBack),()=>Run(RequestCommit),
            _=>UpdateMapInput(),Order,unavailable,autoRequested:()=>Run(RequestAutomatic),
            dismissRequested:()=>Run(RequestDismissAll),currentScrollSpeed:scrollSpeed);
        RefreshParent();
    }

    private bool Active=>!disposed&&current();
    private void Run(Action action)=>dispatch(()=>{if(Active&&!busy)action();});
    private void Unknown(string? reason)=>unavailable(reason??"area-draft.unavailable");
    private bool Accept(AreaAdministrationDraftResult result)
    {if(result.Available)return true;Unknown(result.UnresolvedInput);return false;}

    private IReadOnlyList<AreaAdministrationListEntry> Entries()
    {
        var query=draft.QueryAreas();
        if(!query.Available)
        {
            Unknown(query.UnresolvedInput);
            return draft.HeldAreaIds.Select(id=>new AreaAdministrationListEntry(id,new(false,null),
                Enumerable.Repeat(text.Get("detail_unknown"),AreaAdministrationListContent.Columns.Count).ToArray())).ToArray();
        }
        return AreaAdministrationListContent.Create(repository,text,query.Value!,area=>
        {
            var place=draft.QueryAreaPlaceKind(area);
            AreaPlaceKind? kind=place.Value switch
            {AreaAdministrationPlaceKind.City=>AreaPlaceKind.City,AreaAdministrationPlaceKind.Gate=>AreaPlaceKind.Gate,
                AreaAdministrationPlaceKind.Port=>AreaPlaceKind.Port,_=>null};
            return new(place.Available,kind);
        });
    }

    private AreaAdministrationListOrder Order(IReadOnlyList<int> input,int column,bool ascending)
    {
        AreaAdministrationSortColumn key=column switch
        {-1=>AreaAdministrationSortColumn.Place,0=>AreaAdministrationSortColumn.AreaName,5=>AreaAdministrationSortColumn.Officer,
            6=>AreaAdministrationSortColumn.CommerceLevel,7=>AreaAdministrationSortColumn.CommerceExperience,
            8=>AreaAdministrationSortColumn.AgricultureLevel,9=>AreaAdministrationSortColumn.AgricultureExperience,
            10=>AreaAdministrationSortColumn.BarracksLevel,11=>AreaAdministrationSortColumn.BarracksExperience,
            14=>AreaAdministrationSortColumn.Security,4=>AreaAdministrationSortColumn.OwnedLand,12=>AreaAdministrationSortColumn.Focus,
            _=>throw new InvalidDataException("Unknown Domestic list column.")};
        var result=draft.QueryAreaOrder(input,key,ascending);
        return new(result.Available?result.Value:null,result.UnresolvedInput);
    }

    internal void RefreshParent()
    {
        if(!Active)return;
        parent.ApplyEntries(Entries());
        var status=draft.QueryParentStatus();var economy=draft.QueryParentEconomy();
        var books=status.Available?status.Value!.CommandBooks:null;
        var money=economy.Available?economy.Value:null;
        parent.ApplyHeader(new(Value(books?.Current),Value(books?.SourceHeaderAfter),books?.SourceHeaderDifference,
            Value(money?.CurrentUpkeepPerPeriod),Value(money?.CityGold)?.ToString(CultureInfo.InvariantCulture)));
        var dismiss=draft.QueryCanDismissAll();var automatic=draft.QueryCanAutomatic();
        parent.ApplyCommands(CanCommit(status),automatic.Available&&automatic.Value==true,dismiss.Available&&dismiss.Value==true);
        parent.ApplyHint(status.Available && books?.Current is {Available:true,Value:int currentBooks} &&
            status.Value!.ChangedAreaCount>currentBooks ? messages.Format(0x57C7) : null);
        if(!dismiss.Available){parent.ApplyHint(text.Get("detail_unknown"));Unknown(dismiss.UnresolvedInput);}
        UpdateMapInput();
    }
    private static int? Value(AreaAdministrationQuery<int?>? query)=>query?.Available==true?query.Value:null;
    private static bool CanCommit(AreaAdministrationQuery<AreaAdministrationParentStatus> query)=>
        query.Available&&query.Value is {ChangedAreaCount:>0} status&&
        status.CommandBooks.Current.Available&&status.CommandBooks.Current.Value is int books&&status.ChangedAreaCount<=books;

    internal bool AllowMapPointer(int hex)
    {
        if(!Active||busy||child is not null||confirmation is not null||automaticMenu is not null||!parent.IsMapMode)return false;
        var target=draft.QueryMapTarget(hex);
        return target.Available&&target.Value is {AreaId:not null,IsHeld:true};
    }
    internal void PickMapTarget(int hex)=>Run(()=>
    {
        if(!AllowMapPointer(hex))return;
        OpenArea(draft.QueryMapTarget(hex).Value!.AreaId!.Value);
    });

    private void OpenArea(int area)
    {
        if(child is not null||confirmation is not null||automaticMenu is not null||!Accept(draft.BeginArea(area)))return;
        speechPersonPhysical=null;speechPointerUnavailable=false;
        var view=new AreaAdministrationView();child=view;parent.SetSuspended(true);parent.GetParent().AddChild(view);
        view.Configure(repository,text,()=>Run(ReturnChild),()=>Run(AcceptChild),
            ()=>Run(()=>{if(Accept(draft.Dismiss()))RefreshChildAfterOfficerChange();}),
            ()=>Run(()=>Unknown(draft.QueryPersonCandidates().UnresolvedInput)),
            focus=>Run(()=>
            {
                var chosen=focus switch{1=>AreaDevelopmentFocus.Commerce,2=>AreaDevelopmentFocus.Agriculture,
                    3=>AreaDevelopmentFocus.Barracks,_=>throw new InvalidDataException("Unknown regional focus control.")};
                if(Accept(draft.StageFocus(chosen)))RefreshChild();
            }));
        RefreshChild();UpdateMapInput();
    }
    private void RefreshChild()
    {
        if(!Active||child is null)return;
        var row=draft.QueryAreas().Value?.FirstOrDefault(r=>r.AreaId==draft.ActiveAreaId);
        var panelQuery=draft.QueryPanel();var panel=panelQuery.Available?panelQuery.Value:null;
        var status=draft.QueryChildStatus();var card=draft.QueryOfficerCard();
        string Name(AreaAdministrationQuery<string>? value)=>value?.Available==true?value.Value??"":text.Get("detail_unknown");
        string areaName=Name(row?.AreaName);
        child.ApplySummary(new(areaName,Name(row?.CityName),panel?.Population,Value(panel?.OccupationExtent),
            panel?.HighlightOccupationExtent is {Available:true,Value:true},
            new(Value(panel?.GoldBefore),Value(panel?.GoldAfter)),new(Value(panel?.FoodBefore),Value(panel?.FoodAfter)),
            new(Value(panel?.SoldiersBefore),Value(panel?.SoldiersAfter)),new(Value(panel?.SecurityBefore),Value(panel?.SecurityAfter))));
        var options=panel?.FocusOptions;
        bool? Enabled(AreaDevelopmentFocus focus)=>options?.FirstOrDefault(o=>o.Focus==focus)?.Enabled;
        child.ApplyFocus(new(panel is null?null:FocusControl(panel.Focus),Enabled(AreaDevelopmentFocus.Commerce),
            Enabled(AreaDevelopmentFocus.Agriculture),Enabled(AreaDevelopmentFocus.Barracks)));
        int? Gain(AreaDevelopmentFocus focus)=>panel?.Focus==focus?Value(panel.DevelopmentIncrease):null;
        child.ApplyDevelopment(new(row?.Commerce,row?.Agriculture,row?.Barracks,panel is null?null:FocusControl(panel.Focus),
            Gain(AreaDevelopmentFocus.Commerce),Gain(AreaDevelopmentFocus.Agriculture),Gain(AreaDevelopmentFocus.Barracks)));
        child.ApplyOfficer(officers.Create(card.Available?card.Value:null,lookup=>draft.QueryOfficerTraitTooltip(lookup)),
            draft.QueryPersonCandidates().Available);
        child.ApplyDecision(status.Available&&status.Value!.CanAccept is {Available:true,Value:true});
        bool canDismiss=status.Available&&status.Value!.CanDismiss is {Available:true,Value:true};
        string? dismissalHint=status.Available&&status.Value!.CanDismiss.Available?
            canDismiss?messages.Format(0x56D0,areaName,card.Value?.Name??text.Get("detail_unknown")):messages.Format(0x56D1):null;
        child.ApplyDismissal(canDismiss,dismissalHint);
        child.ApplyDefaultHint(messages.Format(0x56CF,areaName));
        string? Hint(AreaDevelopmentFocus focus,int index)
        {
            var option=options?.FirstOrDefault(o=>o.Focus==focus);
            return option?.Enabled==true?messages.Format(0x56D6+index):option?.Progress>999?messages.Format(0x56D3+index):null;
        }
        child.ApplyFocusHints(Hint(AreaDevelopmentFocus.Commerce,0),Hint(AreaDevelopmentFocus.Agriculture,1),Hint(AreaDevelopmentFocus.Barracks,2));
        RefreshSpeech();
    }
    private void RefreshChildAfterOfficerChange()
    {
        // Only a real person mutation can retry a failed pointer resolution.
        // Keep a known physical pointer so selecting that same person again
        // still follows the original early-return rule.
        speechPointerUnavailable=false;RefreshChild();
    }
    private void RefreshSpeech()
    {
        if(child is null||speechPointerUnavailable)return;
        var prepared=draft.PrepareOfficerSpeech(text.SourceLocale,speechPersonPhysical,presentationRandom);
        if(!prepared.Available)
        {
            speechPointerUnavailable=true;child.ApplySpeechUnknown(text.Get("detail_unknown"));return;
        }
        var refresh=prepared.Value!;speechPersonPhysical=refresh.PersonPhysicalSlot;
        if(refresh.Update is not {} update)return;
        if(!update.Available){child.ApplySpeechUnknown(text.Get("detail_unknown"));return;}
        try { child.ApplySpeech(update.Value?.Text); }
        catch(InvalidOperationException) { child.ApplySpeechUnknown(text.Get("detail_unknown")); }
        catch(InvalidDataException) { child.ApplySpeechUnknown(text.Get("detail_unknown")); }
    }
    private void ReturnChild(){if(Accept(draft.CancelArea()))CloseChild();}
    private static int FocusControl(AreaDevelopmentFocus focus)=>focus switch
    {AreaDevelopmentFocus.Commerce=>1,AreaDevelopmentFocus.Agriculture=>2,AreaDevelopmentFocus.Barracks=>3,
        AreaDevelopmentFocus.None=>0,_=>throw new InvalidDataException("Unknown regional focus value.")};
    private void AcceptChild()
    {
        var status=draft.QueryChildStatus();
        if(status.Available&&status.Value!.CanAccept is {Available:true,Value:true}&&Accept(draft.AcceptArea()))CloseChild();
    }
    private void CloseChild()
    {
        var old=child;child=null;speechPersonPhysical=null;speechPointerUnavailable=false;
        old?.Abandon();old?.QueueFree();parent.SetSuspended(false);RefreshParent();
    }

    internal void RequestBack()
    {
        if(!Active||busy)return;
        if(automaticMenu is not null){CloseAutomaticMenu();return;}
        if(confirmation is { } dialog){dialog.RequestCancel();return;}
        if(child is not null){ReturnChild();return;}
        var status=draft.QueryParentStatus();
        if(!status.Available){Unknown(status.UnresolvedInput);return;}
        if(!status.Value!.HasChanges){returned();return;}
        var confirm=new DeploymentTargetConfirmation();confirmation=confirm;parent.SetInputBlocked(true);
        parent.GetParent().AddChild(confirm);UpdateMapInput();
        confirm.Configure(repository,text,DeploymentTargetConfirmationKind.Discard,value=>Run(()=>
        {
            if(confirmation!=confirm)return;
            confirmation=null;confirm.Abandon();confirm.QueueFree();parent.SetInputBlocked(false);UpdateMapInput();
            if(value&&Accept(draft.CancelParent()))returned();
        }));
    }
    private void RequestCommit()
    {if(child is null&&confirmation is null&&automaticMenu is null&&CanCommit(draft.QueryParentStatus()))commitRequested();}
    internal bool TryHandleAutomaticCancelInput(InputEvent input)=>
        Active&&automaticMenu?.TryHandleCancelInput(input)==true;
    private void RequestAutomatic()
    {
        if(!Active||busy||child is not null||confirmation is not null||automaticMenu is not null)return;
        var canAutomatic=draft.QueryCanAutomatic();
        if(!canAutomatic.Available){Unknown(canAutomatic.UnresolvedInput);RefreshParent();return;}
        if(canAutomatic.Value!=true)return;
        UiSourcePulldownMenu? owner=null;
        owner=parent.CreateAutomaticMenu(repository,text,selection=>Run(()=>
        {
            if(owner is null||automaticMenu!=owner)return;
            // The source caller stages the chosen mode before hiding its menu.
            if(selection is int mode)
            {
                try { Accept(draft.StageAutomatic((AreaAdministrationAutomaticMode)mode)); }
                finally { CloseAutomaticMenu(); }
                RefreshParent();
            }
            else CloseAutomaticMenu();
        }));
        automaticMenu=owner;parent.SetInputBlocked(true);UpdateMapInput();
    }
    private void CloseAutomaticMenu()
    {
        var old=automaticMenu;automaticMenu=null;old?.Abandon();old?.QueueFree();
        parent.SetInputBlocked(busy);UpdateMapInput();
    }
    private void RequestDismissAll()
    {
        if(!Active||busy||child is not null||confirmation is not null||automaticMenu is not null)return;
        var canDismiss=draft.QueryCanDismissAll();
        if(!canDismiss.Available){Unknown(canDismiss.UnresolvedInput);return;}
        if(canDismiss.Value==true&&Accept(draft.DismissAll()))RefreshParent();
    }
    internal void SetBusy(bool value)
    {if(!Active)return;busy=value;if(value)CloseAutomaticMenu();parent.SetInputBlocked(value||automaticMenu is not null);UpdateMapInput();}
    internal void ApplyNotice(string message)
    {if(!Active)return;if(child is not null)child.ApplyDefaultHint(message);else parent.ApplyHint(message);}
    private void UpdateMapInput()
    {if(!disposed)mapInputChanged(Active&&!busy&&child is null&&confirmation is null&&automaticMenu is null&&parent.IsMapMode);}
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        CloseAutomaticMenu();
        var old=child;child=null;old?.Abandon();old?.QueueFree();
        var dialog=confirmation;confirmation=null;dialog?.Abandon();dialog?.QueueFree();
        draft.Dispose();parent.Abandon();
    }
}
