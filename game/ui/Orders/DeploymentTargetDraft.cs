using San14.Core;
using San14.Data;

internal sealed record DeploymentTargetRouteCache(DeploymentDraftRouteStatus Status,IReadOnlyList<int>? Path,
    IReadOnlyList<int>? EtaPath,string? UnresolvedInput);
internal sealed record DeploymentTargetRouteGroup(IReadOnlyList<int> OfficerIds,IReadOnlyList<int> Points,
    DeploymentTargetRouteCache? Route);

/// <summary>Unsubmitted ordinary targets with no explicit waypoints. Route
/// groups are stably ordered by points and only adjacent compatible groups
/// merge. The saved world and its live Army registry remain untouched.</summary>
internal sealed class DeploymentTargetDraft
{
    private GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly Dictionary<int,DeploymentDraftRow> rows;
    private readonly Dictionary<int,int> starts;
    private readonly Dictionary<int,DeploymentTargetProgress> progress=[];
    private readonly List<int> order;
    private readonly HashSet<int> selected=[];
    private readonly HashSet<int> editedPolicies=[];
    private IReadOnlyList<DeploymentTargetRouteGroup> groups;

    internal IReadOnlyList<DeploymentDraftRow> Rows=>order.Select(officer=>rows[officer]).ToArray();
    internal IReadOnlyList<int> SelectedOfficers=>order.Where(selected.Contains).ToArray();
    internal IReadOnlyList<int> EditedPolicyOfficers=>order.Where(editedPolicies.Contains).ToArray();
    internal IReadOnlyList<int> UneditedPolicyOfficers=>order.Where(officer=>!editedPolicies.Contains(officer)).ToArray();
    internal IReadOnlyList<DeploymentTargetRouteGroup> Groups=>groups;
    // A selected row previews the active singleton, whose ETA path has fewer
    // than two points. Deselecting integrates the current committed group's
    // cache; merging may have replaced the path used for its previous ETA.
    internal IReadOnlyList<DeploymentTargetProgress> Progress=>order.Select(officer=>selected.Contains(officer)
        ?progress[officer] with{EstimatedDays=0}:progress[officer]).ToArray();
    // This finite owner creates only Hex targets. Native target resolution for
    // other types belongs to their future query, not to path reachability.
    internal bool CanDecide=>order.Count>0&&order.All(officer=>progress[officer].HasResolvedTarget);

    internal DeploymentEmptyHexMove QueryEmptyHexMove(int hex,int targetForceId)=>
        rules.QueryDeploymentEmptyHexMove(world,hex,targetForceId,new(SelectedOfficers,selected.Count>0,selected.Count>0?0:null));

    internal DeploymentTargetDraft(GameState world,ArmyCampaignRuleAdapter rules,
        IReadOnlyList<DeploymentDraftRow> initial,IReadOnlyList<DeploymentTargetCard> cards,
        IEnumerable<int>? editedPolicyOfficers=null)
    {
        this.world=world;this.rules=rules;
        rows=initial.ToDictionary(row=>row.Request.OfficerId);order=initial.Select(row=>row.Request.OfficerId).ToList();
        editedPolicies.UnionWith(editedPolicyOfficers??[]);
        if(editedPolicies.Any(officer=>!rows.ContainsKey(officer)))
            throw new ArgumentException("Edited policies must belong to the target packets.",nameof(editedPolicyOfficers));
        var positions=cards.ToDictionary(card=>card.OfficerId);
        if(positions.Count!=rows.Count||rows.Keys.Any(officer=>!positions.ContainsKey(officer)))
            throw new ArgumentException("Target positions must belong to the complete draft.",nameof(cards));
        if(initial.Any(row=>row.Request.Actions.Count!=0||row.Request.WaypointHexIds.Count!=0))
            throw new ArgumentException("This target owner accepts new single-segment rows only.",nameof(initial));
        starts=[];
        foreach(int officer in order)
        {
            var card=positions[officer];
            if(card.HasCurrentHex!=true||card.CurrentHex is not int hex||hex<0||hex>=checked(world.MapWidth*world.MapHeight))
                throw new InvalidDataException(card.PositionUnresolvedInput??"deployment.target.current_hex");
            starts.Add(officer,hex);selected.Add(officer);progress.Add(officer,new(officer,0,false));
        }
        groups=Normalize(order.Select(officer=>new DeploymentTargetRouteGroup([officer],[starts[officer]],null)));
    }

    internal void SelectOfficers(IEnumerable<int> officers)
    {
        var next=officers.ToHashSet();
        if(next.Any(officer=>!rows.ContainsKey(officer)))throw new ArgumentException("Selection must belong to this draft.",nameof(officers));
        var deselected=selected.Except(next).ToHashSet();
        foreach(var group in groups)
        {
            var changed=group.OfficerIds.Where(deselected.Contains).ToArray();
            if(changed.Length==0||group.Route is null)continue;
            if(group.Route.EtaPath is { } path)
            {
                foreach(var value in rules.QueryDeploymentDraftEta(world,changed.Select(officer=>rows[officer]).ToArray(),path))
                    progress[value.OfficerId]=new(value.OfficerId,value.Days,progress[value.OfficerId].HasResolvedTarget);
            }
            else foreach(int officer in changed)progress[officer]=progress[officer] with{EstimatedDays=null};
        }
        selected.Clear();selected.UnionWith(next);
    }

