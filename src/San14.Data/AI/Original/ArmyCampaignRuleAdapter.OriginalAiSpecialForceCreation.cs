using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private abstract record SpecialForceCreationTarget;
    private sealed record SpecialForceBaseTarget(BaseReference Base) : SpecialForceCreationTarget;
    private sealed record SpecialForceArmyTarget(ArmyUnitReference Army) : SpecialForceCreationTarget;
    private sealed record SpecialForceCreationObjective(byte Action, ArmyTargetSourceFields Target,
        int Hex, DeploymentAction Command);

    private GameState CreateSpecialForceDefenseArmyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, BaseReference home, ArmyUnitReference target, int troops, int tactic)
        => CreateSpecialForceRaidArmyCurrent(state, current, pool, person, home,
            new SpecialForceArmyTarget(target), troops, 14, 11, tactic);

    private static SpecialForceCreationObjective ResolveSpecialForceCreationTarget(GameState state,
        SourceWorldContext current, SpecialForceCreationTarget target)
    {
        if (target is SpecialForceBaseTarget basis)
        {
            int location = RaidCreationKnown(current.ReadBaseObjectLink(basis.Base), "target_Base4E");
            return new(location <= 51 ? (byte)4 : (byte)5,
                new(location <= 51 ? (byte)5 : (byte)6, checked((ushort)(location <= 51 ? location : location - 51))),
                current.ReadBaseCurrentHexIndex(basis.Base), new("attack", new(DeploymentTargetKind.Location, location)));
        }
        if (target is SpecialForceArmyTarget unit)
        {
            int physical = RaidCreationKnown(unit.Army.PhysicalSlot, "defense_target_receiver");
            int logical = ArmyDestructionQueue.ResolveUnitId(state, physical) ?? throw RaidCreationMissing("defense_target_image");
            return new(7, new(27, checked((ushort)physical)),
                RaidCreationKnown(current.ReadArmyUnitRawHex2A(unit.Army), "defense_target_Hex"),
                new("attack", new(DeploymentTargetKind.Unit, logical)));
        }
        throw RaidCreationMissing("target_descriptor");
    }
}
