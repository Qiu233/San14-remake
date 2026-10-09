using San14.Core;

namespace San14.Data;

public sealed partial class OriginalTacticRemakeCurrentContext
{
    /// <summary>Finite zero-chain173B13..173C92 waiting assignment. Called
    /// only by the automatic pair owner after a real accepted selection. It
    /// performs no manual permission/primary selection or future tick replay.</summary>
    internal NativeTacticWaitingAssignment? ResolveAutomaticActivationWaiting(GameState state, UnitState caster,
        OriginalAutomaticTacticCandidateSource selected)
    {
        if (ArmyValid(state, caster) != true || EffectiveFormation(state, caster) is not int formation ||
            ResolveChain(state, caster, _catalog.Rows[selected.TacticSlot]) is not { ChainCount: 0 } chain ||
            state.NativeArmySimulation!.Forces.GetValueOrDefault(caster.ForceId)?.Flags193 is not byte flags)
            return null;
        int? primary = Parameter(state, 0x1A4A08C), second = Parameter(state, 0x1A4A098), third = Parameter(state, 0x1A4A0A4);
        int? adjustment = formation switch { 1 => Parameter(state, 0x1A4AA64), 2 => Parameter(state, 0x1A4AA70),
            14 => Parameter(state, 0x1A4AA08), _ => 0 };
        int? reduction = (flags & 0x40) != 0 ? Parameter(state, 0x1A4AC30) : 0;
        var reference = _definitions.ReadSkillTacticReference(selected.TacticSlot);
        byte? category = reference is null ? null : _definitions.ReadSkillTacticRawByte(reference, 0x40);
        int? policy = category switch { 5 => Policy(state, caster.ForceId, 0x24),
            6 => Policy(state, caster.ForceId, 0x25), null => null, _ => 0 };
        if (primary is not >= 0 or > 255 || second is not >= 0 or > 255 || third is not >= 0 or > 255 ||
            adjustment is null || reduction is null || policy is null) return null;
        var assigned = OriginalTacticEffectSourceWaiting.AfterActivation(new(chain.SumOfNativeWaitingContributions,
            0, adjustment.Value, reduction.Value, policy.Value, primary.Value, second.Value, third.Value));
        return new(assigned.Primary20, assigned.Second21, assigned.Third22,
            $"native-automatic-current:{_usage.Language}:{_usage.RuleVersion}:{Revision}");
    }

    internal TacticUseSourceResolution? ResolveAutomaticCapturedEffects(GameState state, UnitState caster,
        OriginalAutomaticTacticCandidateSource selected) =>
        new OriginalTacticEffectSourceRules(_catalog, this, new(new OriginalHexTravelMap(_map)))
            .ResolveCapturedInstructionUse(state, caster, selected.TacticSlot, selected.Program, selected.Instructions);
}
