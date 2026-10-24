using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    //2ACF90's descriptor contains a null Army and the actual source Base Force.
    //Flags4 use terrain costs and logistics passability, with no Army mobility.
    internal static int[] DistrictBaseRoute(GameState state, SourceWorldContext current,
        BaseReference source, BaseReference target)
    {
        int force = current.ReadBaseForceSlot(source)
            ?? throw new InvalidDataException("person-return.district-route.source_Force");
        var reference = current.ReadForceTableReference(force <= 51 ? force : 0)
            ?? throw new InvalidDataException("person-return.district-route.Force_pointer");
        bool validForce = current.ReadForceNativeValidity(reference)
            ?? throw new InvalidDataException("person-return.district-route.Force_validity");
        int actualForce = validForce ? current.ReadForcePhysicalSlot(reference)
                ?? throw new InvalidDataException("person-return.district-route.Force_identity") : 0;
        int start = current.ReadBaseCurrentHexIndex(source), goal = current.ReadBaseCurrentHexIndex(target);
        if ((uint)start >= Count || (uint)goal >= Count) return [];
        var query = new Queries(state, current, new DistrictBaseMovement(actualForce),
            start, goal, 0, 0, null, 4);
        try { return Search(query, out _, out _, hasArmyDescriptor: false); }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }

    private sealed class DistrictBaseMovement(int force) : Movement
    {
        internal override bool AvoidHostile => false;
        internal override byte LandFormation => 0;
        internal override byte NavalFormation => 0;
        internal override int Force => force;
        internal override int HomeHex => Count;
        internal override bool WallPermission => true;
        internal override byte? Action => null;
        internal override bool HasNoDestination => true;
        internal override float ReadMobility(int formation, byte explicitSurface) =>
            throw new InvalidDataException("A null Army route descriptor does not query mobility.");
    }
}
