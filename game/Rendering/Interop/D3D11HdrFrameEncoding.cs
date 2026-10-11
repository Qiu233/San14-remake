/// <summary>Current HDR lighting inputs, independent of selected programs and layouts.</summary>
internal static class D3D11HdrFrameEncoding
{
    internal const int ByteLength = 236;

    /// <summary>Modifiers follow the same ordered light records in the season
    /// packet. The native lighting owner prepares actual outer/inner values.</summary>
    internal static byte[] Write(D3D11CurrentSeasonFrame frame, D3D11CurrentHdrCatalog catalog)
    {
        using var stream = new MemoryStream(ByteLength); using var writer = new BinaryWriter(stream);
        writer.Write(0x31434C48u); // HLC1: HDR lighting current inputs.
        writer.Write(catalog.AmbientMapsRate);
        writer.Write(catalog.SuppressAmbient ? 1u : 0u);
        writer.Write(catalog.FogOwnerEnabled ? 1u : 0u);
        foreach (var light in frame.Lights)
        {
            var modifier = light.Lighting.Modifier;
            writer.Write(modifier.Present ? 1u : 0u);
            writer.Write(modifier.AdjustsLighting ? 1u : 0u);
            writer.Write(modifier.DirectionalFactors.X); writer.Write(modifier.DirectionalFactors.Y);
            writer.Write(modifier.DirectionalFactors.Z); writer.Write(modifier.DirectionalFactors.W);
            writer.Write(modifier.AmbientFactor); writer.Write(modifier.HemisphereFactor);
        }
        // HSC1 follows the fixed 176-byte lighting block. These are current
        // semantic inputs; resource program indices remain in the bank.
        writer.Write(0x31435348u);
        writer.Write(frame.Optics.Surface.SpecularOcclusionType);
        writer.Write(catalog.CameraReflectionDeclaration);
        writer.Write(catalog.CommonEffectMode);
        writer.Write(0x31434F48u); // HOC1: ordered modifier occlusion rates.
        foreach(var light in frame.Lights)
        {
            writer.Write(light.Lighting.Modifier.OcclusionRates.X);
            writer.Write(light.Lighting.Modifier.OcclusionRates.Y);
        }
        return stream.ToArray();
    }
}
