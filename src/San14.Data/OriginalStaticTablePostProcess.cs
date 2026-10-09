namespace San14.Data;

public sealed record OriginalStaticPostProcessReference(int TableOffset, int? PhysicalSlot, OriginalPostLoadCallback? Callback);
public interface IOriginalStaticTablePostProcessState
{
    OriginalStaticPostProcessReference? ReadStaticPostProcessReference(int tableOffset, int slot);
    void InvokeStaticPostProcess(OriginalStaticPostProcessReference reference);
}

/// <summary>The 43 current-pointer loops of static post-processing. They exclude
/// both caution tables and invoke callbacks independently of the earlier NoOp stage.</summary>
public static class OriginalStaticTablePostProcess
{
    public static IReadOnlyList<OriginalStaticTableRange> Ranges { get; } = Array.AsReadOnly(new[]
    {
        new OriginalStaticTableRange(0x6CAF0,501), new(0x7E838,501), new(0xDD38,52), new(0xE0D0,52),
        new(0x78CF8,11), new(0x73958,31), new(0xDF30,52), new(0x77428,21), new(0xDED8,11),
        new(0x79118,31), new(0xE270,48400), new(0x77B18,202), new(0x78168,101), new(0x79E90,2001),
        new(0x79210,400), new(0x6DA98,3001), new(0x7DD18,356), new(0x73A50,1851), new(0x78D50,121),
        new(0x80880,101), new(0x784F8,256), new(0x7F7E0,31), new(0x78490,13), new(0xDC20,12),
        new(0xDC80,23), new(0x774D0,201), new(0x73860,31), new(0x7F8D8,501), new(0x83A98,49),
        new(0x83C20,501), new(0x84BC8,51), new(0x84D60,8), new(0x84DA0,50), new(0x85250,201),
        new(0x85898,31), new(0x85990,18), new(0x85A20,5), new(0x85A48,9), new(0x85A90,101),
        new(0x85DB8,7), new(0x85DF0,7), new(0x85E28,31), new(0x85F20,80)
    });

    public static OriginalPostLoadCallback SupportedCallback(int tableOffset)
    {
        if (!Ranges.Any(range => range.TableOffset == tableOffset)) throw new InvalidDataException("Unknown static post-processing table offset.");
        return new(tableOffset switch
        {
            0xDD38 => OriginalPostLoadCallbackKind.City,
            0xE0D0 => OriginalPostLoadCallbackKind.District,
            0xDED8 => OriginalPostLoadCallbackKind.Gate,
            0x7DD18 => OriginalPostLoadCallbackKind.Personality,
            0x73A50 => OriginalPostLoadCallbackKind.Person,
            0x80880 => OriginalPostLoadCallbackKind.Policy,
            _ => OriginalPostLoadCallbackKind.NoOp
        });
    }

    public static void Apply(IOriginalStaticTablePostProcessState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (OriginalStaticTableRange range in Ranges)
            for (int slot = 0; slot < range.Count; slot++)
            {
                OriginalStaticPostProcessReference reference = state.ReadStaticPostProcessReference(range.TableOffset, slot)
                    ?? throw new InvalidDataException($"Current static table{range.TableOffset:X} pointer{slot} is unknown.");
                if (!reference.PhysicalSlot.HasValue) throw new InvalidDataException("Static post-processing requires a current receiver.");
                var callback = reference.Callback ?? throw new InvalidDataException("Current static post-process callback is unknown.");
                if (callback != SupportedCallback(range.TableOffset))
                    throw new InvalidDataException($"Unsupported static post-process callback {callback.Kind} for table{range.TableOffset:X}.");
                if (callback.Kind != OriginalPostLoadCallbackKind.NoOp) state.InvokeStaticPostProcess(reference);
            }
    }
}
