using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The ordered offers and hired officers belonging to a force.</summary>
public sealed record PersonnelHireForceState(
    ImmutableArray<int> AvailableTemplateOfficerIds,
    ImmutableArray<int> HiredOfficerIds);

public sealed record PersonnelHireContract(int OfficerId, int TemplateOfficerId, int HiredYear);

/// <summary>A generated hire template, separate from the world's officer lookup.
/// Published template data is read-only; writers replace entries and hiring
/// copies the selected officer's mutable lists into its world-owned record.</summary>
public sealed record PersonnelHireTemplate(int Id, OfficerState Person)
{
    public int? PreviousForceId { get; init; }
}

public sealed record PersonnelHireGenerationPool(int KindId, int? Sex, ImmutableArray<int> Candidates);

public sealed record PersonnelHireGenerationPools(
    ImmutableArray<int> TypeCodes,
    ImmutableArray<int> FormationIds,
    ImmutableArray<PersonnelHireGenerationPool> Traits,
    ImmutableArray<PersonnelHireGenerationPool> Tactics);

/// <summary>The hire manager's remaining capacity and actual force rosters.</summary>
public sealed record PersonnelHireState(
    int RemainingSlots,
    ImmutableDictionary<int, PersonnelHireForceState> Forces,
    ImmutableDictionary<int, PersonnelHireContract> Contracts)
{
    public ImmutableDictionary<int, PersonnelHireTemplate>? Templates { get; init; }
    public PersonnelHireGenerationPools? GenerationPools { get; init; }
    public ImmutableArray<int>? PendingReleaseOfficerIds { get; init; }
}

public sealed partial record GameState
{
    public PersonnelHireState? PersonnelHire { get; init; }
}
