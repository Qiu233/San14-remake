using System.Collections.Immutable;

namespace San14.Core;

public sealed record BaseAutomaticTacticTroopPrefix(int SourceKind, int SourceId, int SourceTroops,
    int Object14, int SourceForce, int StageProduct);

/// <summary>The retained Base/governor instruction and already calculated
/// target prefix. It has neither an Army caster nor an Army chain.</summary>
public sealed record BaseAutomaticTacticExecutionContinuation(int CasterBaseLocationId, int GovernorOfficerId,
    int SourceForceId, int SourceHexId, int TacticId,
    ImmutableArray<AutomaticTacticInstructionSnapshot> Instructions, int InstructionIndex, int TargetIndex,
    int? TargetUnitId, int? TargetArmyPhysicalSlot, ArmyReceiverId? TargetReceiverId, int TargetHexId,
    int RequestedLoss, ImmutableArray<AutomaticTacticDamageContribution> DamageContributions,
    ImmutableArray<TacticReportEntry> Details, ImmutableArray<TacticResultPresentation> FirstInstructionResults,
    ImmutableArray<TacticResultPresentation> SecondInstructionResults,
    ImmutableArray<TacticEffectPresentation> ArmyPresentationEffects)
{
    public BaseAutomaticTacticTroopPrefix? TroopPrefix { get; init; }
    public SettlementBase? TargetBase { get; init; }
}
