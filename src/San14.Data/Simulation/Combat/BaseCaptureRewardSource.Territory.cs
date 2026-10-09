using San14.Core;

namespace San14.Data;

public static partial class BaseCaptureRewardSource
{
    private static int FillStates(GameState state, SourceWorldContext current,
        ForceReference force, HexListHandle output)
    {
        current.ClearTemporaryHexList(output);
        if (!K(current.ReadForceNativeValidity(force), "2AB190 Force validity")) return 0;
        int owner = K(current.ReadForcePhysicalSlot(force), "2AB190 Force physical");
        //2AB190 really allocates2AAD90's City list before its State scan,
        //even though the scan itself subsequently uses manager90.
        var cities = Temporary(current);
        try
        {
            current.ClearTemporaryHexList(cities);
            if (owner is >= 1 and <= 51)
                for (int index = 0; index < 52; index++)
                {
                    var city = current.ReadCityTableReference(index) ?? throw Missing("2AAD90 City table pointer");
                    if (ReadBaseForce(state, current, city) == owner &&
                        K(current.ReadBaseNativeValidity(city), "2AAD90 City validity"))
                        current.AppendCaptureBaseTemporary(cities, city);
                }
            for (int index = 0; index < 23; index++)
            {
                var province = current.ReadStaticTableReference(0xDC80, index) ?? throw Missing("2AB190 State pointer");
                if (province.PhysicalSlot is not int physical || physical == 0) continue;
                if (physical is < 0 or >= 23) throw Missing("2AB190 State physical");
                int count = 0; bool allOwned = true;
                var registry = current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("2AB190 City90 manager");
                foreach (var entry in registry)
                {
                    if (entry?.Kind != RegistryKind.City || entry.PhysicalSlot is not int cityId) throw Missing("2AB190 City90 payload");
                    var city = new BaseReference(BaseKind.City, cityId);
                    if (!K(current.ReadBaseNativeValidity(city), "2AB190 City validity")) continue;
                    if (ReadCityState(state, cityId) != physical) continue;
                    count++;
                    if (ReadBaseForce(state, current, city) == owner) continue;
                    allOwned = false; break;
                }
                if (count > 0 && allOwned) current.AppendCaptureStaticTemporary(output, "State", physical);
            }
            return K(current.ReadTemporaryHexListCount(output), "2AB190 State count");
        }
        finally { Release(current, cities); }
    }

    private static int FillRegions(GameState state, SourceWorldContext current,
        ForceReference force, HexListHandle states, HexListHandle output)
    {
        current.ClearTemporaryHexList(output);
        if (!K(current.ReadForceNativeValidity(force), "2AAFB0 Force validity")) return 0;
        for (int index = 0; index < 12; index++)
        {
            var region = current.ReadStaticTableReference(0xDC20, index) ?? throw Missing("2AAFB0 Region pointer");
            if (region.PhysicalSlot is not int physical || physical == 0) continue;
            if (physical is < 0 or >= 12) throw Missing("2AAFB0 Region physical");
            bool complete = true;
            for (int at = 0; at < 23; at++)
            {
                var province = current.ReadStaticTableReference(0xDC80, at) ?? throw Missing("2AAFB0 State pointer");
                if (province.PhysicalSlot is not int statePhysical || statePhysical == 0) continue;
                if (K(BaseCaptureRewardInputs.ReadStateRegion(state, statePhysical), "2AAFB0 current State2E") != physical) continue;
                if (current.ContainsCaptureStaticTemporary(states, "State", statePhysical)) continue;
                complete = false; break;
            }
            //The source permits a valid Region with no matching valid State.
            //Do not add a nonempty membership condition.
            if (complete) current.AppendCaptureStaticTemporary(output, "Region", physical);
        }
        return K(current.ReadTemporaryHexListCount(output), "2AAFB0 Region count");
    }

    private static byte ReadCityState(GameState state, int physical)
    {
        var values = state.NativeArmySimulation?.Parameters.Int32Values;
        if (values is null || values.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) != 1 ||
            !values.TryGetValue(PersonGrowthCurrentQueries.CityStateKey(physical), out int raw) || raw is < 0 or > 255)
            throw Missing("2AB190 current CityCE");
        return (byte)raw;
    }

    private static int ReadBaseForce(GameState state, SourceWorldContext current, BaseReference city)
    {
        if (current.ReadBaseForceSlot(city) is int force) return force;
        //Reuse the same saved raw District0 query as the current growth /
        //land-advantage owner. Core null alone does not establish native0.
        if (city.Kind == BaseKind.City && city.PhysicalSlot is int physical &&
            state.Cities.TryGetValue(physical, out var saved) && saved.DistrictId is null &&
            state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
            values.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) == 1 &&
            values.TryGetValue(PersonGrowthCurrentQueries.CityDistrict26Key(physical), out int raw) &&
            raw is >= 0 and <= 255 && (raw == 0 || raw > 51))
        {
            current.SetCityDistrictSlot(physical, (byte)raw);
            return K(current.ReadBaseForceSlot(city), "2AB190 saved raw District0 City B0");
        }
        throw Missing("2AB190 current City B0");
    }
}

public sealed partial class SourceWorldContext
{
    internal bool AppendCaptureStaticTemporary(HexListHandle handle, string kind, int physical)
    {
        var node = AllocateHexListNode(handle);
        if (!node.Identity.HasValue) return false;
        WriteCurrentListPayload(node.Identity.Value, new ReturnStaticTablePayload(kind, physical));
        return true;
    }
    internal bool AppendCaptureBaseTemporary(HexListHandle handle, BaseReference city)
    {
        var node = AllocateHexListNode(handle);
        if (!node.Identity.HasValue) return false;
        WriteCurrentListPayload(node.Identity.Value, city); return true;
    }
    internal bool ContainsCaptureStaticTemporary(HexListHandle handle, string kind, int physical)
    {
        var node = ReadHexListHead(handle) ?? throw new InvalidDataException("Current capture State list head is unknown.");
        while (node.Identity is int identity)
        {
            if (ReadCurrentListPayload(identity) is not ReturnStaticTablePayload payload || payload.TableKind != kind)
                throw new InvalidDataException("Current capture State list payload is unknown or of another kind.");
            if (payload.PhysicalSlot == physical) return true;
            node = ReadHexListNodeNext(node) ?? throw new InvalidDataException("Current capture State list next is unknown.");
        }
        return false;
    }
}
