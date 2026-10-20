using San14.Core;
using San14.Data;

/// <summary>The selected city's temporary caller state. Make packets and
/// returned Target packets have distinct lifetimes and never enter GameState.</summary>
internal sealed class DeploymentCityDraft(GameState world,int city)
{
    private IReadOnlyList<DeploymentDraftRow> targetRows=[];
    private readonly HashSet<int> editedPolicies=[];
    internal IReadOnlyList<DeploymentDraftRow> AcceptedRows {get;private set;}=[];
    internal IReadOnlyList<int> EditedPolicyOfficers=>targetRows.Select(row=>row.Request.OfficerId).Where(editedPolicies.Contains).ToArray();
    internal bool IsFor(GameState current,int source)=>ReferenceEquals(world,current)&&source==city;

    internal IReadOnlyList<DeploymentDraftRow> AcceptMake(IReadOnlyList<DeploymentDraftRow> rows)
    {
        AcceptedRows=CopyRows(rows);
        var previous=targetRows.ToDictionary(row=>row.Request.OfficerId);
        // The ordinary city caller overwrites the packet and restores exactly
        // its four prior Target policies. Old actions/waypoints are not retained.
        targetRows=CopyRows(rows.Select(row=>previous.TryGetValue(row.Request.OfficerId,out var old)
            ?row with{Request=row.Request with{Behavior=old.Request.Behavior}}:row).ToArray());
        editedPolicies.IntersectWith(targetRows.Select(row=>row.Request.OfficerId));
        return targetRows;
    }
    internal void CancelMake()=>AcceptedRows=[];
    internal void ReturnTarget(IReadOnlyList<DeploymentDraftRow> rows,IEnumerable<int>? editedPolicyOfficers=null)
    {
        var edited=(editedPolicyOfficers??EditedPolicyOfficers).ToHashSet();
        if(edited.Except(rows.Select(row=>row.Request.OfficerId)).Any())
            throw new ArgumentException("Edited policies must belong to this city's target packets.",nameof(editedPolicyOfficers));
        targetRows=CopyRows(rows);editedPolicies.Clear();editedPolicies.UnionWith(edited);
    }
    internal void AcceptPreferenceWorld(GameState expected,GameState replacement)
    {
        if(!ReferenceEquals(world,expected))throw new InvalidOperationException("The deployment city belongs to another world.");
        world=replacement;
    }

    internal static IReadOnlyList<DeploymentDraftRow> CopyRows(IReadOnlyList<DeploymentDraftRow> rows)=>
        Array.AsReadOnly(rows.Select(row=>row with{Request=row.Request with
        {
            TacticIds=Array.AsReadOnly(row.Request.TacticIds.ToArray()),
            Actions=Array.AsReadOnly(row.Request.Actions.ToArray()),
            WaypointHexIds=Array.AsReadOnly(row.Request.WaypointHexIds.ToArray())
        }}).ToArray());
}
