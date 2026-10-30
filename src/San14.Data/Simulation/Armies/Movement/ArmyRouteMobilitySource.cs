using San14.Core;

namespace San14.Data;

/// <summary>TC2B0660(Army,explicitFormation,0,0,CHex*) for ordinary status1/status2.
/// Initial route heuristic supplies the source-created scratch surface2;
/// subsequent steps supply their actual candidate Hex10. Current Army Hex
/// still supplies the independent Force/policy water gates. The caller owns
/// this writable context: unknown after a policy query requires discarding
/// its operation draft, including any reached primary-pool changes.</summary>
internal static class ArmyRouteMobilitySource
{
    public static TransportArmyRouteMobilityResult Read(GameState state,
        SourceWorldContext current, UnitState army, int formationPhysical,
        byte explicitHexSurface10)
    {
        try
        {
            int slot = K(army.NativeArmyOrigin?.NativeSlotIndex, "Army_physical_slot");
            var receiver = new ArmyUnitReference(slot);
            if (!K(current.ReadArmyUnitNativeValidity(receiver), "Army_validity")) return new(0, null, null);
            byte status = K(current.ReadArmyReturnUnitStatus10(receiver), "Army10");
            if (status is not (1 or 2)) throw Missing("branch.nonordinary_status");
            if (formationPhysical is < 1 or > 20) throw Missing("branch.nonordinary_Formation");
            int commanderId = K(current.ReadArmyUnitCommanderId(receiver), "Army12");
            int commander = (current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                ?? throw Missing("Person_table")).PhysicalSlot ?? throw Missing("Person_pointer");
            if (!K(current.ReadPersonNativeValidity(commander), "Person_validity")) throw Missing("branch.invalid_commander");
            byte rawForce = K(current.ReadPersonOldForceSlot(commander), "22CD80_Force");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("Force_table");
            int forcePhysical = force.PhysicalSlot ?? throw Missing("Force_pointer");
            ushort currentHex = K(current.ReadArmyUnitRawHex2A(receiver), "Army2A");
            return ArmyRouteMobilityEvaluation.Read(state, current,
                new(commander, forcePhysical, status, currentHex, 0, 0, 0, false,
                    () => ArmySavedCommandKnowledge.ReadControl(state, army), slot), formationPhysical, explicitHexSurface10,
                () => ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army));
        }
        catch (MissingOwner missing) { return new(null, null, missing.Message); }
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new("current.army_route_mobility." + field);
    private sealed class MissingOwner(string message) : Exception(message);
}
