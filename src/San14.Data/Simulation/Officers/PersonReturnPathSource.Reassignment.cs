using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    /// <summary>1F6590:2ACD10 then2318F0(distance,0,Force). The remake
    /// computes the real mode8 Base-to-Base path instead of depending on a
    /// native process distance-cache pointer. No actor trait is consumed.</summary>
    public static PersonReturnPathResult QueryReassignment(GameState state, SourceWorldContext current,
        int sourceLocation, int targetLocation)
    {
        try
        {
            var source = BaseLookup.Find(sourceLocation, current); var target = BaseLookup.Find(targetLocation, current);
            if (current.ReadBaseNativeValidity(source) != true || current.ReadBaseNativeValidity(target) != true)
                throw new InvalidDataException("person-movement.current_Base");
            var start = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(source)) ?? throw new InvalidDataException("person-movement.start");
            var end = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(target)) ?? throw new InvalidDataException("person-movement.end");
            int raw = current.ReadBaseForceSlot(source) ?? throw new InvalidDataException("person-movement.Force");
            var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw new InvalidDataException("person-movement.Force_pointer");
            var output = current.CreateNearbyTemporary();
            try { return Fill(state, current, start, end, force, output); }
            finally { current.ClearNearbyTemporary(output, true); }
        }
        catch (InvalidDataException error) { return new(null, null, null, null, error.Message); }
    }
}
