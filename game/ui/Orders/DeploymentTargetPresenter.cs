using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns the unsubmitted ordinary target interaction. It queries current
/// permissions, routes and confirmation rules; only its caller may submit.</summary>
internal sealed partial class DeploymentTargetPresenter : IDisposable
{
    private readonly DeploymentTargetView view;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int source,force;
    private readonly Func<int,Vector2?> project;
    private readonly Action<IReadOnlyList<DeploymentDraftRow>> accepted;
    private readonly Action back;
    private readonly Action<string> unavailable;
    private readonly Action<bool> modalChanged;
    private readonly Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged;
    private DeploymentTargetMenuQuery targetMenuQuery;
    private ArmyCommandMenu? menu;
    private DeploymentPolicyView? policies;
    private readonly Func<DeploymentBehavior,Action<GameState>,bool>? saveDefaults;
    private readonly Action<DeploymentAction,int,Action<DeploymentAction>>? chooseFalseReportDestination;
    private DeploymentPolicyOptions? policyOptions;
    private GameState? policyWorld;
    private IReadOnlyList<int> policySelection=[];
    private DeploymentTargetConfirmation? confirmation;
    private int? previewHex;
    private bool disposed;
    private UnitTargetMenuMessages? sourceMessages;
    internal DeploymentTargetDraft Draft {get;}

    internal DeploymentTargetPresenter(DeploymentTargetView view,GameResourceRepository repository,PreviewText text,
        GameState world,ArmyCampaignRuleAdapter rules,int source,int force,IReadOnlyList<DeploymentDraftRow> rows,
        Func<int,Vector2?> project,Func<int,Vector2?> projectMarker,Action<IReadOnlyList<DeploymentDraftRow>> accepted,Action back,
        Action<string> unavailable,Action<bool> modalChanged,
        Func<DeploymentBehavior,Action<GameState>,bool>? saveDefaults=null,IEnumerable<int>? editedPolicyOfficers=null,
        Func<int>? scrollSpeed=null,bool? tutorialRestrictsFacilities=null,
        Action<DeploymentAction,int,Action<DeploymentAction>>? chooseFalseReportDestination=null,
        Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged=null)
    {
        this.view=view;this.repository=repository;this.text=text;this.world=world;this.rules=rules;
        this.source=source;this.force=force;this.project=project;this.accepted=accepted;this.back=back;
        this.unavailable=unavailable;this.modalChanged=modalChanged;
        this.saveDefaults=saveDefaults;
        this.chooseFalseReportDestination=chooseFalseReportDestination;
        this.routePreviewChanged=routePreviewChanged;
        this.tutorialRestrictsFacilities=tutorialRestrictsFacilities;
        var cards=rules.QueryDeploymentTargetCards(world,rows);
        Draft=new(world,rules,rows,cards,editedPolicyOfficers);
        targetMenuQuery=rules.CreateDeploymentTargetMenuQuery(world,force);
        var portraits=rules.QueryOfficerPortraitRoutes(world,rows.Select(row=>row.Request.OfficerId).ToArray());
        var contents=rows.Select((row,index)=>DeploymentTargetContent.Create(repository,text,world,row,cards[index],
            portraits[row.Request.OfficerId],0,false)).ToArray();
        var city=world.Cities[source];
        var resources=rules.QueryDeploymentDraftResources(world,source,rows);
        view.Configure(repository,text,city,resources.Troops?.CurrentTroops
            ??throw new InvalidDataException("Deployment troop availability is unknown."),
            resources.DecisionCommandBooks,[new(source,city.Name,contents)],Draft.SelectedOfficers,scrollSpeed?.Invoke()??0,
            officers=>{if(disposed)return;CloseMenu();previewHex=null;Draft.SelectOfficers(officers);Refresh();},
            OpenFacilities,RequestBack,currentScrollSpeed:scrollSpeed);
        view.ConfigureSourceRoute(repository,projectMarker);
        view.ApplyDraftResources(resources);Refresh();
    }

    internal bool AllowPointer(int hex)
    {
        if(disposed||confirmation is not null||facilityPage is not null)return false;
        if(facilityQuery is not null)return facilityDefinition is not null;
        var permission=targetMenuQuery.QueryPointer(Draft.SelectedRows,hex);
        if(permission.Allowed is null)
            unavailable(permission.UnresolvedInput??"deployment.target.pointer");
        return permission.Allowed==true;
    }

