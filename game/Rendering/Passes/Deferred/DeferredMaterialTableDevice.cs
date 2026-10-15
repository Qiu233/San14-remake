using Godot;
using San14.Data;

/// <summary>Typed uint texel-buffer for the source material-table slot.
/// Owns only the device buffer; the complete source entries and slot are retained.</summary>
public sealed class DeferredMaterialTableDevice : IAsyncDisposable
{
    public LoadedDeferredMaterialTable Source { get; }
    public Rid BufferRid { get; }
    private readonly RenderDeviceResources resources;
    private DeferredMaterialTableDevice(LoadedDeferredMaterialTable source, Rid buffer,RenderDeviceResources resources)
        => (Source, BufferRid, this.resources) = (source, buffer, resources);

    public static async Task<DeferredMaterialTableDevice> BuildAsync(LoadedDeferredMaterialTable source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.EntryCount < 1 || source.Bytes.Length != checked(source.EntryCount * 4))
            throw new InvalidDataException("Original material table uint buffer has an invalid source extent.");
        byte[] snapshot = source.Bytes.ToArray();
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd => {
            var resources=new RenderDeviceResources("Original material table typed uint-buffer creation failed.");
            try
            {
                // TextureBufferCreate takes texel count; its data byte extent is count*format size.
                Rid buffer=resources.Own(rd.TextureBufferCreate((uint)source.EntryCount, RenderingDevice.DataFormat.R32Uint, snapshot));
                return new DeferredMaterialTableDevice(source,buffer,resources);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Deferred-material-table-partial");
                throw;
            }
        });
    }
    public ValueTask DisposeAsync()=>resources.DisposeAsync();
}
