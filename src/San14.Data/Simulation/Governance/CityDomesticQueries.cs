using San14.Core;

namespace San14.Data;

public enum CityDomesticDuty { Recruiter, Trainer }
public sealed record CityDomesticCompletionIntent(int CityId, long DraftRevision,
    IReadOnlyList<CityDomesticStageOperation> Operations, CityDomesticTerminal Terminal, CityDomesticPolicy Policy);
public sealed record CityDomesticCandidate(ushort RawIdentity, int PhysicalSlot);
public sealed record CityDomesticPolicyState(CityDomesticPolicy Requested,
    AreaAdministrationQuery<CityDomesticPolicy?> Effective,
    AreaAdministrationQuery<bool?> Commerce, AreaAdministrationQuery<bool?> Agriculture,
    AreaAdministrationQuery<bool?> Barracks, AreaAdministrationQuery<bool?> Delegated)
{
    public AreaAdministrationQuery<bool?> CommerceComplete { get; init; } = new(null, "city-domestic.policy_commerce_complete");
    public AreaAdministrationQuery<bool?> AgricultureComplete { get; init; } = new(null, "city-domestic.policy_agriculture_complete");
    public AreaAdministrationQuery<bool?> BarracksComplete { get; init; } = new(null, "city-domestic.policy_barracks_complete");
}
public sealed record CityDomesticDutyView(int CityId, long Revision, CityDomesticDuty Duty,
    AreaAdministrationQuery<int?> LookupIdentity, AreaAdministrationQuery<int?> PhysicalSlot,
    AreaAdministrationQuery<bool?> NativeValid, AreaAdministrationQuery<AreaAdministrationOfficerCard> Card,
    AreaAdministrationQuery<int?> Fee, AreaAdministrationQuery<int?> Before, AreaAdministrationQuery<int?> After);
public sealed record CityDomesticCommandBooks(AreaAdministrationQuery<int?> Current,
    AreaAdministrationQuery<int?> Required, AreaAdministrationQuery<int?> After,
    AreaAdministrationQuery<bool?> SourceBudgetCanConfirm);
public sealed record CityDomesticSummary(int CityId, string CityName, long Revision,
    AreaAdministrationQuery<int?> Gold, AreaAdministrationQuery<bool?> GoldWarning,
    CityDomesticCommandBooks CommandBooks)
{
    public AreaAdministrationQuery<bool?> CanCommit { get; } =
        AreaAdministrationQuery<bool?>.Unknown("city-domestic.formal_admission_requires_execution");
}
public sealed record CityDomesticPage(CityDomesticSummary Summary,
    AreaAdministrationQuery<CityDomesticDutyView> Recruiter,
    AreaAdministrationQuery<CityDomesticDutyView> Trainer,
    AreaAdministrationQuery<CityDomesticPolicyState> Policy);

/// <summary>A role's first candidate publication. Its owning draft retains this
/// actual instance; subsequent cell revisions do not repeat the initial sort.</summary>
public sealed class CityDomesticPersonSelection
{
    internal CityDomesticPersonSelection(int cityId, CityDomesticDuty duty,
        IReadOnlyList<CityDomesticCandidate> candidates)
    {
        CityId = cityId; Duty = duty;
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }
    public int CityId { get; }
    public CityDomesticDuty Duty { get; }
    public IReadOnlyList<CityDomesticCandidate> Candidates { get; }
}

public enum CityDomesticPersonSortColumn
{
    Name, Leadership, Strength, RolePersonality, PrimaryResponsibility,
    SecondaryResponsibility, TertiaryResponsibility, Returning, AreaLocation, Task,
    Force, District, Home, CurrentLocation, ServiceStatus, Loyalty, ServiceYears,
    Intelligence, Politics, Charm, Health, ItemCount, Office, OtherMetric, Doctrine,
    Age, Sex, Father, Mother, Partner, Sworn, TaskTarget, TaskRemaining,
    Merit, CreditRankStage, CommandBonus, AdditionalAreaCapacity, CreditKind3Bonus, ResetCounter
}
public enum CityDomesticPersonSortDirection { Natural, Reversed }

