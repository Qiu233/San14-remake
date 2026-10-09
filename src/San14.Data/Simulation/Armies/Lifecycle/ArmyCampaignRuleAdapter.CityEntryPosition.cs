using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal ArmyCityEntryEligibilityResult ReadTransportCityEntryPosition(GameState state, UnitState army, CityState city)
    {
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.NativeArmyLifecycle is not { } arena ||
            physical is < 1 or > 500 || physical >= arena.Slots.Length ||
            arena.Slots[physical] is not { ActiveUnitId: var active, Commander12: var commander } slot ||
            active != army.Id || commander != army.OfficerId ||
            arena.Targets38 is not { } targets || physical >= targets.Length ||
            targets[physical] is not { Kind38: 5, Identity3A: ushort target } || target != city.Id ||
            army.NativeCurrentHex2A != army.HexId)
            return new(null, "current.transport_entry_position_owner");
        var geometry = _definitions.ReadCityEntryGeometry(target);
        if (geometry.Goal != city.HexId) return new(null, "current.transport_entry_Object10");
        int? rawForce = null;
        bool? commanderValid = null;
        if (state.Officers.TryGetValue(army.OfficerId, out var person) && person.NativeAbilityRuntime is { } ability)
        {
            commanderValid = NativeValidity.Person(ability.PhysicalSlot, checked((ushort)person.Id),
                ability.Flags196, ability.Extra1FD, ability.Status11E);
            if (commanderValid == false) rawForce = 0;
            else if (commanderValid == true && person.NativeLocationRuntime?.District118 is byte districtId)
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
            army.NativeArmyConfiguration?.Action37, army.NativeMarch?.CurrentHex2A ?? army.NativeCurrentHex2A,
            targets[physical], target, geometry.Valid, geometry.Location, geometry.Goal, rawForce, city.ForceId ?? 0,
            commanderValid));
    }
}
