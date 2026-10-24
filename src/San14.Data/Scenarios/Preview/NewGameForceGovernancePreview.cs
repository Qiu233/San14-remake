using San14.Core;

namespace San14.Data;

/// <summary>One read-only government-page publication. Roster preserves the
/// actual ordered normal departments, including pending values. Dictionary
/// keys are raw personnel references; values retain actual lookup identities.
/// Missing display fields do not disable unrelated browsing.</summary>
public sealed record NewGameForceGovernancePreview(int ForceId, bool? ForceValid,
    bool? CanEdit, GovernanceRosterView Roster,
    IReadOnlyDictionary<int, NewGameGovernanceOfficer> PeopleByReference,
    IReadOnlyList<NewGameGovernancePersonnelSlot> OrderedSlots,
    IReadOnlyList<string> UnresolvedInputs);

/// <summary>All35 raw slots, including known empty slots, in group/ordinal order.</summary>
public sealed record NewGameGovernancePersonnelSlot(int Group, int Ordinal, int ReferenceId, int PendingValue);

public sealed record NewGameGovernanceOfficer(int ReferenceId, int? PhysicalPerson,
    int? OfficerId, bool? NativeValid, string? Name, int? PolicyId,
    string? PolicyName, int? PolicyCost, string? UnresolvedInput);

/// <summary>Actual application state used by77D860/77E020/77E230. The ordinary
/// force-selection host supplies false from its route. Initializing does not
/// establish this input. This publication does not grant live edit permission.</summary>
public sealed record NewGameForceGovernanceApplication(bool? UserStrategyStatePresent);
