using System.Text.Json.Serialization;

namespace San14.Core;

public static class OfficerServiceKinds
{
    public const int Ruler = 1;
    public const int DistrictLeader = 2;
    public const int Governor = 3;
    public const int ServingOfficer = 4;
    public const int Free = 5;
    public const int Captive = 6;
}

/// <summary>Independent knowledge of an officer's service or custody state.
/// Role, presence and affiliation are separate facts; a custody transition
/// does not implicitly rewrite those facts. Unrecognized identities survive
/// import and saving without acquiring invented behavior.</summary>
public sealed record OfficerServiceStatus
{
    [JsonConstructor]
    public OfficerServiceStatus(int kindId)
    {
        if (kindId < 0) throw new InvalidDataException("Officer service status cannot be negative.");
        KindId = kindId;
    }

    public int KindId { get; }
    public static OfficerServiceStatus Ruler { get; } = new(OfficerServiceKinds.Ruler);
    public static OfficerServiceStatus DistrictLeader { get; } = new(OfficerServiceKinds.DistrictLeader);
    public static OfficerServiceStatus Governor { get; } = new(OfficerServiceKinds.Governor);
    public static OfficerServiceStatus ServingOfficer { get; } = new(OfficerServiceKinds.ServingOfficer);
    public static OfficerServiceStatus Free { get; } = new(OfficerServiceKinds.Free);
    public static OfficerServiceStatus Captive { get; } = new(OfficerServiceKinds.Captive);
}

public static class OfficerServiceRules
{
    public static bool IsCaptive(OfficerServiceStatus? status) => status == OfficerServiceStatus.Captive;
    public static bool IsServing(OfficerServiceStatus? status) => status?.KindId is
        OfficerServiceKinds.Ruler or OfficerServiceKinds.DistrictLeader or
        OfficerServiceKinds.Governor or OfficerServiceKinds.ServingOfficer;

    /// <summary>Only explicit status publications update the coarse role.
    /// Custody and unknown identities retain the previously declared role.</summary>
    public static OfficerRole PublishedRole(OfficerServiceStatus? status, OfficerRole retainedRole) => status?.KindId switch
    {
        OfficerServiceKinds.Ruler => OfficerRole.Ruler,
        OfficerServiceKinds.DistrictLeader => OfficerRole.DistrictLeader,
        OfficerServiceKinds.Governor => OfficerRole.Governor,
        OfficerServiceKinds.ServingOfficer => OfficerRole.Officer,
        OfficerServiceKinds.Free => OfficerRole.Free,
        _ => retainedRole
    };
}

public sealed partial record OfficerState
{
    public OfficerServiceStatus? ServiceStatus { get; init; }
}
