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
    private Task? _disposal;
    private RenderColorPass(RenderTargetFrame source,Rid framebuffer,IReadOnlyList<Rid> owners)
        =>(Source,Framebuffer,OwnerTextures)=(source,framebuffer,owners);
    public static async Task<RenderColorPass> BuildAsync(RenderTargetFrame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.ColorViews.Count!=1)throw new InvalidDataException("Deferred color pass requires one source color attachment.");
        Rid framebuffer=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(source.ColorViews.Any(r=>!rd.TextureIsValid(r)))throw new InvalidDataException("Color output is no longer live.");
            Rid rid=rd.FramebufferCreate(new Godot.Collections.Array<Rid>(source.ColorViews));
            if(!rid.IsValid||!rd.FramebufferIsValid(rid))throw new InvalidDataException("Color-only output framebuffer was rejected.");
            return rid;
        });
        return new(source,framebuffer,source.Source.ColorTargets.Select(t=>source.Textures[t.ObjectNameHash].StorageRid).Distinct().ToArray());
    }
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(Framebuffer);return true;}));
}
