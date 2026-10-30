using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2C6A10 receives an index operand, then both its naval prefix and2C6AD0
    //resolve that same operand. The caller selects flag0 or1 explicitly.
    internal bool ReadMarchCollisionPassageCurrent(GameState state, SourceWorldContext current,
        UnitState army, int hexOperand, bool requireNoArmy)
    {
        var hex = current.ReadHexTableReference(hexOperand < 48400 ? hexOperand : 0)
            ?? throw CollisionMissing("passage_Hex_pointer");
        if (hex.PhysicalSlot is null) return false;
        byte surface = current.ReadHexSurfaceCode(hex) ?? throw CollisionMissing("passage_Hex_surface");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
            ?? throw CollisionMissing("passage_Terrain_pointer");
        if (terrain.PhysicalSlot is null) return false;
        if (terrain.PhysicalSlot == 24 && (ArmyConfigurationSourceAdapter.ReadNaval(state, army)
                ?? throw CollisionMissing("passage_Army_naval")) is not (12 or 13 or 17 or 18)) return false;
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw CollisionMissing("passage_Army_receiver");
        //Eligible's input reference contributes only its physical index before
        //the rule's own lookup; this adapter retains the original raw operand.
        return GateCaptureTerritorySource.Eligible(state, current, new(hexOperand < 48400 ? hexOperand : 0),
            current.ReadArmyUnitRawForceSlot(new(physical)), requireNoArmy);
    }

    //Use the same lifecycle owner as command completion: a registered pending
    //receiver is not resurrected into Units by a late movement callback.
    internal GameState WriteMarchCollisionImageCurrent(GameState state, UnitState image)
        => WriteCompletionArmyImage(state, image);

    private static InvalidDataException CollisionMissing(string field)
        => new("Army.march-collision." + field);
}
