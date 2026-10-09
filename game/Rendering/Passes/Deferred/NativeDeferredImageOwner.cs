using Godot;
using San14.Data;

/// <summary>Owns recovered source contracts for the auxiliary PS1 images and
/// borrows the original graph's target frames. Re-reads views after source frame
/// replacement. Allocation/content production belongs to the graph target owner;
/// this provider never creates substitutes for unbound or unproduced images.</summary>
public sealed class NativeDeferredImageOwner : IAsyncDisposable
{
    public LoadedDeferredImageSources Source { get; }
    public IReadOnlyList<DeferredSourceNull2D> FreshSourceNulls { get; }
    private volatile bool _retired;
    private NativeDeferredImageOwner(LoadedDeferredImageSources source,
        IReadOnlyList<DeferredSourceNull2D> nulls) => (Source, FreshSourceNulls) = (source, nulls);

    public static NativeDeferredImageOwner FromSource(LoadedDeferredTexturePlan deferred,
        MainLightConstructionPacket? explicitFreshLight = null,
        DeferredImageStateScope scope = DeferredImageStateScope.CurrentOrEditedState)
    {
        var source = DeferredImageSources.Read(deferred);
        IReadOnlyList<DeferredSourceNull2D> nulls = explicitFreshLight is null ? []
            : DeferredSourceNull2D.FromFreshConstruction(explicitFreshLight, scope);
        return new(source, nulls);
    }

    /// <summary>Reads actual graph-created shader views for t8/t9. The C9AB00
    /// source override and Camera field are explicit; no active branch is guessed.
    /// Frame creation alone does not establish that graph writers have run.</summary>
    public Task<IReadOnlyList<DeferredDeviceBinding>> ReadTargetBindingsAsync(
        DeferredTransparencyImageSelection selection,
        IReadOnlyList<RenderTargetFrame> explicitSourceFrames,
        bool explicitRefractionEnabled)
    {
        ArgumentNullException.ThrowIfNull(explicitSourceFrames);
        ObjectDisposedException.ThrowIf(_retired, this);
        var source = DeferredImageSources.Read(Source.Deferred);
        var frames = explicitSourceFrames.ToArray();
        if (frames.Any(f => f is null)) throw new InvalidDataException("Null deferred image source frame.");
        var desired = new List<DeferredTargetImageSource> { source.SelectSpecular(selection), source.BlurredDepth };
        if (explicitRefractionEnabled) desired.Insert(0, source.RefractionDepth);
        var selected = new List<(DeferredTargetImageSource Source, RenderTargetDeviceTexture Device)>();
        foreach (var input in desired)
        {
            var matches = frames.SelectMany(f => f.Textures.Values).Where(t => t.Source.Object.ObjectId == input.Texture.Object.ObjectId &&
                t.Source.Source.Source.ResourceUri == input.Texture.Source.Source.ResourceUri &&
                t.Source.Source.Source.DecodedSha256 == input.Texture.Source.Source.DecodedSha256).DistinctBy(t => t.StorageRid).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"Deferred {input.Role} requires one actual source graph target view.");
            var texture = matches[0];
            var canonical = RenderTextureResources.Read(texture.Source.Source, texture.Source.Object.ObjectId);
            if (canonical.Kind != input.Texture.Kind || canonical.Width != input.Texture.Width || canonical.Height != input.Texture.Height ||
                RenderTextureFormatMath.Resolve(canonical) != RenderTextureFormatMath.Resolve(input.Texture) ||
                canonical.LevelNum != input.Texture.LevelNum || canonical.MinMipLevel != input.Texture.MinMipLevel ||
                canonical.MultiSample != input.Texture.MultiSample)
                throw new InvalidDataException("Deferred auxiliary target differs from its exact source declaration.");
            selected.Add((input, texture));
        }
        return TerrainDeviceTexture.OnRenderThreadAsync<IReadOnlyList<DeferredDeviceBinding>>(rd =>
        {
            ObjectDisposedException.ThrowIf(_retired, this);
            foreach (var pair in selected)
            {
                var device = pair.Device;
                if (!device.StorageRid.IsValid || !device.ShaderViewRid.IsValid ||
                    !rd.TextureIsValid(device.StorageRid) || !rd.TextureIsValid(device.ShaderViewRid))
                    throw new InvalidDataException("Deferred source target has retired.");
                using var actual = rd.TextureGetFormat(device.ShaderViewRid);
                if (actual.TextureType != RenderingDevice.TextureType.Type2D || actual.ArrayLayers != 1 || actual.Mipmaps != 1 ||
                    actual.Width != (uint)device.ExplicitExtent.X || actual.Height != (uint)device.ExplicitExtent.Y ||
                    actual.Format != device.ShaderFormat || !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                    throw new InvalidDataException("Deferred source view differs from its recovered float Texture2D shape.");
            }
            return Array.AsReadOnly(selected.Select(p => new DeferredDeviceBinding("Texture", p.Source.PixelBinding.BindPoint,
                p.Device.ShaderViewRid, p.Device.StorageRid,
                p.Source.Texture.Source.Source.ResourceUri + "#" + p.Source.Texture.Object.ObjectId.ToString("X8") +
                ";F103-source=" + p.Source.SourceArgument.NameHash.ToString("X8"))).ToArray());
        });
    }

    public Task<IReadOnlyList<DeferredDeviceBinding>> ReadTransparencyTargetBindingsAsync(
        DeferredTransparencyImageSelection selection, IReadOnlyList<RenderTargetFrame> explicitSourceFrames)
        => ReadTargetBindingsAsync(selection, explicitSourceFrames, false);

    public ValueTask DisposeAsync() { _retired = true; return ValueTask.CompletedTask; }
}
