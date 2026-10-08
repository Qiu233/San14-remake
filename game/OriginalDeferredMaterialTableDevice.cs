using Godot;
using San14.Data;

/// <summary>Typed uint texel-buffer for the source material-table slot.
/// Owns only the device buffer; the complete source entries and slot are retained.</summary>
public sealed class OriginalDeferredMaterialTableDevice : IAsyncDisposable
{
    public LoadedOriginalDeferredMaterialTable Source { get; }
    public Rid BufferRid { get; }
    private Task? _disposal;
    private OriginalDeferredMaterialTableDevice(LoadedOriginalDeferredMaterialTable source, Rid buffer)
        => (Source, BufferRid) = (source, buffer);

    public static async Task<OriginalDeferredMaterialTableDevice> BuildAsync(LoadedOriginalDeferredMaterialTable source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.EntryCount < 1 || source.Bytes.Length != checked(source.EntryCount * 4))
            throw new InvalidDataException("Original material table uint buffer has an invalid source extent.");
        byte[] snapshot = source.Bytes.ToArray();
        Rid buffer = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => {
            // TextureBufferCreate takes texel count; its data byte extent is count*format size.
            Rid rid = rd.TextureBufferCreate((uint)source.EntryCount, RenderingDevice.DataFormat.R32Uint, snapshot);
            if (!rid.IsValid) throw new InvalidDataException("Original material table typed uint-buffer creation failed.");
            return rid;
        });
        return new(source, buffer);
    }
    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => {
        rd.FreeRid(BufferRid); return true; }));
}
