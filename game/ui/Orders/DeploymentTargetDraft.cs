using San14.Core;
using San14.Data;

internal sealed record DeploymentTargetRouteCache(DeploymentDraftRouteStatus Status,IReadOnlyList<int>? Path,
    IReadOnlyList<int>? EtaPath,string? UnresolvedInput)
{
    internal int? StartSurfaceCode {get;init;}
    internal int? EndSurfaceCode {get;init;}
    internal string? PresentationUnresolvedInput {get;init;}
    internal int? ControlStartHex {get;init;}
    internal int? ControlGoalHex {get;init;}
    internal int? ControlStartSurfaceCode {get;init;}
    internal int? ControlGoalSurfaceCode {get;init;}
}
internal sealed record DeploymentTargetRouteGroup(IReadOnlyList<int> OfficerIds,IReadOnlyList<int> Points,
    DeploymentTargetRouteCache? Route)
{
    internal int ActiveWaypointIndex {get;init;}
    internal IReadOnlyList<DeploymentTargetRouteCache?> Segments {get;init;}=[];
}

/// <summary>Unsubmitted ordinary targets and their fresh active routes. Route
/// groups are stably ordered by points and only adjacent compatible groups
/// merge. The saved world and its live Army registry remain untouched.</summary>
internal sealed partial class DeploymentTargetDraft
{
    private GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private DeploymentDraftRouteQuery routeQuery;
    private readonly Dictionary<int,DeploymentDraftRow> rows;
    private readonly Dictionary<int,int> starts;
    private readonly Dictionary<int,DeploymentTargetProgress> progress=[];
    private readonly List<int> order;
    private readonly HashSet<int> selected=[];
    private readonly HashSet<int> editedPolicies=[];
    private IReadOnlyList<DeploymentTargetRouteGroup> groups;
    private IReadOnlyList<DeploymentTargetRouteGroup> activeGroups=[];

    internal IReadOnlyList<DeploymentDraftRow> Rows=>order.Select(officer=>rows[officer]).ToArray();
    internal IReadOnlyList<int> SelectedOfficers=>order.Where(selected.Contains).ToArray();
    internal IReadOnlyList<DeploymentDraftRow> SelectedRows=>order.Where(selected.Contains).Select(officer=>rows[officer]).ToArray();
    internal IReadOnlyList<DeploymentFacilityDraftRow> FacilityRows=>order.Select(officer=>
        new DeploymentFacilityDraftRow(rows[officer],selected.Contains(officer),RetainedDefinition(rows[officer]))).ToArray();
    internal IReadOnlyList<int> EditedPolicyOfficers=>order.Where(editedPolicies.Contains).ToArray();
    internal IReadOnlyList<int> UneditedPolicyOfficers=>order.Where(officer=>!editedPolicies.Contains(officer)).ToArray();
    internal IReadOnlyList<DeploymentTargetRouteGroup> Groups=>groups;
    internal IReadOnlyList<DeploymentTargetRouteGroup> ActiveGroups=>activeGroups;
    internal bool HasActiveRouteGroup=>activeGroups.Count>0;
    internal int ActiveWaypointIndex=>activeGroups.Count==0?0:activeGroups[0].ActiveWaypointIndex;
    internal IReadOnlyList<int> Waypoints=>activeGroups.Count==0?[]:
        activeGroups[0].Points.Skip(1).Take(ActiveWaypointIndex).ToArray();
    internal IReadOnlyList<DeploymentTargetProgress> Progress=>order.Select(officer=>progress[officer]).ToArray();
    internal bool CanDecide=>order.Count>0&&order.All(officer=>progress[officer].HasResolvedTarget);

    internal DeploymentEmptyHexMove QueryEmptyHexMove(int hex,int targetForceId)=>
        rules.QueryDeploymentEmptyHexMove(world,hex,targetForceId,new(SelectedOfficers,HasActiveRouteGroup,
            HasActiveRouteGroup?ActiveWaypointIndex:null));

