using System.Collections.Immutable;

namespace San14.Core;

/// <summary>An already selected automatic instruction and its retained ordered
/// Hexes. Resuming does not rebuild targets from the changed combat world.</summary>
public sealed record AutomaticTacticInstructionSnapshot(int Index, byte Opcode, byte Raw64,
    byte EffectValue, byte Raw68, ImmutableArray<int> OrderedHexIds);

public sealed record AutomaticTacticTroopPrefix(int SourceArmyPhysicalSlot, ushort SourceTroops,
    byte SourceForceSlot, int StageProduct);

public sealed record AutomaticTacticMoralePrefix(int SourceArmyPhysicalSlot, int SourceHexId,
    float StageFactor, float WaitingFactor);

public sealed record AutomaticTacticDamageContribution(int Kind, int PhysicalSlot, int RequestedLoss);

/// <summary>The automatic tactic's already calculated instruction/target prefix
/// awaiting the nested destruction decision. Random state and accumulated results
/// belong to this execution, independently of the enclosing mixed combat pair.</summary>
public sealed record AutomaticTacticExecutionContinuation(int CasterUnitId, int TacticId,
    ImmutableArray<AutomaticTacticInstructionSnapshot> Instructions, int InstructionIndex, int TargetIndex,
    int TargetUnitId, int TargetArmyPhysicalSlot, ArmyReceiverId TargetReceiverId, int TargetHexId,
    int RequestedLoss, int ChainWaitingContribution, ulong ChainRandomState,
    ImmutableArray<AutomaticTacticDamageContribution> DamageContributions,
    ImmutableArray<TacticReportEntry> Details,
    ImmutableArray<TacticResultPresentation> FirstInstructionResults,
    ImmutableArray<TacticResultPresentation> SecondInstructionResults,
    ImmutableArray<TacticEffectPresentation> ArmyPresentationEffects)
{
    public AutomaticTacticTroopPrefix? TroopPrefix { get; init; }
    public AutomaticTacticMoralePrefix? MoralePrefix { get; init; }
}