    private DeploymentTargetMenu QueryTargetMenu(int hex)
    {
        var selected=Draft.SelectedRows;
        return targetMenuQuery.QueryMenu(selected,hex,new(Draft.HasActiveRouteGroup,
            Draft.HasActiveRouteGroup?Draft.ActiveWaypointIndex:null));
    }

    internal void PickTarget(int hex,bool waypointShortcut=false)
    {
        if(disposed||confirmation is not null)return;
        if(facilityQuery is not null){PickFacilityTarget(hex);return;}
        CloseMenu();var permission=QueryTargetMenu(hex);
        if(permission.CanOpen is null)
        {unavailable(permission.UnresolvedInput??"deployment.target.menu");return;}
        if(permission.CanOpen!=true||permission.TargetHexId is not int target)return;
        previewHex=target;
        if(Draft.PreviewTarget(target))view.ApplyProgress(Draft.Progress);
        RefreshPoints();
        if(waypointShortcut)
        {
            if(permission.Options.FirstOrDefault(option=>option.ActionKey=="waypoint") is
                {Included:true,Enabled:true,PresentationUnresolvedInput:null})AppendWaypoint(target);
            return;
        }
        ArmyCommandMenu? popup=null;
        var captions=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        var entries=permission.Options.Where(option=>option.Included==true).Select(option=>
            new ArmyCommandMenuEntry(option.ActionKey,(option.ActionKey=="capture"
                ?repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[0]:captions).Records[option.CaptionDataIndex].Text
                ??throw new InvalidDataException("Missing deployment command caption."),Icon(option.ActionKey),Hint(option),
                option.Enabled==true&&UnitTargetMenuMessages.AllowsPresentation(option)&&
                    (option.ActionKey=="waypoint"||option.Action is not null&&option.ActionKey is "move" or "enter" or "attack" or "repair" or "demolish" or "fire" or "occupy"||
                        chooseFalseReportDestination is not null&&option.Action is {ActionKey:"capture",Target.Kind:DeploymentTargetKind.Unit}),()=>
        {
            if(disposed||popup is null||menu!=popup)return;
            CloseMenu();var current=QueryTargetMenu(hex);
            var accepted=current.Options.FirstOrDefault(value=>value.ActionKey==option.ActionKey);
            if(current.CanOpen!=true||current.TargetHexId!=target||
                accepted is not {Included:true,Enabled:true}||!UnitTargetMenuMessages.AllowsPresentation(accepted)||
                accepted.Action!=option.Action||accepted.GoalHexId!=option.GoalHexId)
            {if(current.CanOpen is null)unavailable(current.UnresolvedInput??"deployment.target.menu");return;}
            if(accepted.ActionKey=="waypoint"){AppendWaypoint(target);return;}
            if(accepted.Action is null)return;
            if(accepted.Action is {ActionKey:"capture",Target.Kind:DeploymentTargetKind.Unit} action)
            {
                var ownerWorld=world;
                chooseFalseReportDestination?.Invoke(action,target,chosen=>
                {
                    if(disposed||!ReferenceEquals(world,ownerWorld)||chosen.FalseReportDestinationHexId is not int destination||
                        chosen!=(action with{FalseReportDestinationHexId=destination}))return;
                    Draft.AssignAction(chosen,target);view.ApplySelection(Draft.SelectedOfficers);Refresh();
                });
                return;
            }
            Draft.AssignAction(accepted.Action,accepted.GoalHexId??target);view.ApplySelection(Draft.SelectedOfficers);Refresh();
        },option.WaypointOrdinal)).ToArray();
        if(entries.Length==0)return;
        popup=new ArmyCommandMenu();
        menu=popup;view.AddMapMenu(popup);
        popup.Configure(repository,text,entries,value=>{if(!disposed&&menu==popup)view.ApplyHint(value);});
        popup.Name="DeploymentTargetMenu";
        var panel=new DeploymentPolicyView();policies=panel;view.AddPolicyPanel(panel);
        panel.Configure(repository,text,(setting,first)=>
        {if(!disposed&&policies==panel&&menu==popup)ChangePolicy(setting,first);},()=>
        {if(!disposed&&policies==panel&&menu==popup)SavePolicyDefaults();});
        RefreshPolicies();
        popup.TrackAnchor(()=>project(target),position=>
        {
            if(disposed||policies!=panel||menu!=popup)return;
            panel.Visible=position.HasValue;
            if(position is Vector2 point)panel.Position=point;
        },view.MenuFooterTop);
    }

