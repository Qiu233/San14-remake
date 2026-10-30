using System.Collections.Immutable;

namespace San14.Core;

public enum ArmyGroupOfficerKind
{
    Officer,
    NoOfficer,
    DefaultOfficer
}

/// <summary>Commander identity used for group leadership. A default lookup
/// officer is distinct from an absent officer, even when neither is playable.</summary>
public sealed record ArmyGroupOfficerReference(ArmyGroupOfficerKind Kind, int? OfficerId = null)
{
    public static ArmyGroupOfficerReference None { get; } = new(ArmyGroupOfficerKind.NoOfficer);
    public static ArmyGroupOfficerReference Default { get; } = new(ArmyGroupOfficerKind.DefaultOfficer);
}

public sealed record ArmyGroupAssignment(bool IsKnown, int? GroupId);
public sealed record ArmyGroupLeader(bool IsKnown, ArmyGroupOfficerReference Officer);

/// <summary>The caution-group objective of a military group. A known null
/// means the group has another objective kind. Zero remains a cleared caution
/// objective, distinct from another kind.</summary>
public sealed record ArmyGroupCautionTarget(bool IsKnown, int? CautionGroupId);

/// <summary>Military groups own assignments and leaders. Their ordered
/// membership comes from the world's army roster, not a second saved roster.</summary>
public sealed record ArmyGroupsState
{
    public Dictionary<int, ArmyGroupAssignment> UnitAssignments { get; init; } = [];
    public Dictionary<int, ArmyGroupLeader> Leaders { get; init; } = [];
    public Dictionary<int, ArmyGroupCautionTarget>? CautionTargets { get; init; } = [];
    // Unknown owner or absent row stays unknown. Only an actual creation,
    // reset or purpose/relay writer installs these immutable planning facts.
    public ImmutableDictionary<int, ArmyGroupObjectiveState>? Objectives { get; init; }
    /// <summary>Actual original-policy group contexts. Null retains unknown
    /// legacy state; only the policy manager's construction/registration creates
    /// known contexts. A reset releases the previous context for that group.</summary>
    public ImmutableDictionary<int, OriginalAiPolicyActorState>? OriginalAiContexts { get; init; }
    public OriginalAiMilitaryGroupPrototype? OriginalAiContextPrototype { get; init; }
    public ArmyGroupOfficerReference ResetLeader { get; init; } = ArmyGroupOfficerReference.None;

    // An unused group has the normal initial leader. Unknown imported leaders
    // are represented explicitly, rather than by omission.
    public ArmyGroupLeader GetLeader(int groupId) => Leaders.GetValueOrDefault(groupId)
        ?? new(true, ResetLeader);
    public ArmyGroupCautionTarget GetCautionTarget(int groupId)
    {
        if (Objectives?.TryGetValue(groupId, out var objective) == true && objective.Purpose is { } purpose)
            return new(true, purpose.Kind == ArmyGroupPurposeKind.InterceptCautionGroup ? purpose.Target.Id : null);
        return CautionTargets is null ? new(false, null) : CautionTargets.GetValueOrDefault(groupId) ?? new(true, null);
    }

    public ArmyGroupsState ResetObjective(int groupId) => this with
    {
        Objectives = (Objectives ?? ImmutableDictionary<int, ArmyGroupObjectiveState>.Empty)
            .SetItem(groupId, ArmyGroupObjectiveState.Reset),
        OriginalAiContexts = OriginalAiContexts?.Remove(groupId)
    };

    /// <summary>The actual creation/register tail reuses an existing context;
    /// it never reconstructs one from a query or an unknown legacy owner.</summary>
    public ArmyGroupsState RegisterOriginalAiContext(int groupId)
    {
        if (OriginalAiContexts is null || OriginalAiContextPrototype is null || OriginalAiContexts.ContainsKey(groupId)) return this;
        return this with { OriginalAiContexts = OriginalAiContexts.Add(groupId,
            new(new(OriginalAiPolicyActorKind.MilitaryGroup, groupId), OriginalAiContextPrototype.BundleKey,
                [], OriginalAiContextPrototype.InitialBlackboard)) };
    }

    public ArmyGroupsState Copy() => this with
    {
        UnitAssignments = new(UnitAssignments),
        Leaders = new(Leaders),
        CautionTargets = CautionTargets is null ? null : new(CautionTargets)
    };
}

/// <summary>Current commander facts projected by the game's data boundary.
/// Leadership eligibility and recognized districts are policy facts, not
/// source table limits embedded in Core.</summary>
public sealed record ArmyGroupCommander(ArmyGroupOfficerReference Officer,
    bool IsValid, int? DistrictId, bool HasRecognizedDistrict, bool CanLeadGroup);

public sealed record ArmyGroupDistrict(int? DistrictId, bool IsRecognized);

public enum ArmyGroupLeaderPolicy
{
    PreserveForPlayerDistrict,
    AssignCommander
}
