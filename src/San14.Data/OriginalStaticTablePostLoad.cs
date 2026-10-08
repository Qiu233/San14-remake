namespace San14.Data;

public sealed record OriginalStaticTableRange(int TableOffset, int Count);
public sealed record OriginalStaticTableReference(int? PhysicalSlot, uint? Slot4Target);

public interface IOriginalStaticTablePostLoadState
{
    OriginalStaticTableReference? ReadStaticTableReference(int tableOffset, int slot);
}

/// <summary>The45 current-pointer loops at the beginning of CE32F910.
/// Every supported table's actual slot4 is the proven370500 empty operation.
/// Empty methods still require a valid receiver and virtual dispatch target;
/// this stage neither resets fields nor substitutes the later person slot7.</summary>
public static class OriginalStaticTablePostLoad
{
    public static IReadOnlyList<OriginalStaticTableRange> Ranges { get; } = Array.AsReadOnly(new[]
    {
        new OriginalStaticTableRange(0x6CAF0,501), new(0x7E838,501), new(0xDD38,52), new(0xE0D0,52),
        new(0x78CF8,11), new(0x73958,31), new(0xDF30,52), new(0x77428,21), new(0xDED8,11),
        new(0x79118,31), new(0xE270,48400), new(0x77B18,202), new(0x78168,101), new(0x79E90,2001),
        new(0x79210,400), new(0x6DA98,3001), new(0x7DD18,356), new(0x73A50,1851), new(0x78D50,121),
        new(0x80880,101), new(0x784F8,256), new(0x7F7E0,31), new(0x78490,13), new(0xDC20,12),
        new(0xDC80,23), new(0x774D0,201), new(0x73860,31), new(0x7F8D8,501), new(0x80BA8,501),
        new(0x81B50,1001), new(0x83A98,49), new(0x83C20,501), new(0x84BC8,51), new(0x84D60,8),
        new(0x84DA0,50), new(0x85250,201), new(0x85898,31), new(0x85990,18), new(0x85A20,5),
        new(0x85A48,9), new(0x85A90,101), new(0x85DB8,7), new(0x85DF0,7), new(0x85E28,31), new(0x85F20,80)
    });

    public static void Apply(IOriginalStaticTablePostLoadState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (OriginalStaticTableRange range in Ranges)
            for (int slot = 0; slot < range.Count; slot++)
            {
                OriginalStaticTableReference reference = state.ReadStaticTableReference(range.TableOffset,slot)
                    ?? throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} is unknown.");
                if (!reference.PhysicalSlot.HasValue)
                    throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} is null at virtual slot4 dispatch.");
                if (!reference.Slot4Target.HasValue)
                    throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} has an unknown virtual slot4 target.");
                if (reference.Slot4Target.Value != 0x370500)
                    throw new InvalidDataException($"Current virtual slot4 target{reference.Slot4Target.Value:X} is outside the supported CE post-load profile.");
                // The exact370500 body is RET0 (no return-register assignment).
                // The caller ignores its return value; there are no state writes.
            }
    }
}
