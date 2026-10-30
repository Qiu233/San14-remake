using San14.Data;

namespace San14.Client.Gameplay;

internal sealed record GovernanceRosterOfficer(int Id, string Name, string? PolicyName,
    int? PolicyCost, int? PendingValue, bool Selectable = true)
{
    internal bool? NativeValid { get; init; }
    internal int? AppearanceIndex { get; init; }
    internal string? FaceResourceName { get; init; }
    internal string? FaceSpriteName { get; init; }
    internal int? PolicyId { get; init; }
    internal int? PolicyLevel { get; init; }
    internal IReadOnlyList<int>? PolicyEffectSlots { get; init; }
    internal string? PolicyDescription { get; init; }
    internal string? PolicyIcon { get; init; }
    internal bool CostDiscount { get; init; }
    internal int? Intelligence { get; init; }
    internal int? ServiceStatus { get; init; }
}
internal sealed record GovernanceRosterDepartment(int Group, string Name, int? Capacity, int? Expansion,
    IReadOnlyList<GovernanceRosterOfficer> Members, IReadOnlyList<GovernanceRosterOfficer> Candidates)
{
    internal int? MaximumCapacity { get; init; }
    internal IReadOnlyList<GovernanceRosterOfficer?>? SlotMembers { get; init; }
}
internal sealed record GovernanceRosterPageModel(string ForceName, GovernanceRosterOfficer? Advisor,
    IReadOnlyList<GovernanceRosterOfficer> AdvisorCandidates, IReadOnlyList<GovernanceRosterDepartment> Departments,
    bool CanEdit)
{
    internal GovernanceRosterOfficer? Ruler { get; init; }
    internal string? RulerResourceName { get; init; }
    internal string? RulerSpriteName { get; init; }
    internal string? RulerPortraitUnresolvedInput { get; init; }
    internal int? DoctrineId { get; init; }
    internal ForceDoctrineProgress? DoctrineProgress { get; init; }
    internal DetailForceAppearance? Flag { get; init; }
    internal int? CommandBooksAvailable { get; init; }
    internal bool ShowCommandBooks { get; init; }
    internal int? NewAppointmentPending { get; init; }
    internal int? RemainingExpansions { get; init; }
    internal IReadOnlyDictionary<int, bool> CanExpand { get; init; } = new Dictionary<int, bool>();
}
internal sealed record GovernanceDepartmentSelection(int Group, IReadOnlyList<int> OfficerIds);
internal sealed record GovernanceRosterSelection(int? AdvisorId, IReadOnlyList<GovernanceDepartmentSelection> Departments)
{
    internal GovernanceRosterSelection WithPick(int? group,IReadOnlyList<int> values,
        IReadOnlyList<GovernanceRosterDepartment> source)
    {
        var next=Departments.ToDictionary(row=>row.Group,row=>row.OfficerIds.ToList());
        int? advisor=AdvisorId;
        if(group is int id)
        {
            next[id]=values.ToList();
            if(advisor is int current&&values.Contains(current))advisor=null;
        }
        else
        {
            advisor=values.Count==0?null:values[0];
            if(advisor is int person&&advisor!=AdvisorId)
            {
                var department=source.FirstOrDefault(row=>row.Candidates.Any(candidate=>candidate.Id==person));
                if(department is not null)next[department.Group].Remove(person);
            }
        }
        return new(advisor,Departments.Select(row=>new GovernanceDepartmentSelection(row.Group,next[row.Group].ToArray())).ToArray());
    }
}
internal sealed record GovernanceRosterEvaluationView(bool? Allowed, int? CommandBooks, int? PolicyCost,
    int? PolicyCostLimit, bool? ExceedsLevelLimit, string Status);
