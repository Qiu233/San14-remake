using San14.Data;

/// <summary>Encode known optical settings and the existing non-optical Main compatibility fields for NativeApi.</summary>
internal static class D3D11CurrentSeasonEncoding
{
    internal static D3D11PreparedMainWord[] MainWrites(MapSeasonCameraPostEffectScalarWriteIdentity actual,
        IReadOnlyDictionary<int, uint> selectedShadowWords)
    {
        var words = actual.Scalars.EncodedWrites.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in selectedShadowWords) words.TryAdd(pair.Key, pair.Value);
        if (actual.PreparedSpecialRectangleFlag1510 is uint flag) words[0x1510] = flag;
        return words.OrderBy(pair => pair.Key).Select(pair => new D3D11PreparedMainWord(pair.Key, pair.Value)).ToArray();
    }
}
