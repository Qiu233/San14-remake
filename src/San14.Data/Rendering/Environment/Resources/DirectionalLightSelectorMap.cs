namespace San14.Data;

/// <summary>A selector's sampling geometry over an owned raw resource. Selectors
/// may share map bytes while retaining different dimensions, cells and origins.</summary>
internal sealed class DirectionalLightSelectorMap
{
    private readonly MapSeasonLightSelectorDeclaration selector;
    private readonly byte[] values;

    internal DirectionalLightSelectorMap(MapSeasonLightSelectorDeclaration declaration, byte[] bytes)
    {
        if (declaration.GridWidth == 0 || declaration.GridHeight == 0 ||
            declaration.CellSizeX == 0 || declaration.CellSizeZ == 0 ||
            !float.IsFinite(declaration.OriginX) || !float.IsFinite(declaration.OriginZ))
            throw new InvalidDataException("Directional selector requires positive dimensions and cells with finite origins.");
        ulong count = (ulong)declaration.GridWidth * declaration.GridHeight;
        if (count > int.MaxValue)
            throw new NotSupportedException("Directional selector grid exceeds the supported managed array range.");
        if ((ulong)bytes.LongLength != count)
            throw new InvalidDataException("Directional selector raw map length does not match its declared grid.");
        selector = declaration;
        values = bytes;
    }

    internal byte Sample(float locationX, float locationZ)
    {
        int x = Cell(locationX, selector.OriginX, selector.CellSizeX, selector.GridWidth);
        int z = Cell(locationZ, selector.OriginZ, selector.CellSizeZ, selector.GridHeight);
        return values[z * (int)selector.GridWidth + x];
    }

    private static int Cell(float location, float origin, uint cellSize, uint dimension)
    {
        // Preserve binary32 subtraction, signed truncation, unsigned DIV, then
        // signed clamping. Negative coordinates do not simply clamp to zero.
        float delta = location - origin;
        if (!float.IsFinite(delta) || delta < int.MinValue || (double)delta >= 2147483648d)
            throw new NotSupportedException("Directional selector location exceeds the supported signed32 conversion range.");
        uint quotient = unchecked((uint)(int)delta) / cellSize;
        return Math.Clamp(unchecked((int)quotient), 0, (int)dimension - 1);
    }
}
