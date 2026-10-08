namespace San14.Data;

public sealed record OriginalCityHomeChoicePathResult(bool? NativePathFound, int? OutputNodeCount,
    int[]? PathHexes, string? UnresolvedOwner);

public static partial class OriginalPersonReturnPathSource
{
    /// <summary>260310's2AEDC0(start,end,1,output,0).1845B0 scratch0
    /// and Return's scratch2 share the same actual mode8 Search. This does
    /// not execute2318F0, land advantage or Return deadline mathematics.</summary>
    public static OriginalCityHomeChoicePathResult FillCityHomeChoicePath(OriginalEventInitializationContext current,
        OriginalHexReference start, OriginalHexReference end, OriginalHexListHandle output)
    {
        try
        {
            if (start.PhysicalSlot is null || end.PhysicalSlot is null)
                return new(false, Known(current.ReadTemporaryHexListCount(output), "2AEDC0 current output count"), [], null);
            var inner = Known(current.CreateTemporaryHexList(), "2AEDC0 inner Hex temporary");
            int[] path;
            try
            {
                current.ClearTemporaryHexList(inner);
                path = Search(current, start.PhysicalSlot.Value, end.PhysicalSlot.Value);
                foreach (int hex in path)
                    _ = current.AppendTemporaryHexReference(output, Known(current.ReadHexTableReference(hex), "2AEDC0 output Hex pointer"));
            }
            finally { current.ClearTemporaryHexList(inner); current.ReleaseTemporaryHexList(inner); }
            return new(path.Length != 0, Known(current.ReadTemporaryHexListCount(output), "2AEDC0 output count"), path, null);
        }
        catch (MissingOwner error) { return new(null, null, null, error.Message); }
    }
}
