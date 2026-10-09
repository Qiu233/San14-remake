using San14.Core;

namespace San14.Data;

public sealed record BaseCaptureCommanderRewardResult(GameState? State,
    PersonNormalArmyRewardResult? Growth, PersonPendingMeritResult? Merit,
    string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal BaseCaptureCommanderRewardResult ApplyBaseCaptureCommanderReward(GameState state,
        SourceWorldContext current, BaseReference target, ArmyUnitReference winner)
    {
        try
        {
            if (current.ReadArmyUnitNativeValidity(winner) is not bool valid)
                throw new InvalidDataException("current.capture_winner_validity");
            if (!valid) return new(state, null, null, null);
            int id = current.ReadArmyUnitCommanderId(winner) ?? throw new InvalidDataException("current.capture_Commander12");
            //Actual287C95..9C uses kind3 and amount0. It still executes the
            //normal Person's233560 gate; a live140 Base request can skip it.
            var growth = TryApplyNativeNormalPersonRewardDuringArmyOperation(state, id, 3, 0);
            if (growth.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                return new(null, growth, null, "current.capture_Commander_growth:" + growth.MissingPhase);
            state = growth.State;
            int physical = current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot
                ?? throw new InvalidDataException("current.capture_Commander_pointer");
            bool personValid = current.ReadPersonNativeValidity(physical)
                ?? throw new InvalidDataException("current.capture_Commander_validity");
            if (!personValid) return new(state, growth, null, null);
            int district = current.ReadBaseDistrictSlot(target) ?? throw new InvalidDataException("current.capture_oldBase78");
            int link = current.ReadBaseObjectLink(target) ?? throw new InvalidDataException("current.capture_Base4E");
            string key = district != 0 && link is >= 1 and <= 61
                ? link <= 51 ? SimulationParameterKeys.ArmyDestruction.CityCaptureCommanderMerit : SimulationParameterKeys.ArmyDestruction.GateCaptureCommanderMerit : SimulationParameterKeys.ArmyDestruction.UnassignedBaseCommanderMerit;
            int basis = ArmyDestructionParameterSeed.ReadCurrent(state, key)
                ?? throw new InvalidDataException($"current.capture_Commander_merit_{key}");
            //Source1695C0 param3=1 remains independent of the Army excluded
            //gate above. Do not deduplicate it with gross loss or kind3 merit.
            var merit = ApplyPendingMeritDuringArmyOperation(state, id, unchecked(basis * 10));
            return merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved
                ? new(null, growth, merit, "current.capture_Commander_pending_merit")
                : new(merit.State, growth, merit, null);
        }
        catch (InvalidDataException error) { return new(null, null, null, error.Message); }
    }
}
