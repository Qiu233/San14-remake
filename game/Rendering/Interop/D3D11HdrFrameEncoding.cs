/// <summary>Current HDR lighting inputs, independent of selected programs and layouts.</summary>
internal static class D3D11HdrFrameEncoding
{
    internal const int ByteLength = 544;

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
        var filters=frame.Mode.AcceptedMainFrame.Camera.Source.Declaration.RefractionFilters;
        writer.Write((uint)new San14.Data.DeferredTransparencyImageSelection(
            filters.TransparencyResolutionOverride,catalog.CommonTransparencyResolution).SourceIndex);
        writer.Write(0x31434F48u); // HOC1: ordered modifier occlusion rates.
        foreach(var light in frame.Lights)
        {
            writer.Write(light.Lighting.Modifier.OcclusionRates.X);
            writer.Write(light.Lighting.Modifier.OcclusionRates.Y);
        }
        // HMP1 extends the ordered light transport. Null is an unknown
        // material publication; unused payload words do not imply defaults.
        writer.Write(0x31504D48u);
        foreach(var light in frame.Lights)
        {
            var material=light.Lighting.Modifier.Material;
            writer.Write(material is null ? 0u : 1u);
            if(material is null)
            {
                for(int i=0;i<14;i++)writer.Write(0f);
                continue;
            }
            var h=material.Hatching;
            writer.Write(h.RampScale);writer.Write(h.RampFade.X);writer.Write(h.RampFade.Y);
            writer.Write(h.IndexedRampScale);writer.Write(h.OccScale);
            writer.Write(h.OccFade.X);writer.Write(h.OccFade.Y);
            writer.Write(h.UVRepeat.X);writer.Write(h.UVRepeat.Y);writer.Write(h.UVRot);
            writer.Write(h.UVVelocity.X);writer.Write(h.UVVelocity.Y);writer.Write(h.UVUpdateInterval);
            writer.Write(material.ShadowMaskFade);
        }
        return stream.ToArray();
    }
}
