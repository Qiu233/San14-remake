using San14.Core;

namespace San14.Data;

public sealed record TransportArmyCreationRouteReceipt(int ArmyPhysical, int GoalHex,
    int[] PathHexes, int? ChosenPathIndex, ushort? WrapperNextHex48Write, int ExpandedNodes, string SourceBranch);
public sealed record TransportArmyCreationRouteResult(TransportArmyCreationRouteReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Only2009B0's reached post-creation1845B0(mode1,count1,scratch2)
/// and48 tail. Caller owns actual created Army/current/pool. No301DD0 clone,
/// hold/exclusion check,1F8 writer, command-book or warning is invented here.</summary>
public static class TransportArmyCreationRouteSource
{
    public static TransportArmyCreationRouteResult Prepare(GameState state,
        SourceWorldContext current, UnitState createdArmy)
    {
        try
        {
            int physical = createdArmy.NativeArmyOrigin?.NativeSlotIndex ?? throw new InvalidDataException("current.transport_creation_route_Army_physical");
            var temporary = current.CreateTemporaryHexList() ?? throw new InvalidDataException("current.transport_creation_route_descriptor");
            try
            {
                current.ClearTemporaryHexList(temporary); //Source669E60 precedes1845B0.
                var (goal, path, expanded) = TransportArmyRouteSource.ReadCreationPath(state, current, createdArmy);
                if (path.Length == 0) return Done(null, null, "2009B0.no_path");
                ushort start = createdArmy.NativeMarch?.CurrentHex2A ?? createdArmy.NativeCurrentHex2A ??
                    throw new InvalidDataException("current.transport_creation_route_Army2A");
                var hex = current.ReadHexTableReference(start < 48400 ? start : 0) ??
                    throw new InvalidDataException("current.transport_creation_route_start_Hex_pointer");
                if (hex.PhysicalSlot is null) return Done(null, null, "2009B0.invalid_start_Hex");
                ushort raw = current.ReadHexObjectLink(hex) ?? throw new InvalidDataException("current.transport_creation_route_start_Hex1A");
                //22A9E0 uses raw Hex1A as a City pointer-table lookup;
                //it does not first resolve the Object table/physical ordinal.
                var city = current.ReadCityTableReference(raw <= 51 ? raw : 0) ??
                    throw new InvalidDataException("current.transport_creation_route_start_City_pointer");
                bool valid = current.ReadBaseNativeValidity(city) ??
                    throw new InvalidDataException("current.transport_creation_route_start_City_validity");
                int index = valid ? 2 : 1;
                ushort? next = path.Length > index ? path[index] == 48400 ? (ushort)48400 :
                    checked((ushort)Math.Clamp(path[index], 0, 48399)) : null;
                return Done(index, next, next is null ? "2009B0.path_too_short_keep48" : "2009B0.write_path48");

                TransportArmyCreationRouteResult Done(int? index, ushort? next, string branch) =>
                    new(new(physical, goal, path, index, next, expanded, branch), null);
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
        catch (InvalidDataException missing) { return new(null, missing.Message); }
        catch (TransportArmyRouteSource.MissingOwner missing) { return new(null, missing.Message); }
    }
}