    internal DeploymentTargetDraft(GameState world,ArmyCampaignRuleAdapter rules,
        IReadOnlyList<DeploymentDraftRow> initial,IReadOnlyList<DeploymentTargetCard> cards,
        IEnumerable<int>? editedPolicyOfficers=null, IReadOnlyDictionary<int,int>? initialGoals=null)
    {
        this.world=world;this.rules=rules;routeQuery=rules.CreateDeploymentDraftRouteQuery(world);
        rows=initial.ToDictionary(row=>row.Request.OfficerId);order=initial.Select(row=>row.Request.OfficerId).ToList();
        editedPolicies.UnionWith(editedPolicyOfficers??[]);
        if(editedPolicies.Any(officer=>!rows.ContainsKey(officer)))
            throw new ArgumentException("Edited policies must belong to the target packets.",nameof(editedPolicyOfficers));
        var positions=cards.ToDictionary(card=>card.OfficerId);
        if(positions.Count!=rows.Count||rows.Keys.Any(officer=>!positions.ContainsKey(officer)))
            throw new ArgumentException("Target positions must belong to the complete draft.",nameof(cards));
        if(initialGoals is null && initial.Any(row=>row.Request.Actions.Count!=0||row.Request.WaypointHexIds.Count!=0))
            throw new ArgumentException("This target owner accepts new Make rows only.",nameof(initial));
        starts=[];
        foreach(int officer in order)
        {
            var card=positions[officer];
            if(card.HasCurrentHex!=true||card.CurrentHex is not int hex||hex<0||hex>=checked(world.MapWidth*world.MapHeight))
                throw new InvalidDataException(card.PositionUnresolvedInput??"deployment.target.current_hex");
            starts.Add(officer,hex);selected.Add(officer);progress.Add(officer,new(officer,0,false));
        }
        groups=Normalize(order.Select(officer=>new DeploymentTargetRouteGroup([officer],[starts[officer]],null)));
        RebuildActive();
        if (initialGoals is not null) InitializeGeneralTargets(initialGoals);
    }

    internal void SelectOfficers(IEnumerable<int> officers)
    {
        var next=officers.ToHashSet();
        if(next.Any(officer=>!rows.ContainsKey(officer)))throw new ArgumentException("Selection must belong to this draft.",nameof(officers));
        if(selected.SetEquals(next))return;
        var deselected=selected.Except(next).ToHashSet();
        selected.Clear();selected.UnionWith(next);RebuildActive();
        foreach(var group in groups)
        {
            var changed=group.OfficerIds.Where(deselected.Contains).ToArray();
            if(changed.Length==0||group.Route is null)continue;
            if(group.Route.EtaPath is { } path)
            {
                foreach(var value in routeQuery.QueryEta(changed.Select(officer=>rows[officer]).ToArray(),path))
                    progress[value.OfficerId]=new(value.OfficerId,value.Days,progress[value.OfficerId].HasResolvedTarget);
            }
            else foreach(int officer in changed)progress[officer]=progress[officer] with{EstimatedDays=null};
        }
    }

    private void RebuildActive()
    {
        // E4280 rebuilds from the ordered page rows and their real current
        // positions. Old committed targets and waypoints never seed this set.
        foreach(var group in activeGroups)
            foreach(int officer in group.OfficerIds)progress[officer]=progress[officer] with{EstimatedDays=0};
        activeGroups=Normalize(order.Where(selected.Contains).Select(officer=>
            new DeploymentTargetRouteGroup([officer],[starts[officer]],null)));
        foreach(int officer in selected)progress[officer]=progress[officer] with{EstimatedDays=0};
    }

    internal bool PreviewTarget(int hex)
    {
        RequireHex(hex);
        //7E3400/7E7A00 query only a changed actual candidate. Retained prefix
        //segments survive; repeated input and camera frames never search.
        bool changed=false;
        var nextGroups=activeGroups.Select(group=>
        {
            var next=SetTail(group,hex);
            if(ReferenceEquals(next,group))return group;
            changed=true;
            next=EnsureSegments(next);Integrate(next);
            return next;
        }).ToArray();
        if(changed)activeGroups=nextGroups;
        return changed;
    }

    internal bool AppendWaypoint(int hex)
    {
        RequireHex(hex);
        if(activeGroups.Count==0||ActiveWaypointIndex>=5)return false;
        activeGroups=activeGroups.Select(group=>
        {
            var target=EnsureSegments(SetTail(group,hex));
            Integrate(target);
            int at=target.ActiveWaypointIndex+1;
            var points=target.Points.ToList();points.Insert(at,hex);
            var segments=target.Segments.ToList();segments.Insert(at,null);
            // C5990 retains the candidate after the newly confirmed point.
            // Its new tail is a real zero-length query, not a fabricated path.
            return EnsureSegments(WithRoute(target with{Points=points.ToArray(),Segments=segments.ToArray(),
                ActiveWaypointIndex=at}));
        }).ToArray();
        return true;
    }

