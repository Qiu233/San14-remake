using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus CityDuty(uint footholdKey, uint? officerKey, CityDutyKind kind)
    {
        if (!TryEntity(footholdKey, out var foothold) || foothold.Kind != WorldEntityKind.City)
            return OriginalAiNodeStatus.Failure;
        int? officerId = null;
        if (officerKey is uint key)
        {
            if (!TryEntity(key, out var person) || person.Kind != WorldEntityKind.Officer)
                return OriginalAiNodeStatus.Failure;
            officerId = person.Id;
        }
        var preparation = adapter.CreateOriginalAiCityDutyCurrent(Current, InputsCurrent,
            foothold.Id, officerId, kind);
        if (!preparation.Success) return OriginalAiNodeStatus.Failure;
        if (preparation.Order is not { } order) return OriginalAiNodeStatus.Success;
        if (order.ForceId != Actor.ForceId) return OriginalAiNodeStatus.Failure;
        var result = planning.Apply(order, rules.OrderRules, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing)
            throw new InvalidDataException($"original_ai.command.{missing}: {result.Error}");
        return result.Accepted ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus CityDutyInformation(uint input, int field, uint output, bool person)
    {
        if (!TryEntity(input, out var entity)) return OriginalAiNodeStatus.Failure;
        var current = InputsCurrent;
        if (person)
        {
            if (entity.Kind != WorldEntityKind.Officer) return OriginalAiNodeStatus.Failure;
            int physical = CityDutyPerson(current, entity.Id);
            if (!CityDutyNeed(current.ReadPersonNativeValidity(physical), "Person_validity"))
                return OriginalAiNodeStatus.Failure;
            int identity = CityDutyNeed(current.ReadPersonIdentity(physical), "Person10");
            return SetInteger(output, adapter.ReadOriginalAiCityDutyAbility(Current, current,
                identity, field == 7 ? 0 : 1));
        }
        if (entity.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate)) return OriginalAiNodeStatus.Failure;
        var foothold = BaseLookup.Find(entity.Id, current);
        if (!CityDutyNeed(current.ReadBaseNativeValidity(foothold), "Foothold_validity"))
            return OriginalAiNodeStatus.Failure;
        if (field is 20 or 21)
        {
            var related = CurrentGateMapQueries.FindCity(foothold, current);
            int city = CityDutyNeed(related.PhysicalSlot, "related_City");
            int raw = CityDutyNeed(field == 20 ? current.ReadAssignmentCityCA(city) :
                current.ReadAssignmentCityCC(city), "City_duty_identity");
            int physical = CityDutyPerson(current, raw);
            // The source writes the looked-up object even when it is the
            // invalid Person0 sentinel. Its consumer owns the validity gate.
            int identity = CityDutyNeed(current.ReadPersonIdentity(physical), "duty_Person10");
            return SetEntity(output, new(WorldEntityKind.Officer, identity));
        }
        int value = field switch
        {
            22 => CityDutyNeed(current.ReadBaseMorale44(foothold), "Foothold44"),
            34 => CityDutyNeed(current.ReadBaseResource(foothold, BaseResourceKind.Gold), "Foothold_virtual90"),
            36 => CityDutyNeed(current.ReadBaseResource(foothold, BaseResourceKind.Troops), "Foothold3C"),
            _ => throw CityDutyMissing("FootholdInfo_" + field)
        };
        return SetInteger(output, value);
    }

    internal bool CityDutyCondition108(IReadOnlyList<uint> parameters)
    {
        if (!TryEntity(parameters[0], out var district) || district.Kind != WorldEntityKind.District) return false;
        var current = InputsCurrent;
        int physical = CityDutyNeed(current.ReadDistrictTableReference(district.Id is >= 0 and <= 51 ? district.Id : 0)?.PhysicalSlot,
            "condition108_District_pointer");
        if (!CityDutyNeed(current.ReadDistrictNativeValidity(physical), "condition108_District_validity")) return false;
        uint mode = parameters[1];
        if (mode <= 4 || mode == 17)
        {
            byte value = CityDutyNeed(current.ReadDistrictRawByte(physical, 0x1B), "condition108_permissions1B");
            return mode == 17 ? value > 127 : ((value >> ((int)mode + 2)) & 1) != 0;
        }
        if (mode is >= 5 and <= 16)
        {
            uint value = 0;
            for (int i = 0; i < 4; i++) value |= (uint)CityDutyNeed(
                current.ReadDistrictRawByte(physical, 0x1C + i), "condition108_construction1C") << (i * 8);
            int shift = mode switch { 13 => 14, 14 => 11, 15 => 12, 16 => 13, _ => (int)mode - 2 };
            // Preserve the uint result of 0A9D80, including its upper bits.
            return ((value >> shift) & 0xFFFFFF01u) != 0;
        }
        return false;
    }

    private static int CityDutyPerson(SourceWorldContext current, int identity) => CityDutyNeed(
        (identity is >= 0 and <= 6000 ? current.ReadPersonLookupReference(identity) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
    private static T CityDutyNeed<T>(T? value, string field) where T : struct => value ?? throw CityDutyMissing(field);
    private static InvalidDataException CityDutyMissing(string field) => new("original-ai.city-duty." + field);
}
