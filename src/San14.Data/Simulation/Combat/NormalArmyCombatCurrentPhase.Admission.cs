using San14.Core;

namespace San14.Data;

public enum NormalArmyPairAdmissionOutcome { CurrentInputsUnresolved, NativeSkip, Admitted }
public sealed record NormalArmyPairAdmission(NormalArmyPairAdmissionOutcome Outcome,
    int PairIndex, string SourceBranch);

public sealed partial class NormalArmyCombatCurrentPhase
{
    /// <summary>TC169CE0 reads current Army validity/exclusion and relations
    /// before reaching the pair's stages. A descriptor keeps its physical
    /// receiver after cleanup; a proved reset is a source skip, not an absent
    /// numerical input. This does not re-generate the phase descriptor cache.</summary>
    public NormalArmyPairAdmission ResolvePairAdmission(GameState current, int pairIndex)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (pairIndex < 0 || pairIndex >= plan.OrderedPairs.Length)
            throw new ArgumentOutOfRangeException(nameof(pairIndex));
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.phase_owner");
        if (captureDefinitions(current) is not { } definitions)
            return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.source_definitions");
        var context = createContext(definitions);
        var pair = plan.OrderedPairs[pairIndex];
        foreach (var descriptor in new[] { pair.Attacker, pair.Target })
        {
            if (!current.Units.TryGetValue(descriptor.ArmyId, out var army))
            {
                if (current.ReadArmyLifecycle() is not { } arena || descriptor.NativePoolSlot >= arena.ReceiverSlots().Length)
                    return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.receiver_header");
                var slot = arena.ReceiverSlots()[descriptor.NativePoolSlot];
                // Exact native validity short-circuit: commander0, then status0.
                // PendingImage alone does not prove pending70 membership.
                if (slot.PhysicalSlot != descriptor.NativePoolSlot || slot.ActiveUnitId is not null)
                    return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.receiver_identity");
                if (slot.Commander12 == 0 || slot.Commander12 is not null && slot.Status10 == 0)
                    return Result(NormalArmyPairAdmissionOutcome.NativeSkip, "169D.Army_native_invalid");
                return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.receiver_not_retired");
            }
            if (army.NativeArmyOrigin?.NativeSlotIndex != descriptor.NativePoolSlot ||
                !identities.TryGetValue(army.Id, out var identity) || identity.Officer != army.OfficerId)
                return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.receiver_identity");
            bool? valid = context.ArmyValid(current, army);
            if (valid is null)
                return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.Army_validity");
            if (!valid.Value) return Result(NormalArmyPairAdmissionOutcome.NativeSkip, "169D.Army_native_invalid");
            bool? excluded = context.ArmyManagerExcluded(current, army.Id);
            if (excluded is null)
                return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.Army_manager233560");
            if (excluded.Value) return Result(NormalArmyPairAdmissionOutcome.NativeSkip, "169D.Army_manager_excluded");
        }
        bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(current,
            current.Units[pair.Attacker.ArmyId].ForceId, current.Units[pair.Target.ArmyId].ForceId);
        if (hostile is null) return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.relation2CA650");
        if (!hostile.Value) return Result(NormalArmyPairAdmissionOutcome.NativeSkip, "169E.no_hostile_relation");
        if (TacticRemakeCurrentBattleControl.Read(current, 0x48) is not false)
            return Result(NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved, "current.tutorial_pair_gate497F20");
        return Result(NormalArmyPairAdmissionOutcome.Admitted, "169E.ordinary_pair_stages");

        NormalArmyPairAdmission Result(NormalArmyPairAdmissionOutcome outcome, string branch) =>
            new(outcome, pairIndex, branch);
    }
}