    private string? Hint(UnitTargetMenuOption option)
    {
        if(!UnitTargetMenuMessages.AllowsPresentation(option))return null;
        int message=option.TooltipMessageId;
        if(message==0x56C1&&option.ActionKey=="capture")
            return (sourceMessages??=new(repository,text.SourceLocale)).FalseReportHint(option.SuccessRate,text.Get("detail_unknown"));
        if(message==0x56A9)
            return option.SuccessRate is int rate
                ?(sourceMessages??=new(repository,text.SourceLocale)).FireHint(rate):null;
        var record=repository.LoadMessages(text.SourceLocale,MessageKind.Game)
            .Messages.Sections[message/10000].Records[message%10000];
        return record.Literals.Count==1?record.Literals[0].Text:null;
    }
    private static string? Icon(string key)=>key switch
    {
        "move"=>"000_cmn_icon_bt_kidou",
        "enter"=>"000_cmn_icon_bt_nyujo",
        "attack"=>"000_cmn_icon_bt_battle",
        "repair" or "demolish"=>"000_cmn_icon_bt_build",
        "fire"=>"000_cmn_icon_bt_kakei",
        "occupy"=>"000_cmn_icon_bt_senryo",
        _=>null
    };

    private void AppendWaypoint(int hex)
    {
        if(!Draft.AppendWaypoint(hex))return;
        previewHex=hex;Refresh();
    }

    internal void HoverTarget(int? hex)
    {
        if(disposed||facilityQuery is not null||confirmation is not null||menu is not null||!Draft.HasActiveRouteGroup||
            hex is not int target||previewHex==target)return;
        if(targetMenuQuery.QueryPointer(Draft.SelectedRows,target).Allowed!=true)return;
        previewHex=target;
        if(Draft.PreviewTarget(target))view.ApplyProgress(Draft.Progress);
        RefreshPoints();
    }

    private void RefreshPoints()
    {
        view.ApplySourceWaypoints(Draft.Waypoints,Draft.HasActiveRouteGroup?previewHex:null);
        routePreviewChanged?.Invoke(Draft.RoutePreviewPaths);
    }

    private void Refresh()
    {
        view.ApplyProgress(Draft.Progress);
        view.ApplyDecision(Draft.CanDecide,RequestDecision);
        view.ApplyDraftResources(rules.QueryDeploymentDraftResources(world,source,Draft.Rows));
        RefreshPolicies();RefreshPoints();
    }

    private DeploymentPolicyOptions ReadPolicyOptions()
    {
        var selected=Draft.SelectedOfficers;
        if(policyOptions is null||!ReferenceEquals(policyWorld,world)||!policySelection.SequenceEqual(selected))
        {
            policyOptions=rules.QueryDeploymentPolicyOptions(world,selected);
            policyWorld=world;policySelection=selected.ToArray();
        }
        return policyOptions;
    }

    private DeploymentPolicyPair[] PolicyPairs(DeploymentPolicyOptions options)
    {
        var ids=Draft.SelectedOfficers.ToHashSet();
        var values=Draft.Rows.Where(row=>ids.Contains(row.Request.OfficerId)).Select(row=>row.Request.Behavior).ToArray();
        bool? Uniform(Func<DeploymentBehavior,bool> read)
        {
            if(values.Length==0||values.Any(value=>value is null||!Enum.IsDefined(value.AfterAction)))return null;
            bool first=read(values[0]!);return values.All(value=>read(value!)==first)?first:null;
        }
        return [new(Uniform(value=>value.AttackNearbyEnemies),options.AttackNearbyEnemiesAllowed,options.IgnoreNearbyEnemiesAllowed),
            new(Uniform(value=>value.AllowRetreat),options.RetreatAllowed,options.RetreatForbiddenAllowed),
            new(Uniform(value=>value.PursueRetreatingEnemies),options.PursuitAllowed,options.PursuitForbiddenAllowed),
            new(Uniform(value=>value.AfterAction==DeploymentAfterAction.Retreat),options.RetreatAfterActionAllowed,options.StayAfterActionAllowed)];
    }

    private DeploymentBehavior? SelectedPolicies(IReadOnlyList<DeploymentPolicyPair> pairs)=>
        pairs.All(pair=>pair.FirstSelected.HasValue)
            ?new(pairs[0].FirstSelected!.Value,pairs[1].FirstSelected!.Value,pairs[2].FirstSelected!.Value,
                pairs[3].FirstSelected==true?DeploymentAfterAction.Retreat:DeploymentAfterAction.Stay):null;

