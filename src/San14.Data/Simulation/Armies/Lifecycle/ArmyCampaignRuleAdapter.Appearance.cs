using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Source stage9's midnight appearance batch. Actual at-home
    /// qualification and hold/physical ordering precede all writes; any unknown
    /// reached branch discards the whole private batch. Calendar belongs to the
    /// existing driver, and every other unimplemented stage remains unresolved.</summary>
    public OrdinaryArmyAppearanceResult TryAdvanceOrdinaryAppearance(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Appearance ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army appearance requires its reached source stage9.");
        if (moment.PreWriterThirdDate.Hour != 0) return new(state, [], null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.Version);
            var selected = new List<(int Physical, int Unit, byte Hold)>();
            foreach (int physical in queries.ReadRegistry() ?? throw OrdinaryArmyAppearanceSource.Missing("registry"))
            {
                var receiver = queries.ReadReceiver(physical);
                if (!(receiver.IsValid ?? throw OrdinaryArmyAppearanceSource.Missing("registry_Army_validity"))) continue;
                if (!(queries.IsAtHome2C96E0(physical) ?? throw OrdinaryArmyAppearanceSource.Missing("at_home_qualification"))) continue;
                if (receiver.UnitId is not int id || !state.Units.TryGetValue(id, out var unit))
                    throw OrdinaryArmyAppearanceSource.Missing("active_Army_owner");
                selected.Add((physical, id, unit.NativeRouteHold56 ?? throw OrdinaryArmyAppearanceSource.Missing("sort_hold")));
            }
            var working = state;
            var receipts = new List<OrdinaryArmyAppearanceReceipt>();
            foreach (var actor in selected.OrderBy(a => a.Hold).ThenBy(a => a.Physical))
            {
                //CAppearArmyState snapshots once; update checks validity again,
                //without re-running the at-home filter after earlier actors.
                current = CaptureWritableTransportRouteCurrent(working);
                if (!(current.ReadArmyUnitNativeValidity(new(actor.Physical)) ??
                      throw OrdinaryArmyAppearanceSource.Missing("update_Army_validity"))) continue;
                if (!working.Units.TryGetValue(actor.Unit, out var army))
                    throw OrdinaryArmyAppearanceSource.Missing("update_Army_owner");
                var result = OrdinaryArmyAppearanceSource.Depart(working, current, army);
                working = result.State; receipts.Add(result.Receipt);
            }
            return new(working, receipts.ToArray(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }
}
