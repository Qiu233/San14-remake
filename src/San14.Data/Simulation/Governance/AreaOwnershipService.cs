namespace San14.Data;

public interface IAreaOwnershipServiceQueries : IServiceFlagQueries
{
    ForceReference? ReadCurrentPlayerForce();
    bool? ReadForceNativeValidity(ForceReference force);
    int? ReadForcePhysicalSlot(ForceReference force);
    // A known sequence is the resolved current registry+B0 list, in native node order.
    IReadOnlyList<AreaReference?>? ReadRegisteredAreaReferences();
    byte? ReadAreaCurrentForceSlot(AreaReference area);
}

/// <summary>
/// 3E7280(1,1,0): count current registered areas via the captured force iterator key,
/// then use the shared local service flag/date implementation for bits16 and0.
/// </summary>
public static class AreaOwnershipService
{
    public static bool Refresh(IAreaOwnershipServiceQueries queries, ITransferServiceState state)
    {
        ArgumentNullException.ThrowIfNull(queries); ArgumentNullException.ThrowIfNull(state);
        if (!Known(queries.ReadTransferServiceEligibility(), "refresh eligibility")) return false;
        ForceReference player = queries.ReadCurrentPlayerForce()
            ?? throw new InvalidDataException("Current area service player force pointer is unknown.");
        if (!Known(queries.ReadForceNativeValidity(player), "player force validity")) return false;
        IReadOnlyList<AreaReference?> areas = queries.ReadRegisteredAreaReferences()
            ?? throw new InvalidDataException("Current registered area list is unknown.");
        int filterKey = Identity();
        int next = FindNext(0);
        _ = Identity(); // Native2228A0 builds the null end even for an empty list.
        int count = 0;
        while (next < areas.Count)
        {
            count = checked(count + 1);
            next = FindNext(next + 1);
            _ = Identity(); // Fresh saved receiver slot12; does not replace the captured filter key.
        }
        if (count < 200) return false;
        TransferService.SetFlag(16, queries, state);
        for (int bit = 1; bit <= 37; bit++)
            if (!Known(state.ReadPrimaryFlag(bit), $"current completion bit {bit}")) return true;
        TransferService.SetFlag(0, queries, state);
        return true;

        int Identity()
        {
            int identity = Known(queries.ReadForcePhysicalSlot(player), "saved player physical identity");
            if (identity is < 0 or >= 52)
                throw new InvalidDataException("Current area service force identity is outside the supported CE profile.");
            return identity;
        }
        int FindNext(int start)
        {
            if (filterKey is < 1 or > 51) return areas.Count;
            for (int i = start; i < areas.Count; i++)
            {
                AreaReference area = areas[i]
                    ?? throw new InvalidDataException($"Current registered area pointer {i} is unknown.");
                if (!area.PhysicalSlot.HasValue)
                    throw new InvalidDataException("Current registered area is null; native22C060 would be unsafe.");
                if (Known(queries.ReadAreaCurrentForceSlot(area), "registered area current force") == filterKey) return i;
            }
            return areas.Count;
        }
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area ownership service {field} is unknown.");
}
