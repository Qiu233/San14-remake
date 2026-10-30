using System.Collections.Immutable;

namespace San14.Core;

/// <summary>An already selected automatic instruction and its retained ordered
/// Hexes. Resuming does not rebuild targets from the changed combat world.</summary>
public sealed record AutomaticTacticInstructionSnapshot(int Index, byte Opcode, byte Raw64,
    byte EffectValue, byte Raw68, ImmutableArray<int> OrderedHexIds);

public sealed record AutomaticTacticTroopPrefix(int SourceArmyPhysicalSlot, ushort SourceTroops,
    byte SourceForceSlot, int StageProduct);

public sealed record AutomaticTacticMoralePrefix(int SourceArmyPhysicalSlot, int SourceHexId,
    float StageFactor, float WaitingFactor)
{
    public bool SourceHexIsNull { get; init; }
}

/// <summary>The source identities captured on entering this actor, before an
/// instruction can retire its Army or clear the commander's live binding.</summary>
public sealed record AutomaticTacticActorSource(int ForceId, int CommanderOfficerId,
    int? CommanderPersonPhysical);

public sealed record AutomaticTacticDamageContribution(int Kind, int PhysicalSlot, int RequestedLoss);

public sealed record AutomaticTacticLinkedActorPlan(int CasterUnitId, int TacticId,
    ImmutableArray<AutomaticTacticInstructionSnapshot> Instructions, int SourceArmyPhysicalSlot);

public sealed record AutomaticTacticLinkedActorResult(int CasterUnitId, ArmyReceiverId Actor, int TacticId,
    ImmutableArray<TacticResultPresentation> FirstInstructionResults,
    ImmutableArray<TacticResultPresentation> SecondInstructionResults,
    ImmutableArray<TacticEffectPresentation> ArmyPresentationEffects, int SourceArmyPhysicalSlot, bool WasExecuted);

/// <summary>The already selected successors and completed predecessors of the
/// current actor. Current instruction and target progress remain on its owner.</summary>
public sealed record AutomaticTacticLinkedExecutionContinuation(
    ImmutableArray<AutomaticTacticLinkedActorPlan> RemainingActors,
    ImmutableArray<AutomaticTacticLinkedActorResult> CompletedActors, int CategoryPolicyReduction);

/// <summary>The automatic tactic's already calculated instruction/target prefix
/// awaiting the nested destruction decision. Random state and accumulated results
/// belong to this execution, independently of the enclosing mixed combat pair.</summary>
public sealed record AutomaticTacticExecutionContinuation(int CasterUnitId, int TacticId,
    ImmutableArray<AutomaticTacticInstructionSnapshot> Instructions, int InstructionIndex, int TargetIndex,
    int? TargetUnitId, int? TargetArmyPhysicalSlot, ArmyReceiverId? TargetReceiverId, int TargetHexId,
    int RequestedLoss, int ChainWaitingContribution, ulong ChainRandomState,
    ImmutableArray<AutomaticTacticDamageContribution> DamageContributions,
    ImmutableArray<TacticReportEntry> Details,
    ImmutableArray<TacticResultPresentation> FirstInstructionResults,
    ImmutableArray<TacticResultPresentation> SecondInstructionResults,
    ImmutableArray<TacticEffectPresentation> ArmyPresentationEffects)
{
    public AutomaticTacticTroopPrefix? TroopPrefix { get; init; }
    public AutomaticTacticMoralePrefix? MoralePrefix { get; init; }
    public AutomaticTacticActorSource? EntrySource { get; init; }
    public SettlementBase? TargetBase { get; init; }
    /// <summary>When present, ChainWaitingContribution is the completed actors'
    /// contribution sum. Null preserves the legacy empty-chain interpretation.</summary>
    public AutomaticTacticLinkedExecutionContinuation? LinkedExecution { get; init; }
}
