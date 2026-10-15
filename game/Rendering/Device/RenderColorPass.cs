using Godot;

/// <summary>Borrowed source color attachments with a color-only framebuffer.
/// Godot RD forbids sampling framebuffer-bound depth even when depth writes
/// are disabled. Use only for a pass with explicit depth/stencil disable;
/// this never clears or writes the source depth attachment.</summary>
public sealed class RenderColorPass : IAsyncDisposable
{
    public RenderTargetFrame Source { get; }
    public Rid Framebuffer { get; }
    public IReadOnlyList<Rid> OwnerTextures { get; }
    private readonly RenderDeviceResources resources;
    private RenderColorPass(RenderTargetFrame source,Rid framebuffer,IReadOnlyList<Rid> owners,RenderDeviceResources owned)
        =>(Source,Framebuffer,OwnerTextures,resources)=(source,framebuffer,owners,owned);
    public static async Task<RenderColorPass> BuildAsync(RenderTargetFrame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.ColorViews.Count!=1)throw new InvalidDataException("Deferred color pass requires one source color attachment.");
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var resources=new RenderDeviceResources("Color-only output framebuffer was rejected.");
            try
            {
                if(source.ColorViews.Any(r=>!rd.TextureIsValid(r)))throw new InvalidDataException("Color output is no longer live.");
                var attachments=new Godot.Collections.Array<Rid>(source.ColorViews);
                Rid rid=resources.Own(rd.FramebufferCreate(attachments));
                if(!rd.FramebufferIsValid(rid))throw new InvalidDataException("Color-only output framebuffer was rejected.");
                return new RenderColorPass(source,rid,
                    source.Source.ColorTargets.Select(t=>source.Textures[t.ObjectNameHash].StorageRid).Distinct().ToArray(),resources);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Render-color-pass-partial");
                throw;
            }
        });
    }
    public ValueTask DisposeAsync()=>resources.DisposeAsync();
}
