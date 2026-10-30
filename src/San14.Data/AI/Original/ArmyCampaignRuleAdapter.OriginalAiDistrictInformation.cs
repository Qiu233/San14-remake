using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal BaseInformationValue ReadOriginalAiDistrictInformationCurrent(GameState state,
        SourceWorldContext current, int district, int mode)
    {
        BaseInformationValue Number(int value) => new BaseInformationInteger(value);
        BaseInformationValue People(IEnumerable<int> values) => new BaseInformationEntities(WorldEntityKind.Officer,
            values.Select(person => BaseInfoPersonEntity(current, BaseInfoNeed(current.ReadPersonIdentity(person), "District_Person10"))).ToArray());
        switch (mode)
        {
            case 0: return Number(district);
            case 1: return Number(BaseInfoNeed(current.ReadDistrictNumber(district), "District_number11"));
            case 2:
            {
                int raw = BaseInfoNeed(current.ReadDistrictForceId(district), "District_Force10");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw BaseInfoMissing("District_Force_pointer");
                return new BaseInformationEntity(new(WorldEntityKind.Force, BaseInfoNeed(force.PhysicalSlot, "District_Force_identity")));
            }
            case 3: return new BaseInformationFailure(); //No source selector3.
            case 4: return new BaseInformationEntity(BaseInfoPersonEntity(current,
                BaseInfoNeed(current.ReadDistrictLeader(district), "District_leader12")));
            case 5: return Number(BaseInfoNeed(current.ReadDistrictRawByte(district, 0x14), "District_books14"));
            case 7: return new BaseInformationEntities(WorldEntityKind.Army, BaseInfoArmies(state, current, null, district).ToArray());
            case 13:
            {
                var result = new List<OriginalAiEntity>();
                //Unlike CDistrictFootholdIterator, this branch compares the
                //registry's virtual78 directly, including District0.
                foreach (var entry in current.ReadRegisteredBases() ?? throw BaseInfoMissing("District_adjacency_Base_registry"))
                {
                    var at = entry ?? throw BaseInfoMissing("District_adjacency_Base_pointer");
                    if (BaseInfoNeed(current.ReadBaseDistrictSlot(at), "District_adjacency_Base78") != district) continue;
                    foreach (var neighbor in AiForceInformationNeighbors(current, at))
                    {
                        int other = BaseInfoNeed(current.ReadBaseDistrictReference(neighbor)?.PhysicalSlot, "neighbor_District_pointer");
                        var item = new OriginalAiEntity(WorldEntityKind.District, other);
                        if (other != district && !result.Contains(item)) result.Add(item);
                    }
                }
                return new BaseInformationEntities(WorldEntityKind.District, result);
            }
            case 14:
                return new BaseInformationEntities(WorldEntityKind.City, BaseInfoDistrictBases(current, district)
                    .Select(at => AiForceInformationBaseEntity(current, at)).Distinct().ToArray());
            case 15: return People(BaseInfoDistrictPeople(current, district));
            case 16:
            {
                var result = new List<OriginalAiEntity>();
                foreach (var at in BaseInfoDistrictBases(current, district))
                    foreach (var neighbor in AiForceInformationNeighbors(current, at))
                    {
                        if (BaseInfoNeed(current.ReadBaseDistrictSlot(neighbor), "frontier_neighbor_District") == district) continue;
                        var item = AiForceInformationBaseEntity(current, neighbor);
                        if (!result.Contains(item)) result.Add(item);
                    }
                return new BaseInformationEntities(WorldEntityKind.City, result);
            }
            case 17:
                return People(BaseInfoCreditPeople(state, current, district).Where(person => CreditEligibleCurrent(state, current, person, rankup: true)));
            case 18:
            {
                //6D8E30 retains source registration order.087330 only inspects
                //the first selected receiver, not every element in that list.
                int? first = BaseInfoCreditPeople(state, current, district)
                    .Where(person => CreditEligibleCurrent(state, current, person, rankup: false)).Select(person => (int?)person).FirstOrDefault();
                if (first is not int person) return People([]);
                int id = BaseInfoNeed(current.ReadPersonIdentity(person), "near_merit_Person10");
                var merit = state.Officers.GetValueOrDefault(id)?.MeritState ?? throw BaseInfoMissing("near_merit_owner");
                int stage = BaseInfoNeed(merit.RankStageId, "near_merit_stage1BC");
                int next = stage + 1 < 7 ? stage + 1 : 0;
                int threshold = CreditRanks()[next].SignedThreshold;
                int amount = BaseInfoNeed(merit.Merit, "near_merit1BE");
                return People(amount < threshold && threshold <= unchecked(amount + BaseInfoParameter(state, "credit-near-threshold")) ? [person] : []);
            }
            case 19: return ReadOriginalAiDistrictDiplomaticDestinationsCurrent(state, current, district);
            case 20:
            {
                //2313C0 classifies the actual District24 budget against the
                //Force-rank capacity. It is not a City treasury ratio.
                int capacity = DistrictAdministrationIntelligenceCapacity(state, current, district);
                if (capacity == 0) throw BaseInfoMissing("District_capacity_divisor");
                int stock = ReadDistrictAdministrationIntelligenceCurrent(current, district);
                int ratio = unchecked(stock * BaseInfoParameter(state, "district-budget-ratio-scale")) / capacity;
                return Number(ratio >= BaseInfoParameter(state, "district-budget-high") ? 4 :
                    ratio >= BaseInfoParameter(state, "district-budget-middle") ? 3 :
                    ratio >= BaseInfoParameter(state, "district-budget-low") ? 2 : 1);
            }
            default: throw BaseInfoMissing("District_selector_" + mode);
        }
    }

    private static IEnumerable<BaseReference> BaseInfoDistrictBases(SourceWorldContext current, int district)
    {
        if (district is < 1 or > 51) yield break;
        foreach (var entry in current.ReadRegisteredBases() ?? throw BaseInfoMissing("District_Base_registry80"))
        {
            var at = entry ?? throw BaseInfoMissing("District_Base_payload");
            if (BaseInfoNeed(current.ReadBaseDistrictSlot(at), "District_Base_virtual78") == district) yield return at;
        }
    }

    private static IEnumerable<int> BaseInfoDistrictPeople(SourceWorldContext current, int district)
    {
        if (district is < 1 or > 51) yield break;
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw BaseInfoMissing("District_Person_registry100"))
        {
            int person = BaseInfoNeed(entry, "District_Person_pointer");
            //CDistrictPersonIterator(mode4) accepts every registered status.
            if (BaseInfoNeed(current.ReadPersonDistrictSlot(person), "District_PersonE0") == district) yield return person;
        }
    }

    private IEnumerable<int> BaseInfoCreditPeople(GameState state, SourceWorldContext current, int district)
    {
        int raw = BaseInfoNeed(current.ReadDistrictForceId(district), "credit_District10");
        var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw BaseInfoMissing("credit_Force_pointer");
        if (!BaseInfoNeed(current.ReadForceNativeValidity(force), "credit_Force_validity")) yield break;
        int owner = BaseInfoNeed(force.PhysicalSlot, "credit_Force_identity");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        bool wholeForce = ReadOriginalAiForceIsCurrentPlayer(state, current, owner) && primary.PhysicalSlot == district;
        foreach (int person in NormalTurnServingPeople(current, owner, district, wholeForce)) yield return person;
    }
}
