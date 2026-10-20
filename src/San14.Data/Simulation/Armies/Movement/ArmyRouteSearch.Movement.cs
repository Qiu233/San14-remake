using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    /// <summary>Movement facts consumed by the shared terrain/occupancy rules.
    /// A draft has no registered receiver and never acquires one here.</summary>
    internal abstract class Movement
    {
        internal abstract bool AvoidHostile { get; }
        internal abstract byte LandFormation { get; }
        internal abstract byte NavalFormation { get; }
        internal abstract int Force { get; }
        internal abstract int HomeHex { get; }
        internal abstract bool WallPermission { get; }
        internal abstract byte? Action { get; }
        internal abstract bool HasNoDestination { get; }
        internal abstract float ReadMobility(int formation, byte explicitSurface);
    }

    private sealed class LiveMovement : Movement
    {
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly UnitState army;
        internal override bool AvoidHostile { get; }
        internal override byte LandFormation { get; }
        internal override byte NavalFormation { get; }
        internal override int Force { get; }
        internal override int HomeHex { get; }
        internal override bool WallPermission { get; }
        internal override byte? Action => ArmyConfigurationSourceAdapter.ReadAction(state, army);
        internal override bool HasNoDestination =>
            (AvoidHostile ? TransportRouteSourceAdapter.ReadWaypoint(state, army, 0) :
                ArmyMarchSourceAdapter.EncodeDestination(army.March?.Target)) == Count;

        internal LiveMovement(GameState state, SourceWorldContext current, UnitState army)
        {
            this.state = state; this.current = current; this.army = army;
            AvoidHostile = army.NativeTacticRuntime?.Status10 switch { 1 => false, 2 => true, _ => throw Missing("ordinary_status1_or2") };
            LandFormation = K(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "Army1B");
            NavalFormation = K(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "Army1C");
            var person = K(current.ReadPersonLookupReference(army.OfficerId), "Person_pointer");
            int physical = person.PhysicalSlot ?? throw Missing("Person_receiver");
            Force = K(current.ReadPersonOldForceSlot(physical), "Army_Force22CD80");
            if (Force is < 1 or > 45) throw Missing("ordinary_Force_scope");
            ushort location = K(current.ReadPersonFirstLocation(physical), "PersonE2");
            var home = location <= 51 ? K(current.ReadCityTableReference(location), "Home_City_pointer") :
                location <= 61 ? K(current.ReadGateTableReference(location - 51), "Home_Gate_pointer") :
                K(current.ReadCityTableReference(0), "Home_placeholder_pointer");
            ushort link = K(current.ReadBaseObjectLink(home), "Base4E");
            var obj = K(current.ReadObjectTableReference(link <= 3000 ? link : 0), "Base_Object_pointer");
            HomeHex = K(current.ReadObjectNativeValidity(obj), "Base_Object_validity") ? K(current.ReadObjectHexIndex(obj), "Object10") : Count;
            WallPermission = !K(TacticRemakeCurrentTraits.CommanderImmune(current, physical, 102), "Commander_trait102");
        }

        internal override float ReadMobility(int formation, byte explicitSurface)
        {
            var value = ArmyRouteMobilitySource.Read(state, current, army, formation <= 20 ? formation : 0, explicitSurface);
            return value.Value ?? throw Missing(value.UnresolvedOwner ?? "2B0660");
        }
    }
}
