using System.Text;

/// <summary>Encode current HDR numeric writes using the loaded shader layout.</summary>
internal static class D3D11HdrFrameEncoding
{
    /// <summary>Only current field bytes and actual selected destination ranges.
    /// No source I/O, hashes, shader creation or whole CB template copies.</summary>
    internal static byte[] Write(D3D11CurrentSeasonFrame frame, D3D11CurrentHdrCatalog catalog)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(catalog.Routes.Count);
        foreach (var route in catalog.Routes)
        {
            var fields = frame.DeferredNamedFields(route.OuterMask, route.InnerMask,
                route.Arg18, catalog.AmbientMapsRate, route.FogOwnerEnabled);
            byte[] identity = Encoding.UTF8.GetBytes(route.Identity);
            writer.Write(identity.Length); writer.Write(route.Slot);
            writer.Write(route.ConstantBytes); writer.Write(route.Writes.Count); writer.Write(identity);
            foreach (var write in route.Writes)
            { writer.Write(write.Offset); writer.Write(write.Bytes);
                writer.Write(fields[write.Name].AsSpan(write.SourceOffset, write.Bytes)); }
        }
        return stream.ToArray();
    }
}
