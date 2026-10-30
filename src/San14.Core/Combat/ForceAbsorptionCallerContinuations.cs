using System.Collections.Immutable;

namespace San14.Core;

public sealed record DemandSurrenderAbsorptionTail(PersonnelDiplomacyTask Task, bool OwnPlayer, int? ChronicleSlot);
public sealed record DemandSurrenderReportSnapshot(int Actor, int OwnRuler, int TargetRuler,
    int ActorDistrict, int TargetDistrict, int TargetSpeaker, int TargetHex,
    bool OwnPlayer, bool TargetPlayer, string TargetName);
public sealed record DemandSurrenderCompletionTail(PersonnelDiplomacyTask Task, bool Accepted,
    NativeMapSeasonDate OperationDate, DemandSurrenderReportSnapshot Report);
public sealed record PlayerCoalitionAbsorptionTail(int PlayerForcePhysicalSlot, int HeadForcePhysicalSlot,
    ImmutableArray<int> RemainingMemberForcePhysicalSlots, NativeMapSeasonDate CooldownSourceDate,
    bool Visible, string? MembershipText);
public sealed record NpcForceAbsorptionTail(int PlayerForcePhysicalSlot, int SurvivorForcePhysicalSlot,
    bool Visible, string? Narration, int CameraHex);
public sealed record StoryForceAbsorptionTail(int OriginalForcePhysicalSlot, int RecipientForcePhysicalSlot,
    bool RestoreRecordingGate);

public sealed partial record GameState
{
    public DemandSurrenderAbsorptionTail? DemandSurrenderAbsorptionTail { get; init; }
    public DemandSurrenderCompletionTail? DemandSurrenderCompletionTail { get; init; }
    public PlayerCoalitionAbsorptionTail? PlayerCoalitionAbsorptionTail { get; init; }
    public NpcForceAbsorptionTail? NpcForceAbsorptionTail { get; init; }
    public StoryForceAbsorptionTail? StoryForceAbsorptionTail { get; init; }
}