    internal bool ChangePolicy(DeploymentPolicySetting setting,bool first)
    {
        if(!Enum.IsDefined(setting))throw new ArgumentOutOfRangeException(nameof(setting));
        var updates=selected.Select(officer=>
        {
            var row=rows[officer];var behavior=row.Request.Behavior
                ??throw new InvalidDataException("The current deployment policy is unknown.");
            var replacement=setting switch
            {
                DeploymentPolicySetting.EnemyApproach=>behavior with{AttackNearbyEnemies=first},
                DeploymentPolicySetting.Retreat=>behavior with{AllowRetreat=first},
                DeploymentPolicySetting.Pursuit=>behavior with{PursueRetreatingEnemies=first},
                DeploymentPolicySetting.AfterAction=>behavior with{AfterAction=first?DeploymentAfterAction.Retreat:DeploymentAfterAction.Stay},
                _=>throw new ArgumentOutOfRangeException(nameof(setting))
            };
            return (Officer:officer,Row:row,Behavior:replacement);
        }).ToArray();
        bool changed=false;
        foreach(var update in updates)
        {
            if(update.Row.Request.Behavior==update.Behavior)continue;
            rows[update.Officer]=update.Row with{Request=update.Row.Request with{Behavior=update.Behavior}};
            editedPolicies.Add(update.Officer);changed=true;
        }
        return changed;
    }

    /// <summary>Only the accepted save-defaults command can replace this read
    /// context. It cannot alter troops, targets, paths or the edited-row set.</summary>
    internal void AcceptPreferenceWorld(GameState expected,GameState replacement,
        IReadOnlyDictionary<int,DeploymentBehavior> resetBehaviors)
    {
        if(!ReferenceEquals(world,expected))throw new InvalidOperationException("The target draft belongs to another world.");
        var reset=UneditedPolicyOfficers;
        if(resetBehaviors.Count!=reset.Count||reset.Any(officer=>!resetBehaviors.ContainsKey(officer)))
            throw new ArgumentException("Default policies must cover every unedited target packet.",nameof(resetBehaviors));
        world=replacement;
        foreach(int officer in reset)
        {
            var row=rows[officer];
            rows[officer]=row with{Request=row.Request with{Behavior=resetBehaviors[officer]}};
        }
    }

    /// <summary>The caller must first obtain the actual Move menu permission.
    /// Target assignment survives NoPath or unknown route inputs. This method
    /// does not infer menu eligibility from a successful search.</summary>
    internal void AssignMove(int goalHex)
    {
        if(goalHex<0||goalHex>=checked(world.MapWidth*world.MapHeight))throw new ArgumentOutOfRangeException(nameof(goalHex));
        if(selected.Count==0)throw new InvalidOperationException("Move needs selected draft rows.");
        // Selection forms an independent active set. Splitting a committed
        // group never searches or invalidates its unselected rows' old cache.
        // Rebuild selected singletons in original row order using only their
        // current positions; old committed destinations do not sort this set.
        var active=Normalize(order.Where(selected.Contains).Select(officer=>
            new DeploymentTargetRouteGroup([officer],[starts[officer]],null)));
        var remaining=groups.Select(group=>group with
            {OfficerIds=group.OfficerIds.Where(officer=>!selected.Contains(officer)).ToArray()})
            .Where(group=>group.OfficerIds.Count>0).ToList();
        // Selection already formed active groups using the old actions.
        // Changing the action does not regroup them before their searches.
        foreach(int officer in selected)
            rows[officer]=rows[officer] with{Request=rows[officer].Request with
                {Actions=Array.AsReadOnly(new[]{new DeploymentAction("move",new(DeploymentTargetKind.Hex,goalHex))})}};
        foreach(var group in active)
        {
            int start=group.Points[0];
            var march=rules.QueryDeploymentDraftMarch(world,group.OfficerIds.Select(officer=>rows[officer]).ToArray(),start,goalHex);
            foreach(var value in march.Rows)progress[value.OfficerId]=new(value.OfficerId,value.Days,true);
            remaining.Add(group with{Points=Array.AsReadOnly(new[]{start,goalHex}),
                Route=new(march.Status,march.Path,march.EtaPath,march.UnresolvedInput)});
        }
        groups=Normalize(remaining);selected.Clear();
    }

    private IReadOnlyList<DeploymentTargetRouteGroup> Normalize(IEnumerable<DeploymentTargetRouteGroup> input)
    {
        // Enumerable.OrderBy is stable. Original ordinary UI uses stable
        // insertion sorting for at most nine rows, with the same comparator.
        var sorted=input.OrderBy(group=>group,Comparer<DeploymentTargetRouteGroup>.Create(ComparePoints));
        var result=new List<DeploymentTargetRouteGroup>();
        foreach(var group in sorted)
        {
            if(result.Count>0&&Compatible(result[^1],group))
                result[^1]=result[^1] with{OfficerIds=Array.AsReadOnly(result[^1].OfficerIds.Concat(group.OfficerIds).ToArray())};
            else result.Add(group);
        }
        return result.AsReadOnly();
    }

    private bool Compatible(DeploymentTargetRouteGroup left,DeploymentTargetRouteGroup right)
    {
        if(ComparePoints(left,right)!=0)return false;
        var first=rows[left.OfficerIds[0]];var second=rows[right.OfficerIds[0]];
        return first.Request.FormationId==second.Request.FormationId&&first.NavalFormationId==second.NavalFormationId&&
            first.Request.Actions.FirstOrDefault()?.ActionKey==second.Request.Actions.FirstOrDefault()?.ActionKey;
    }

    private static int ComparePoints(DeploymentTargetRouteGroup left,DeploymentTargetRouteGroup right)
    {
        int count=left.Points.Count.CompareTo(right.Points.Count);if(count!=0)return count;
        for(int index=0;index<left.Points.Count;index++)
        {int value=left.Points[index].CompareTo(right.Points[index]);if(value!=0)return value;}
        return 0;
    }
}
