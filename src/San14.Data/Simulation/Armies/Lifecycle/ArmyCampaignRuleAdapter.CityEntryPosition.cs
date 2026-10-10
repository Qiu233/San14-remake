using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal ArmyCityEntryEligibilityResult ReadTransportCityEntryPosition(GameState state, UnitState army, CityState city)
    {
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } arena ||
            physical is < 1 or > 500 || physical >= arena.Slots.Count ||
            arena.ReceiverSlots()[physical] is not { ActiveUnitId: var active, Commander12: var commander } slot ||
            active != army.Id || commander != army.OfficerId ||
            OwnedArmyLifecycleTargets.Read(state, slot.ReceiverId) is not { Kind38: 5, Identity3A: ushort target } || target != city.Id ||
            ArmyMarchSourceAdapter.ReadCreatedCurrentHex(state, army) != army.HexId)
            return new(null, "current.transport_entry_position_owner");
        var geometry = _definitions.ReadCityEntryGeometry(target);
        if (geometry.Goal != city.HexId) return new(null, "current.transport_entry_Object10");
        int? rawForce = null;
        bool? commanderValid = null;
        if (state.Officers.TryGetValue(army.OfficerId, out var person) && _definitions.ResolveOfficerSource(person.Id) is int personPhysical)
        {
            commanderValid = NativeValidity.PersonWithBehavior(personPhysical, checked((ushort)person.Id),
                person.Behavior, OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus));
            if (commanderValid == false) rawForce = 0;
            else if (commanderValid == true && OfficerLocationSourceAdapter.EncodeMembership(person.LocationState?.Membership,
                (state.ResourceState as CampaignSourceKnowledge)?.Find(person.Id)) is byte districtId)
            {
                if (districtId is 0 or > 51) rawForce = 0;
                else if (state.Districts.TryGetValue(districtId, out var district))
                {
                    bool? valid = NativeValidity.District(districtId, checked((byte)district.ForceId),
                        checked((byte)district.Number), district.LeaderId is int leader ? checked((ushort)leader) : null);
                    rawForce = valid == true ? district.ForceId : valid == false ? 0 : null;
                }
            }
        }
        return ArmyCityEntryEligibilitySource.ReadPosition(new(physical, slot.Commander12, slot.Status10,
            ArmyConfigurationSourceAdapter.ReadAction(state, army), ArmyMarchSourceAdapter.ReadCreatedCurrentHex(state, army),
            OwnedArmyLifecycleTargets.Read(state, slot.ReceiverId), target, geometry.Valid, geometry.Location, geometry.Goal, rawForce, city.ForceId ?? 0,
            commanderValid));
    }
}
