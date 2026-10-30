using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IDistrictMilitaryIntelligenceTransferRules
{
    /// <summary>054180 chooses one real Force-registry district, retaining the
    /// primary's30 percent reserve and the source integer/truncation order.</summary>
    internal OriginalAiCommandPreparation PrepareOriginalAiSpyAllocation(GameState state,
        SourceWorldContext current, OriginalAiActor actor)
    {
        var own = ReadOriginalAiMyselfForce(current, actor);
        if (own is not { } identity) return new(false, null);
        var force = current.ReadForceTableReference(identity.Id) ?? throw SpyAllocationMissing("Force_pointer");
        if (!SpyAllocationNeed(current.ReadForceNativeValidity(force), "Force_validity")) return new(false, null);
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        int primaryId = SpyAllocationNeed(primary.PhysicalSlot, "primary_District_identity");
        if (!SpyAllocationNeed(current.ReadDistrictNativeValidity(primaryId), "primary_District_validity")) return new(false, null);

        var owned = new List<int>();
        int total = 0;
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw SpyAllocationMissing("District_registry"))
        {
            int district = SpyAllocationNeed(entry?.PhysicalSlot, "District_registry_payload");
            //334790 filters by actual Force ordinal and District10. Its
            //iterator does not add a native-validity filter of its own.
            if (identity.Id is < 1 or > 51 ||
                SpyAllocationNeed(current.ReadDistrictForceId(district), "District10_filter") != identity.Id) continue;
            owned.Add(district);
            if (SpyAllocationNeed(current.ReadDistrictNativeValidity(district), "sum_District_validity"))
                total = unchecked(total + ReadDistrictAdministrationIntelligenceCurrent(current, district));
        }
        int reserve = unchecked(total * 30) / 100;
        int remaining = unchecked(total - reserve);
        int cities = CityCount(identity.Id, null);
        if (cities == 0) throw SpyAllocationMissing("Force_City_count_divisor");
        int perCity = remaining / cities;
        //22AAC0 ignores its recovered extra argument; the source does not
        //add the division remainder to the primary's allocation.
        int primaryTarget = unchecked(CityCount(identity.Id, primaryId) * perCity + reserve);
        int excess = unchecked(ReadDistrictAdministrationIntelligenceCurrent(current, primaryId) - primaryTarget);
        if (excess <= 0) return new(true, null);

        int largestDeficit = 0;
        int? destination = null;
        foreach (int district in owned)
        {
            if (district == primaryId) continue;
            int deficit = unchecked(CityCount(identity.Id, district) * perCity -
                ReadDistrictAdministrationIntelligenceCurrent(current, district));
            //Strict comparison preserves actual District registry order on ties.
            if (deficit <= largestDeficit) continue;
            largestDeficit = deficit; destination = district;
        }
        if (destination is not int target) return new(true, null);
        return new(true, new TransferDistrictMilitaryIntelligenceOrder(identity.Id,
            primaryId, target, Math.Min(excess, largestDeficit)));

        int CityCount(int forceId, int? district)
        {
            int count = 0;
            var scope = new OriginalAiActor(forceId, district,
                district.HasValue ? OriginalAiAgentKind.District : OriginalAiAgentKind.Force);
            for (int slot = 0; slot < 52; slot++)
                if (ReadOriginalAiActorCityLoopCurrent(current, scope, slot) is not null) count++;
            return count;
        }
    }

    /// <summary>Shared ordinary transfer writer. The command provider repeats
    /// current ownership, primary selection and stock checks before publishing
    /// only the two saved District24 windows. It spends no command books.</summary>
    CommandResult IDistrictMilitaryIntelligenceTransferRules.Transfer(GameState state, ForceState force,
        TransferDistrictMilitaryIntelligenceOrder order, CommandContext context)
    {
        try
        {
            var current = CaptureCurrentCampaignEventQueries(state);
            var own = current.ReadForceTableReference(force.Id) ?? throw SpyAllocationMissing("transfer_Force_pointer");
            if (!SpyAllocationNeed(current.ReadForceNativeValidity(own), "transfer_Force_validity"))
                return CommandResult.Reject("The transfer force is no longer valid.", "military-intelligence.force");
            var source = current.ReadDistrictTableReference(order.SourceDistrictId) ?? throw SpyAllocationMissing("transfer_source_pointer");
            var target = current.ReadDistrictTableReference(order.TargetDistrictId) ?? throw SpyAllocationMissing("transfer_target_pointer");
            if (!SpyAllocationNeed(current.ReadDistrictNativeValidity(order.SourceDistrictId), "transfer_source_validity") ||
                !SpyAllocationNeed(current.ReadDistrictNativeValidity(order.TargetDistrictId), "transfer_target_validity") ||
                SpyAllocationNeed(current.ReadDistrictForceSlot(source), "transfer_source_Force") != force.Id ||
                SpyAllocationNeed(current.ReadDistrictForceSlot(target), "transfer_target_Force") != force.Id)
                return CommandResult.Reject("The transfer districts are no longer controlled by this force.", "military-intelligence.control");
            var primary = ForcePrimaryDistrictSource.Resolve(current, own);
            if (SpyAllocationNeed(primary.PhysicalSlot, "transfer_primary_identity") != order.SourceDistrictId)
                return CommandResult.Reject("The source is no longer the primary district.", "military-intelligence.primary");
            int sourceStock = ReadDistrictAdministrationIntelligenceCurrent(current, order.SourceDistrictId);
            int targetStock = ReadDistrictAdministrationIntelligenceCurrent(current, order.TargetDistrictId);
            if (order.Amount > sourceStock)
                return CommandResult.Reject("There is insufficient military intelligence to transfer.", "military-intelligence.stock");
            //Use the normal231310 writer for each district's own current cap,
            //raw field update and saved-window publication, in source order.
            var result = WriteIntelligenceBudgetCurrent(state, current, order.SourceDistrictId,
                unchecked(sourceStock - order.Amount));
            result = WriteIntelligenceBudgetCurrent(result, current, order.TargetDistrictId,
                unchecked(targetStock + order.Amount));
            return CommandResult.Accept(result);
        }
        catch (InvalidDataException missing)
        { return CommandResult.RuleUnavailable(missing.Message, missing.Message); }
    }

    private static T SpyAllocationNeed<T>(T? value, string field) where T : struct => value ?? throw SpyAllocationMissing(field);
    private static InvalidDataException SpyAllocationMissing(string field) => new("original-ai.spy-allocation." + field);
}
