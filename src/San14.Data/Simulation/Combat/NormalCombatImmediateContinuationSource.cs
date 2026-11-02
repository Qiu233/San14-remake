using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ResumeImmediateCombatSettlementCurrent(GameState state, SourceWorldContext current,
        NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        if (CaptureArmyReturnContinuationSource.Read(state) is { CaptureTail.SettlementIndex: -1 } ||
            state.BaseCaptureContinuation is { SettlementIndex: -1 } ||
            state.CityCaptureEmperorTail is { SettlementIndex: -1 } || state.BaseWinnerEmperorTail is { SettlementIndex: -1 })
            return ResumeImmediateBaseCaptureCurrent(state, current, date, revision, effects).State;
        return ResumeNormalCombatImmediateDefeatCurrent(state, current, date, revision);
    }

    private static NormalCombatImmediateContinuation CaptureImmediateCombatContinuation(
        GameState phaseEntry, NormalCombatMixedProjection projection,
        IReadOnlyDictionary<int, NormalArmyCombatDescriptor> descriptors,
        int pairIndex, ushort reportWord20, TacticSourceSamplerWorld sampler)
    {
        //The phase's immutable entry state retains identities of receivers
        //retired by earlier pairs. Later live state must not replace them.
        var armies = descriptors.ToImmutableDictionary(item => item.Key, item =>
        {
            var descriptor = item.Value;
            var person = phaseEntry.Units.GetValueOrDefault(item.Key)?.OfficerId
                ?? throw ImmediateCombatMissing("phase_entry_commander");
            return new NormalCombatArmySnapshot(descriptor.ArmyId, descriptor.NativePoolSlot, person,
                descriptor.CappedMorale8, descriptor.TroopsA, descriptor.NativeHexC,
                descriptor.NativeForce10, descriptor.AcceptedPrimaryCount14);
        });
        return new(projection.Actors.Select(row => Save(row.Actor)).ToImmutableArray(),
            projection.OrderedPairs.Select(pair => new NormalCombatPairSnapshot(pair.GenerationOrdinal,
                Save(pair.Actor), Save(pair.Target))).ToImmutableArray(), armies,
            pairIndex, reportWord20, sampler.Day450, sampler.Salt45C);
    }

    private static NormalCombatImmediateContinuation ContinueImmediateCombatSnapshot(
        NormalCombatImmediateContinuation original, int pairIndex, ushort reportWord20) =>
        original with { PairIndex = pairIndex, ReportWord20 = reportWord20 };

    private static NormalCombatMixedProjection RestoreImmediateCombatProjection(NormalCombatImmediateContinuation saved) =>
        new(saved.Actors.Select(actor => new NormalCombatActorTargets(Restore(actor), [], false)).ToArray(),
            saved.OrderedPairs.Select(pair => new NormalCombatMixedPair(pair.GenerationOrdinal,
                Restore(pair.Actor), Restore(pair.Target))).ToArray());

    private static Dictionary<int, NormalArmyCombatDescriptor> RestoreImmediateCombatDescriptors(
        NormalCombatImmediateContinuation saved) => saved.ArmyDescriptors.ToDictionary(item => item.Key,
            item => new NormalArmyCombatDescriptor(item.Value.ArmyId, item.Value.NativePoolSlot,
                item.Value.CappedMorale, item.Value.Troops, item.Value.HexId,
                item.Value.ForceId, item.Value.AcceptedCount));

    private static NormalCombatParticipantSnapshot Save(NormalCombatParticipant value) => new(value.Kind,
        value.PhysicalSlot, value.UnitId, value.Morale, value.Troops, value.HexId, value.ForceSlot, value.AcceptedCount);

    private static NormalCombatParticipant Restore(NormalCombatParticipantSnapshot value) => new(value.Kind,
        value.PhysicalSlot, value.UnitId, value.Morale, value.Troops, value.HexId, value.ForceSlot, value.AcceptedCount);
}
