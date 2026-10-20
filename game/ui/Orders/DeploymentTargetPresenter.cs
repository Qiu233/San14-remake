using Godot;
using San14.Core;
using San14.Data;

/// <summary>Owns the unsubmitted ordinary target interaction. It queries current
/// permissions, routes and confirmation rules; only its caller may submit.</summary>
internal sealed class DeploymentTargetPresenter : IDisposable
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
    private DeploymentMoveMenu? menu;
    private DeploymentPolicyView? policies;
    private readonly Func<DeploymentBehavior,Action<GameState>,bool>? saveDefaults;
    private DeploymentPolicyOptions? policyOptions;
    private GameState? policyWorld;
    private IReadOnlyList<int> policySelection=[];
    private DeploymentTargetConfirmation? confirmation;
    private bool disposed;
    internal DeploymentTargetDraft Draft {get;}

    internal DeploymentTargetPresenter(DeploymentTargetView view,GameResourceRepository repository,PreviewText text,
        GameState world,ArmyCampaignRuleAdapter rules,int source,int force,IReadOnlyList<DeploymentDraftRow> rows,
        Func<int,Vector2?> project,Action<IReadOnlyList<DeploymentDraftRow>> accepted,Action back,
        Action<string> unavailable,Action<bool> modalChanged,
        Func<DeploymentBehavior,Action<GameState>,bool>? saveDefaults=null,IEnumerable<int>? editedPolicyOfficers=null)
    {
        this.view=view;this.repository=repository;this.text=text;this.world=world;this.rules=rules;
        this.source=source;this.force=force;this.project=project;this.accepted=accepted;this.back=back;
        this.unavailable=unavailable;this.modalChanged=modalChanged;
        this.saveDefaults=saveDefaults;
        var cards=rules.QueryDeploymentTargetCards(world,rows);
        Draft=new(world,rules,rows,cards,editedPolicyOfficers);
        var portraits=rules.QueryOfficerPortraitRoutes(world,rows.Select(row=>row.Request.OfficerId).ToArray());
        var contents=rows.Select((row,index)=>DeploymentTargetContent.Create(repository,text,world,row,cards[index],
            portraits[row.Request.OfficerId],0,false)).ToArray();
        var city=world.Cities[source];
        var resources=rules.QueryDeploymentDraftResources(world,source,rows);
        view.Configure(repository,text,city,resources.Troops?.CurrentTroops
            ??throw new InvalidDataException("Deployment troop availability is unknown."),
            resources.DecisionCommandBooks,[new(source,city.Name,contents)],Draft.SelectedOfficers,0,
            officers=>{if(disposed)return;CloseMenu();Draft.SelectOfficers(officers);Refresh();},
            ()=>{if(!disposed)unavailable("deployment.target.facility_selection");},RequestBack);
        view.ApplyDraftResources(resources);Refresh();
    }

    internal bool AllowPointer(int hex)
    {
        if(disposed||confirmation is not null)return false;
        var permission=Draft.QueryEmptyHexMove(hex,force);
        if(permission.IsEmpty==false)return false;
        if(permission.PointerAllowed is null)
            unavailable(permission.PointerUnresolvedInput??permission.ClassificationUnresolvedInput??"deployment.target.pointer");
        return permission.PointerAllowed==true;
    }

    internal void PickTarget(int hex)
    {
        if(disposed||confirmation is not null)return;
        CloseMenu();var permission=Draft.QueryEmptyHexMove(hex,force);
        if(permission.IsEmpty==false)return;
        if(permission.ShowMove is null)
        {unavailable(permission.MenuUnresolvedInput??permission.ClassificationUnresolvedInput??"deployment.target.move");return;}
        if(permission.ShowMove!=true)return;
        var popup=new DeploymentMoveMenu();menu=popup;view.AddMapMenu(popup);
        popup.Configure(repository,text,()=>
        {
            if(disposed||menu!=popup)return;
            CloseMenu();var current=Draft.QueryEmptyHexMove(hex,force);
            if(current.ShowMove!=true)
            {if(current.ShowMove is null)unavailable(current.MenuUnresolvedInput??"deployment.target.move");return;}
            Draft.AssignMove(hex);view.ApplySelection(Draft.SelectedOfficers);Refresh();
        },value=>{if(!disposed&&menu==popup)view.ApplyHint(value);});
        var panel=new DeploymentPolicyView();policies=panel;view.AddPolicyPanel(panel);
        panel.Configure(repository,text,(setting,first)=>
        {if(!disposed&&policies==panel&&menu==popup)ChangePolicy(setting,first);},()=>
        {if(!disposed&&policies==panel&&menu==popup)SavePolicyDefaults();});
        RefreshPolicies();
        popup.TrackAnchor(()=>project(hex),position=>
        {
            if(disposed||policies!=panel||menu!=popup)return;
            panel.Visible=position.HasValue;
            if(position is Vector2 point)panel.Position=point;
        },view.MenuFooterTop);
    }

    private void Refresh()
    {
        view.ApplyProgress(Draft.Progress);
        view.ApplyDecision(Draft.CanDecide,RequestDecision);
        view.ApplyDraftResources(rules.QueryDeploymentDraftResources(world,source,Draft.Rows));
        RefreshPolicies();
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
        });
        RefreshPolicies();
    }

    private void RequestDecision()
    {
        if(disposed||!Draft.CanDecide||confirmation is not null)return;
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
        if(menu is null)return false;CloseMenu();return true;
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
        if(disposed)return;disposed=true;CloseMenu();
        if(confirmation is { } dialog){confirmation=null;dialog.Abandon();dialog.QueueFree();modalChanged(false);}
    }
}