    internal bool UndoWaypoint()
    {
        if(activeGroups.Count==0||ActiveWaypointIndex==0)return false;
        activeGroups=activeGroups.Select(group=>
        {
            int at=group.ActiveWaypointIndex;
            var points=group.Points.ToList();points.RemoveAt(at);
            var segments=group.Segments.ToList();segments.RemoveAt(at);segments[at-1]=null;
            var replacement=EnsureSegments(WithRoute(group with{Points=points.ToArray(),
                Segments=segments.ToArray(),ActiveWaypointIndex=at-1}));
            Integrate(replacement);return replacement;
        }).ToArray();
        return true;
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

    internal void AdoptSpeechWorld(GameState expected,GameState replacement)
    {
        if(!ReferenceEquals(world,expected))throw new InvalidOperationException("The target draft belongs to another world.");
        // Detached route/placement inputs and cached paths remain valid: speech
        // changes no gameplay inputs, rows, selection, policies or waypoints.
        world=replacement;
    }

    /// <summary>An accepted save-defaults command also resets the unedited
    /// policies. It cannot alter troops, targets, paths or the edited-row set.</summary>
    internal void AcceptPreferenceWorld(GameState expected,GameState replacement,
        IReadOnlyDictionary<int,DeploymentBehavior> resetBehaviors)
    {
        if(!ReferenceEquals(world,expected))throw new InvalidOperationException("The target draft belongs to another world.");
        var reset=UneditedPolicyOfficers;
        if(resetBehaviors.Count!=reset.Count||reset.Any(officer=>!resetBehaviors.ContainsKey(officer)))
            throw new ArgumentException("Default policies must cover every unedited target packet.",nameof(resetBehaviors));
        world=replacement;routeQuery=rules.CreateDeploymentDraftRouteQuery(replacement);
        foreach(int officer in reset)
        {
            var row=rows[officer];
            rows[officer]=row with{Request=row.Request with{Behavior=resetBehaviors[officer]}};
        }
    }

    /// <summary>The caller must first obtain the actual Move menu permission.
    /// Target assignment survives NoPath or unknown route inputs. This method
    /// does not infer menu eligibility from a successful search.</summary>
    internal void AssignMove(int goalHex)=>AssignAction(new("move",new(DeploymentTargetKind.Hex,goalHex)),goalHex);

    private static int? RetainedDefinition(DeploymentDraftRow row)
    {
        var key=row.Request.ConstructionSelection?.DefinitionKey;
        if(key is null)return 0; // A new Make packet has known no selection.
        return FacilityConstructionIntent.IsDefinitionKey(key)
            ?int.Parse(key.AsSpan("san14-object-definition:".Length)):null;
    }

    internal void AssignConstruction(DeploymentFacilityPlacement placement,int? finalHexId=null)
    {
        if(placement is not {OriginalAllowed:true,CommandUnresolvedInput:null,Action:{ActionKey:"build"} action})
            throw new ArgumentException("Construction needs an accepted placement.",nameof(placement));
        var intent=new FacilityConstructionIntent(action.StructureKindId,
            ArmyMarchDestination.AtHex(placement.ClickedHexId),finalHexId is int end
                ?ArmyMarchDestination.AtHex(end):ArmyMarchDestination.None);
        foreach(int officer in selected)
            rows[officer]=rows[officer] with{Request=rows[officer].Request with{ConstructionSelection=intent}};
        AssignAction(action,placement.ClickedHexId);
    }

    /// <summary>The mode0 menu supplies the typed command target separately
    /// from its normalized map position. Keep the complete Make configuration
    /// and policies while assigning only this selected set's initial action.</summary>
    internal void AssignAction(DeploymentAction action,int goalHex)
    {
        ArgumentNullException.ThrowIfNull(action);
        RequireHex(goalHex);
        if(selected.Count==0)throw new InvalidOperationException("An initial action needs selected draft rows.");
        var remaining=groups.Select(group=>group with
            {OfficerIds=group.OfficerIds.Where(officer=>!selected.Contains(officer)).ToArray()})
            .Where(group=>group.OfficerIds.Count>0).ToList();
        // Selection already formed active groups using the old actions.
        // Changing the action does not regroup them before their searches.
        foreach(int officer in selected)
            rows[officer]=rows[officer] with{Request=rows[officer].Request with
                {Actions=Array.AsReadOnly(new[]{action})}};
        foreach(var group in activeGroups)
        {
            var target=EnsureSegments(SetTail(group,goalHex));
            foreach(int officer in target.OfficerIds)
                rows[officer]=rows[officer] with{Request=rows[officer].Request with
                    {WaypointHexIds=Array.AsReadOnly(target.Points.Skip(1).Take(target.ActiveWaypointIndex).ToArray())}};
            Integrate(target);
            foreach(int officer in target.OfficerIds)progress[officer]=progress[officer] with{HasResolvedTarget=true};
            remaining.Add(target);
        }
        groups=Normalize(remaining);selected.Clear();activeGroups=[];
    }

    private IReadOnlyList<DeploymentTargetRouteGroup>? publishedGroups,publishedActiveGroups;
    private IReadOnlyList<MapRoutePreviewPath> publishedPaths=[];

    //The geometry consumer reads this stable publication, including from a
    //camera frame. Rebuild only after the actual page group writers replace it.
    internal IReadOnlyList<MapRoutePreviewPath> RoutePreviewPaths
    {
        get
        {
            if(ReferenceEquals(publishedGroups,groups)&&ReferenceEquals(publishedActiveGroups,activeGroups))return publishedPaths;
            var paths=new List<MapRoutePreviewPath>();
            Add(groups,1);Add(activeGroups,0);
            publishedGroups=groups;publishedActiveGroups=activeGroups;
            return publishedPaths=paths.AsReadOnly();

            void Add(IReadOnlyList<DeploymentTargetRouteGroup> source,int bank)
            {
                foreach(var group in source)
                    foreach(var segment in group.Segments)
                        if(segment is not null)
                            paths.Add(new(segment,segment.Status,segment.Path,segment.StartSurfaceCode,segment.EndSurfaceCode,
                                bank,0,segment.PresentationUnresolvedInput??segment.UnresolvedInput)
                            {ControlStartHex=segment.ControlStartHex,ControlGoalHex=segment.ControlGoalHex,
                                ControlStartSurfaceCode=segment.ControlStartSurfaceCode,ControlGoalSurfaceCode=segment.ControlGoalSurfaceCode});
            }
        }
    }

    private void RequireHex(int hex)
    {
        if(hex<0||hex>=checked(world.MapWidth*world.MapHeight))throw new ArgumentOutOfRangeException(nameof(hex));
    }

    private static DeploymentTargetRouteGroup SetTail(DeploymentTargetRouteGroup group,int hex)
    {
        int confirmed=group.ActiveWaypointIndex+1;
        if(group.Points.Count>confirmed&&group.Points[^1]==hex)return group;
        return WithRoute(group with{Points=group.Points.Take(confirmed).Append(hex).ToArray(),
            Segments=group.Segments.Take(confirmed-1).Append(null).ToArray()});
    }

    private DeploymentTargetRouteGroup EnsureSegments(DeploymentTargetRouteGroup group)
    {
        var segments=group.Segments.ToArray();
        for(int index=0;index<segments.Length;index++)
        {
            if(segments[index] is not null)continue;
            var result=routeQuery.QuerySegment(group.OfficerIds.Select(officer=>rows[officer]).ToArray(),
                group.Points[index],group.Points[index+1]);
            segments[index]=new(result.Status,result.Path,result.EtaPath,result.UnresolvedInput)
            {StartSurfaceCode=result.StartSurfaceCode,EndSurfaceCode=result.EndSurfaceCode,
                PresentationUnresolvedInput=result.PresentationUnresolvedInput,
                ControlStartHex=result.ControlStartHex,ControlGoalHex=result.ControlGoalHex,
                ControlStartSurfaceCode=result.ControlStartSurfaceCode,ControlGoalSurfaceCode=result.ControlGoalSurfaceCode};
        }
        return WithRoute(group with{Segments=segments});
    }

    private void Integrate(DeploymentTargetRouteGroup group)
    {
        if(group.Route?.EtaPath is not { } path)
        {
            foreach(int officer in group.OfficerIds)progress[officer]=progress[officer] with{EstimatedDays=null};
            return;
        }
        foreach(var row in routeQuery.QueryEta(group.OfficerIds.Select(officer=>rows[officer]).ToArray(),path))
            progress[row.OfficerId]=progress[row.OfficerId] with{EstimatedDays=row.Days};
    }

    private static DeploymentTargetRouteGroup WithRoute(DeploymentTargetRouteGroup group)
    {
        if(group.Segments.Count==0)return group with{Route=null};
        // A hover-invalidated tail must not copy every retained prefix path
        // before discovering that no complete ETA list exists yet.
        foreach(var segment in group.Segments)
            if(segment?.EtaPath is null)
                return group with{Route=new(DeploymentDraftRouteStatus.Unresolved,null,null,
                    segment?.UnresolvedInput??"deployment.target.pending_segment")};
        // Each actual segment already omitted its own last point. Preserve
        // joins, duplicates and float accumulation order; never add Days.
        var eta=new List<int>();bool noPath=false;
        foreach(var segment in group.Segments)
        {
            if(segment?.EtaPath is { } path)
            {eta.AddRange(path);noPath|=segment.Status==DeploymentDraftRouteStatus.NoPath;}
        }
        return group with{Route=new(noPath?DeploymentDraftRouteStatus.NoPath:DeploymentDraftRouteStatus.Found,
            group.Segments.Count==1?group.Segments[0]?.Path:null,eta.ToArray(),null)};
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
