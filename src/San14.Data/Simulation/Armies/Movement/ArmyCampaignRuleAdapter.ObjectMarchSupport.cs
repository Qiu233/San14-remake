using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2C6A10(next,1,Army,0), including its naval prefix and raw Force
    //getter. The existing completion caller already owns this full gate.
    internal bool ReadObjectMarchPassageCurrent(GameState state, SourceWorldContext current,
        UnitState army, HexReference hex)
    {
        int physical = ObjectMarchNeed(army.NativeArmyOrigin?.NativeSlotIndex, "passage_Army_receiver");
        int force = current.ReadArmyUnitRawForceSlot(new(physical));
        return FacilityCompletionTerritoryEligible(state, current, army, hex, force);
    }

    //2DA620: this runs before credit/direction/position/waypoint stores.
    //It is not the later2D99C0 effect102 tail. The display observes the
    //existing saved fire-duration root, as with the other Hex16 writers.
    internal GameState ApplyObjectMarchFireTransferCurrent(GameState state, SourceWorldContext current,
        UnitState army, HexReference from, HexReference to, byte fromTerrainFlags, byte toTerrainFlags,
        NativeArmyProgressStageMoment? moment)
    {
        int source = ObjectMarchNeed(from.PhysicalSlot, "fire_transfer_source_Hex");
        ushort duration = ObjectMarchNeed(current.ReadCaptureHexWord16(source), "fire_transfer_source16");
        if (duration == 0 || (fromTerrainFlags & 2) == 0) return state;
        int destination = ObjectMarchNeed(to.PhysicalSlot, "fire_transfer_destination_Hex");
        if (source == destination) return state;
        if ((toTerrainFlags & 2) != 0)
            current.SetCaptureHexWord16(destination, (ushort)Math.Min((int)duration, 720));
        current.SetCaptureHexWord16(source, 0);
        return BaseCaptureCurrentInputs.PublishCurrent(state, current);
    }

    //2D99C0: only after Base arrival and29DB90. Keep the retained Army
    //receiver valid even when the active Unit has entered pending70.
    internal GameState ApplyObjectMarchExtinguishCurrent(GameState state, SourceWorldContext current,
        UnitState army, NativeArmyProgressStageMoment? moment)
    {
        int physical = ObjectMarchNeed(army.NativeArmyOrigin?.NativeSlotIndex, "extinguish_Army_receiver");
        var receiver = new ArmyUnitReference(physical);
        if (!ObjectMarchNeed(current.ReadArmyUnitNativeValidity(receiver), "extinguish_Army_validity")) return state;
        ushort commander = ObjectMarchNeed(current.ReadArmyUnitCommanderId(receiver), "extinguish_Commander12");
        int person = ObjectMarchNeed(current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)?.PhysicalSlot,
            "extinguish_Commander_lookup");
        if (!ObjectMarchNeed(TacticRemakeCurrentTraits.CommanderImmune(current, person, 102), "extinguish_effect102")) return state;
        ushort rawHex = ObjectMarchNeed(current.ReadArmyUnitRawHex2A(receiver), "extinguish_current2A");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw ObjectMarchMissing("extinguish_Hex_pointer");
        int position = ObjectMarchNeed(hex.PhysicalSlot, "extinguish_Hex_receiver");
        if (ObjectMarchNeed(current.ReadCaptureHexWord16(position), "extinguish_Hex16") == 0) return state;
        byte terrainCode = ObjectMarchNeed(current.ReadHexSurfaceCode(hex), "extinguish_Hex_surface");
        int terrain = ObjectMarchNeed(current.ReadSurfaceTypeTableReference(terrainCode <= 30 ? terrainCode : 0)?.PhysicalSlot,
            "extinguish_Terrain_pointer");
        if ((ObjectMarchNeed(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrain), "extinguish_Terrain2C") & 2) != 0)
            return state;
        ushort link = ObjectMarchNeed(current.ReadHexObjectLink(hex), "extinguish_Hex1A");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw ObjectMarchMissing("extinguish_Object_pointer");
        bool validObject = ObjectMarchNeed(current.ReadObjectNativeValidity(obj), "extinguish_Object_validity");
        if (validObject && ObjectMarchNeed(obj.PhysicalSlot, "extinguish_Object_receiver") is >= 1 and <= 61)
            return state;
        current.SetCaptureHexWord16(position, 0);
        var cleared = BaseCaptureCurrentInputs.PublishCurrent(state, current);
        return QueueArmyMapSpeechCurrent(cleared, current, army.Id, commander, 0x22, position);
    }

    private static T ObjectMarchNeed<T>(T? value, string field) where T : struct
        => value ?? throw ObjectMarchMissing(field);
    private static InvalidDataException ObjectMarchMissing(string field)
        => new("Army.object-march." + field);
}
