namespace San14.Data;

/// <summary>A current source pointer; known absence differs from an unread value.</summary>
public readonly record struct UnitWorldGroundHeightReference<T>(bool IsKnown, T? Value) where T : class
{
    public static UnitWorldGroundHeightReference<T> Known(T? value) => new(true, value);
    public static UnitWorldGroundHeightReference<T> Unknown => new(false, null);
    internal T? Read(string field) => IsKnown ? Value :
        throw new InvalidDataException($"Current GroundHeight terrain pointer {field} is unknown.");
}

public sealed record UnitWorldGroundHeightTerrainOwner(
    UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainHolder> Holder8);
public sealed record UnitWorldGroundHeightTerrainHolder(
    UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainList> List20);
public sealed record UnitWorldGroundHeightTerrainList(uint? Count50,
    UnitWorldGroundHeightReference<IReadOnlyList<UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainEntry>>> Entries20);
public sealed record UnitWorldGroundHeightTerrainEntry(
    UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainData> Data20);
public sealed record UnitWorldGroundHeightTerrainData(
    UnitWorldGroundHeightReference<UnitWorldGroundHeightGrid> GridB0);

/// <summary>The exact pointer-selection wrapper SC BAA9E0 / TC BA9890.
/// Entries with absent +20 are skipped; the first present +20 ends the scan.</summary>
public static class UnitWorldGroundHeightScene
{
    public static float QueryBAA9E0(UnitWorldGroundHeightTerrainOwner owner, float x, float z)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var holder = owner.Holder8.Read("owner+8");
        if (holder is null) return 0f;
        var list = holder.List20.Read("holder+20");
        if (list is null) return 0f;
        uint count = list.Count50 ?? throw new InvalidDataException("Current GroundHeight terrain count +50 is unknown.");
        if (count == 0) return 0f;
        var entries = list.Entries20.Read("list+20");
        if (entries is null)
            throw new InvalidDataException("Current GroundHeight terrain entry-array pointer is null with a nonzero source count.");
        for (uint index = 0; index < count; index++)
        {
            if (index >= entries.Count)
                throw new InvalidDataException($"Current GroundHeight terrain entry[{index}] is unknown.");
            var entry = entries[(int)index].Read($"entry[{index}]");
            if (entry is null) continue;
            var data = entry.Data20.Read($"entry[{index}]+20");
            if (data is null) continue;
            var grid = data.GridB0.Read("terrain data+B0");
            return grid is null ? 0f : UnitWorldGroundHeight.QueryA0B830(grid, x, z);
        }
        return 0f;
    }
}
