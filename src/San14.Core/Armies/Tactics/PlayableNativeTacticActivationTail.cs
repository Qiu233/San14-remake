namespace San14.Core;

/// <summary>Source tail after ordered effects, before native waiting assignment.
/// The ordinary nondamage source actor can write its commander's pending merit.
/// Null means a reached current input is unresolved, never a successful skip.</summary>
public interface IPlayableNativeTacticActivationTailSource
{
    bool HasNativeActivationCommanderTail => true;
    OfficerState? ResolveNativeActivationCommanderTail(GameState state, UnitState caster,
        PlayableTacticDefinition definition);
}
