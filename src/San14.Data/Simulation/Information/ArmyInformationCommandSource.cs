using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private string ReadUnitDetailTarget(GameState state, SourceWorldContext current, UnitState unit, bool temporary)
    {
        if (!UnitDetailCommandVisible(state, current, unit)) return UnitDetailData(1, 0x6D6);
        int raw;
        if (temporary)
        {
            var target = ArmyOverrideTargetSourceAdapter.Read(state, unit);
            byte kind = UnitDetailCommandNeed(target.Kind3C, "temporary_target.kind3C");
            raw = UnitDetailCommandNeed(target.Identity3E, "temporary_target.identity3E");
            return kind switch
            {
                5 => UnitDetailBaseName(state, current, BaseKind.City, raw < 52 ? raw : 0),
                6 => UnitDetailBaseName(state, current, BaseKind.Gate, raw < 11 ? raw : 0),
                11 => UnitDetailObjectDefinitionName(current, UnitDetailObject(current, raw)),
                27 => ReadHistoryArmyName(state, current, new(raw < 501 ? raw : 0)),
                _ => UnitDetailData(1, 0x353)
            };
        }
        int slot = UnitDetailCommandNeed(UnitDetailArmyReceiver(unit).PhysicalSlot, "target.Army_receiver");
        var fields = current.ReadArmyLifecycleTargetFields(slot) ?? throw UnitDetailCommandMissing("target.current_owner");
        raw = UnitDetailCommandNeed(fields.Identity3A, "target.identity3A");
        if (raw == 255) return UnitDetailData(1, 0x353);
        byte action = UnitDetailCommandNeed(ArmyConfigurationSourceAdapter.ReadAction(state, unit), "target.action37");
        return action switch
        {
            2 or 4 or 11 => UnitDetailBaseName(state, current, BaseKind.City, raw < 52 ? raw : 0),
            3 or 5 or 12 => UnitDetailBaseName(state, current, BaseKind.Gate, raw < 11 ? raw : 0),
            6 or 9 or 10 => UnitDetailHexAreaName(state, current,
                UnitDetailCommandNeed(current.ReadObjectHexIndex(UnitDetailObject(current, raw)), "target.Object_hex10")),
            7 or 17 => ReadHistoryArmyName(state, current, new(raw < 501 ? raw : 0)),
            1 or 8 or 14 or 15 or 16 => UnitDetailHexAreaName(state, current, raw),
            _ => ""
        };
    }

    private string ReadUnitDetailConstruction(GameState state, SourceWorldContext current, UnitState unit)
    {
        if (!UnitDetailCommandVisible(state, current, unit)) return UnitDetailData(1, 0x6D6);
        byte action = UnitDetailCommandNeed(ArmyConfigurationSourceAdapter.ReadAction(state, unit), "construction.action37");
        if (action == 8)
        {
            var definition = FacilityConstructionDefinitions.Selected(state, current, unit);
            return UnitDetailDefinitionName(UnitDetailCommandNeed(definition.PhysicalSlot, "construction.definition46"));
        }
        if (action is not (6 or 9 or 10)) return UnitDetailData(1, 0x353);
        int slot = UnitDetailCommandNeed(UnitDetailArmyReceiver(unit).PhysicalSlot, "construction.Army_receiver");
        var target = current.ReadArmyLifecycleTargetFields(slot) ?? throw UnitDetailCommandMissing("construction.target_owner");
        int raw = UnitDetailCommandNeed(target.Identity3A, "construction.target3A");
        return ReadFacilityArrivalObjectName(state, current, UnitDetailObject(current, raw));
    }

    private string ReadUnitDetailCaptiveCount(GameState state, SourceWorldContext current, UnitState unit)
        => EnumerateUnitDetailCaptivePhysicalSlots(state, current, unit).Count().ToString(CultureInfo.InvariantCulture);

    //230C70 and 34EE40 share the actual Person registry order and membership.
    //Do not substitute the Officer dictionary or apply a global list filter.
    private IEnumerable<int> EnumerateUnitDetailCaptivePhysicalSlots(GameState state,
        SourceWorldContext current, UnitState unit)
    {
        int slot = UnitDetailCommandNeed(UnitDetailArmyReceiver(unit).PhysicalSlot, "captives.Army_receiver");
        int location = slot is >= 1 and <= 500 ? slot + 61 : 0;
        var registry = state.ReadArmyLifecycle()?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Officer)
            ?? throw UnitDetailCommandMissing("captives.registered_people");
        foreach (var registered in registry)
        {
            if (registered.Kind != WorldEntityKind.Officer || registered.Id is not int id)
                throw UnitDetailCommandMissing("captives.Person_identity");
            var person = (id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw UnitDetailCommandMissing("captives.Person_lookup");
            int physical = UnitDetailCommandNeed(person.PhysicalSlot, "captives.Person_receiver");
            if (UnitDetailCommandNeed(current.ReadPersonStatus(physical), "captives.Person_statusE6") != 6) continue;
            if (UnitDetailCommandNeed(current.ReadPersonSecondLocation(physical), "captives.Person_locationE4") == location)
                yield return physical;
        }
    }

    private string ReadUnitDetailPreference(GameState state, UnitState unit, int node)
    {
        if (node is 147 or 152)
        {
            byte flags = unit.NativeTacticRuntime?.Flags26 ?? throw UnitDetailCommandMissing("preference.flags26");
            return UnitDetailData(1, (flags & (node == 147 ? 4 : 8)) != 0 ? 0x338 : 0x339);
        }
        if (node == 142)
        {
            var behavior = unit.Behavior ?? throw UnitDetailCommandMissing("preference.enemy_approach40");
            return UnitDetailData(0, 0x115 + (behavior.AttackNearbyEnemies ? 2 : 1));
        }
        byte status = unit.NativeTacticRuntime?.Status10 ?? throw UnitDetailCommandMissing("preference.status10");
        byte choice = status == 2
            ? UnitDetailCommandNeed(TransportRouteSourceAdapter.ReadChoice(state, unit), "preference.transport_choice41")
            : UnitDetailCommandNeed(unit.NativeCommandChoice41, "preference.choice41");
        return choice is 1 or 2 ? UnitDetailData(0, 0x118 + choice) : "";
    }

    private ObjectReference UnitDetailObject(SourceWorldContext current, int raw) =>
        current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw UnitDetailCommandMissing("target.Object_lookup");

    private string UnitDetailObjectDefinitionName(SourceWorldContext current, ObjectReference obj)
    {
        var fields = current.ReadPhysicalObject(UnitDetailCommandNeed(obj.PhysicalSlot, "target.Object_receiver"))
            ?? throw UnitDetailCommandMissing("target.Object_fields");
        return UnitDetailDefinitionName(fields.RawField12 < 31 ? fields.RawField12 : 0);
    }

    private string UnitDetailDefinitionName(int raw) =>
        _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions[raw < 31 ? raw : 0].Name;

    private string UnitDetailHexAreaName(GameState state, SourceWorldContext current, int raw)
    {
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw UnitDetailCommandMissing("target.Hex_lookup");
        int areaRaw = UnitDetailCommandNeed(current.ReadHexAreaSlot(hex), "target.Hex_Area12");
        var area = current.ReadAreaTableReference(areaRaw < 501 ? areaRaw : 0)
            ?? throw UnitDetailCommandMissing("target.Area_lookup");
        int physical = UnitDetailCommandNeed(area.PhysicalSlot, "target.Area_receiver");
        if (physical != 0) return state.Areas.GetValueOrDefault(physical)?.Name ?? throw UnitDetailCommandMissing("target.Area_name10");
        var rows = _scenario.Snapshot.Areas;
        if (rows.Count == 0 || rows[0].Slot != 0) throw UnitDetailCommandMissing("target.Area0_definition");
        return rows[0].Name;
    }

    private static T UnitDetailCommandNeed<T>(T? value, string field) where T : struct =>
        value ?? throw UnitDetailCommandMissing(field);
    private static InvalidDataException UnitDetailCommandMissing(string field) => new("unit.details." + field);
}
