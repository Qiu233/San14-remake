using Godot;

/// <summary>Explicit original cube SRV mip range. The caller keeps the parent
/// texture live until this view and every draw using it have been disposed.</summary>
public sealed class OriginalCubeShaderView : IAsyncDisposable
{
    public OriginalCubeRenderTextureDevice Owner { get; }
    public uint MostDetailedMip { get; }
    public uint MipLevels { get; }
    public Rid TextureRid { get; }
    private Task? _disposal;

    private OriginalCubeShaderView(OriginalCubeRenderTextureDevice owner, uint first, uint count, Rid rid)
        => (Owner, MostDetailedMip, MipLevels, TextureRid) = (owner, first, count, rid);

    public static Task<OriginalCubeShaderView> BuildAsync(OriginalCubeRenderTextureDevice owner,
        uint mostDetailedMip, uint mipLevels)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (mipLevels == 0 || mostDetailedMip >= owner.Source.LevelNum ||
            mipLevels > owner.Source.LevelNum - mostDetailedMip)
            throw new InvalidDataException("Cube shader view mip range exceeds the original source declaration.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            if (!rd.TextureIsValid(owner.TextureRid))
                throw new InvalidDataException("Cube shader view parent is no longer live.");
            using var view = new RDTextureView();
            Rid rid = rd.TextureCreateSharedFromSlice(view, owner.TextureRid, 0,
                mostDetailedMip, mipLevels, RenderingDevice.TextureSliceType.Cubemap);
            if (!rid.IsValid || !rd.TextureIsValid(rid))
                throw new InvalidDataException("Device rejected the original cube SRV mip range.");
            return new OriginalCubeShaderView(owner, mostDetailedMip, mipLevels, rid);
        });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(TextureRid); return true; }));
}
