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
            var actor = new ArmyUnitReference(K(army.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver"));
            Force = current.ReadArmyUnitRawForceSlot(actor);
            ushort commander = K(current.ReadArmyUnitCommanderId(actor), "Army12");
            var person = K(current.ReadPersonLookupReference(commander <= 6000 ? commander : 0), "Person_pointer");
            int physical = person.PhysicalSlot ?? throw Missing("Person_receiver");
            //183C00 leaves homeStart false for a special Force without the
            //232860 binding. Other actors use32C7D0's actual Base lookup.
            HomeHex = Count;
            if (Force is not (>= 46 and <= 51) || current.IsArmyUnitBoundToSpecialForce(actor))
            {
                ushort location = K(current.ReadPersonFirstLocation(physical), "PersonE2");
                HomeHex = current.ReadBaseCurrentHexIndex(BaseLookup.Find(location, current));
            }
            WallPermission = !K(TacticRemakeCurrentTraits.CommanderImmune(current, physical, 102), "Commander_trait102");
        }

        internal override float ReadMobility(int formation, byte explicitSurface)
        {
            var value = ArmyRouteMobilitySource.Read(state, current, army, formation <= 20 ? formation : 0, explicitSurface);
            return value.Value ?? throw Missing(value.UnresolvedOwner ?? "2B0660");
        }
    }
}