/// <summary>Display values only. Known null is a source empty/not-applicable
/// value; a missing input belongs to that cell, independently of row paint.</summary>
public sealed record CityDomesticPersonBasicRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name,
    AreaAdministrationQuery<int?> Leadership, AreaAdministrationQuery<int?> Strength,
    AreaAdministrationQuery<bool?> RolePersonality,
    AreaAdministrationQuery<DeploymentPersonResponsibility?> PrimaryResponsibility,
    AreaAdministrationQuery<DeploymentPersonResponsibility?> SecondaryResponsibility,
    AreaAdministrationQuery<DeploymentPersonResponsibility?> TertiaryResponsibility,
    AreaAdministrationQuery<bool?> Returning, AreaAdministrationQuery<bool?> AreaLocation,
    AreaAdministrationQuery<int?> Task, AreaAdministrationQuery<int?> RowState);

public sealed record CityDomesticPersonBasicPage(CityDomesticPersonSelection Selection, long Revision,
    IReadOnlyList<CityDomesticPersonBasicRow> Rows,
    AreaAdministrationQuery<int?> PreselectedOccurrence);

public sealed record CityDomesticMenuAdmission(bool Present, bool Enabled);

/// <summary>One selected tab's current cells over the retained role publication.</summary>
public sealed record CityDomesticPersonPage<TRow>(CityDomesticPersonSelection Selection, long Revision,
    IReadOnlyList<TRow> Rows, AreaAdministrationQuery<int?> PreselectedOccurrence);
public sealed record CityDomesticPersonTaskRemaining(bool NotApplicable, int? Value);

public sealed record CityDomesticPersonForceName(string Text, AreaMarkerColor MarkerColour);

public sealed record CityDomesticPersonAffiliationRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, AreaAdministrationQuery<int?> RowState,
    AreaAdministrationQuery<CityDomesticPersonForceName?> ForceName, AreaAdministrationQuery<int?> DistrictNumber,
    AreaAdministrationQuery<string?> HomeName, AreaAdministrationQuery<string?> CurrentLocationName,
    AreaAdministrationQuery<DeploymentPersonServiceStatus?> ServiceStatus,
    AreaAdministrationQuery<DeploymentPersonResponsibility?> PrimaryResponsibility,
    AreaAdministrationQuery<int?> Loyalty, AreaAdministrationQuery<string?> OfficeName,
    AreaAdministrationQuery<int?> OtherMetric, AreaAdministrationQuery<int?> ServiceYears);
public sealed record CityDomesticPersonAbilityRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, AreaAdministrationQuery<int?> RowState,
    AreaAdministrationQuery<DeploymentPersonServiceStatus?> ServiceStatus,
    AreaAdministrationQuery<int?> Leadership, AreaAdministrationQuery<int?> Strength,
    AreaAdministrationQuery<int?> Intelligence, AreaAdministrationQuery<int?> Politics,
    AreaAdministrationQuery<int?> Charm, AreaAdministrationQuery<int?> HealthCode,
    AreaAdministrationQuery<int?> ItemCount);
public sealed record CityDomesticPersonMeritRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, AreaAdministrationQuery<int?> RowState,
    AreaAdministrationQuery<string?> RankName, AreaAdministrationQuery<int?> Merit,
    AreaAdministrationQuery<int?> CommandBonus, AreaAdministrationQuery<int?> AdditionalAreaCapacity,
    AreaAdministrationQuery<int?> CreditKind3Bonus);
public sealed record CityDomesticPersonTaskRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, AreaAdministrationQuery<int?> RowState,
    AreaAdministrationQuery<int?> Task, AreaAdministrationQuery<string?> TargetName,
    AreaAdministrationQuery<CityDomesticPersonTaskRemaining?> Remaining,
    AreaAdministrationQuery<bool?> Returning, AreaAdministrationQuery<int?> ResetValue);
public sealed record CityDomesticPersonPersonalRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, AreaAdministrationQuery<int?> RowState,
    AreaAdministrationQuery<int?> SexCode, AreaAdministrationQuery<int?> AgeValue,
    AreaAdministrationQuery<string?> DoctrineName, AreaAdministrationQuery<string?> FatherName,
    AreaAdministrationQuery<string?> MotherName, AreaAdministrationQuery<string?> PartnerName,
    AreaAdministrationQuery<string?> SwornName);
