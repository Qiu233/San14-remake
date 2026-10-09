namespace San14.Data;

public sealed record StaticTableRange(int TableOffset, int Count);
public sealed record StaticTableReference(int? PhysicalSlot, PostLoadCallback? PostLoadCallback);

public interface IStaticTablePostLoadState
{
    StaticTableReference? ReadStaticTableReference(int tableOffset, int slot);
}

/// <summary>The 45 current-pointer loops of the first static post-load stage.
/// Every supported table has a NoOp callback. The stage requires a current
/// receiver and known callback; it does not reset fields or run later processing.</summary>
public static class StaticTablePostLoad
{
    public static IReadOnlyList<StaticTableRange> Ranges { get; } = Array.AsReadOnly(new[]
    {
        new StaticTableRange(0x6CAF0,501), new(0x7E838,501), new(0xDD38,52), new(0xE0D0,52),
        new(0x78CF8,11), new(0x73958,31), new(0xDF30,52), new(0x77428,21), new(0xDED8,11),
        new(0x79118,31), new(0xE270,48400), new(0x77B18,202), new(0x78168,101), new(0x79E90,2001),
        new(0x79210,400), new(0x6DA98,3001), new(0x7DD18,356), new(0x73A50,1851), new(0x78D50,121),
        new(0x80880,101), new(0x784F8,256), new(0x7F7E0,31), new(0x78490,13), new(0xDC20,12),
        new(0xDC80,23), new(0x774D0,201), new(0x73860,31), new(0x7F8D8,501), new(0x80BA8,501),
        new(0x81B50,1001), new(0x83A98,49), new(0x83C20,501), new(0x84BC8,51), new(0x84D60,8),
        new(0x84DA0,50), new(0x85250,201), new(0x85898,31), new(0x85990,18), new(0x85A20,5),
        new(0x85A48,9), new(0x85A90,101), new(0x85DB8,7), new(0x85DF0,7), new(0x85E28,31), new(0x85F20,80)
    });

    public static void Apply(IStaticTablePostLoadState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (StaticTableRange range in Ranges)
            for (int slot = 0; slot < range.Count; slot++)
            {
                StaticTableReference reference = state.ReadStaticTableReference(range.TableOffset,slot)
                    ?? throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} is unknown.");
                if (!reference.PhysicalSlot.HasValue)
                    throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} is null at virtual slot4 dispatch.");
                if (reference.PostLoadCallback is not { } callback)
                    throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} has an unknown post-load callback.");
                if (callback != PostLoadCallback.NoOp)
                    throw new InvalidDataException($"Current static post-load callback {callback.Kind} is not NoOp.");
                // This stage has no state writes or consumed return value.
            }
    }
}