    private void RefreshPolicies()
    {
        if(policies is null)return;
        var options=ReadPolicyOptions();var pairs=PolicyPairs(options);
        var desired=SelectedPolicies(pairs);
        bool knownRows=Draft.Rows.Where(row=>Draft.SelectedOfficers.Contains(row.Request.OfficerId))
            .All(row=>row.Request.Behavior is { } value&&Enum.IsDefined(value.AfterAction));
        policies.Apply(options.HasSelection&&knownRows,pairs,options.Available&&saveDefaults is not null&&
            desired is not null&&world.DefaultDeploymentBehavior is { } prior&&desired!=prior);
        if(!options.Available)unavailable(options.UnresolvedInput!);
    }

    private void ChangePolicy(DeploymentPolicySetting setting,bool first)
    {
        var options=ReadPolicyOptions();var pairs=PolicyPairs(options);
        if(!options.Available||!options.HasSelection||(first?pairs[(int)setting].FirstAllowed:pairs[(int)setting].SecondAllowed)!=true)return;
        Draft.ChangePolicy(setting,first);RefreshPolicies();
    }

    private void SavePolicyDefaults()
    {
        if(saveDefaults is null)return;
        var options=ReadPolicyOptions();var desired=SelectedPolicies(PolicyPairs(options));
        if(!options.Available||!options.HasSelection||desired is null||world.DefaultDeploymentBehavior is not { } prior||desired==prior)return;
        var reset=rules.QueryDeploymentDefaultBehaviors(world,Draft.UneditedPolicyOfficers,desired);
        if(!reset.Available){unavailable(reset.UnresolvedInput!);return;}
        saveDefaults(desired,replacement=>
        {
            Draft.AcceptPreferenceWorld(world,replacement,reset.Behaviors!);
            world=replacement;
            CloseMenu();targetMenuQuery=rules.CreateDeploymentTargetMenuQuery(replacement,force);
        });
        RefreshPolicies();
    }

    private void RequestDecision()
    {
        if(disposed||facilityQuery is not null||!Draft.CanDecide||confirmation is not null)return;
        var result=rules.QueryDeploymentDraftConfirmation(world,Draft.Rows);
        if(result.Kind==DeploymentDraftConfirmationKind.Unknown)
        {unavailable(result.UnresolvedInput??"deployment.confirmation.unavailable");return;}
        Confirm(result.Kind==DeploymentDraftConfirmationKind.LowMoraleWarning?
            DeploymentTargetConfirmationKind.LowMorale:DeploymentTargetConfirmationKind.Orders,
            ()=>accepted(Draft.Rows));
    }

    private void RequestBack()
    {
        if(disposed||confirmation is not null)return;
        if(facilityQuery is not null){CancelFacilities();return;}
        if(Draft.Rows.Any(row=>row.Request.Actions.Count>0))Confirm(DeploymentTargetConfirmationKind.Discard,back);
        else back();
    }

    private void Confirm(DeploymentTargetConfirmationKind kind,Action yes)
    {
        CloseMenu();var dialog=new DeploymentTargetConfirmation();confirmation=dialog;view.AddChild(dialog);
        modalChanged(true);
        dialog.Configure(repository,text,kind,value=>
        {
            if(disposed||confirmation!=dialog)return;
            confirmation=null;dialog.Hide();dialog.QueueFree();modalChanged(false);
            if(value)yes();
        });
    }

    internal bool TryCancelOverlay()
    {
        if(disposed)return false;
        if(confirmation is { } dialog){dialog.RequestCancel();return true;}
        if(facilityQuery is not null){CancelFacilities();return true;}
        if(menu is not null){CloseMenu();RefreshPoints();return true;}
        if(!Draft.UndoWaypoint())return false;
        Refresh();return true;
    }
    private void CloseMenu()
    {
        var old=menu;var panel=policies;menu=null;policies=null;
        if(old is not null){old.Abandon();old.QueueFree();}
        if(panel is not null){panel.Abandon();panel.QueueFree();}
        view.ApplyHint(null);
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;CloseMenu();CloseFacilities();
        routePreviewChanged?.Invoke(Array.Empty<MapRoutePreviewPath>());
        if(confirmation is { } dialog){confirmation=null;dialog.Abandon();dialog.QueueFree();modalChanged(false);}
    }
}
