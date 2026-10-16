using San14.Data;

/// <summary>Current camera controls and ordered light modifiers for contact
/// occlusion. Native selects groups only after preparing the same-frame View.</summary>
internal static class D3D11ContactOcclusionEncoding
{
    internal static byte[] Write(D3D11CurrentSeasonFrame frame)
    {
        using var stream=new MemoryStream();
        using var writer=new BinaryWriter(stream);
        writer.Write(0x3143434Fu); // OCC1: semantic contact-occlusion inputs.
        writer.Write(frame.Lights.Count);
        ContactOcclusionCurrentEncoding.WriteSettings(writer,
            CameraOcclusionSettingsImport.ReadCurrent(frame.Present));
        foreach(var light in frame.Lights)
        {
            writer.Write(light.GroupMask);
            ContactOcclusionCurrentEncoding.WriteModifier(writer,light.State.ContactOcclusion);
        }
        return stream.ToArray();
    }
}
