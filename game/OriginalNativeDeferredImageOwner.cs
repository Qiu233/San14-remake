using Godot;
using San14.Data;

/// <summary>Owns recovered source contracts for the auxiliary PS1 images and
/// borrows the original graph's target frames. Re-reads views after source frame
/// replacement. Allocation/content production belongs to the graph target owner;
/// this provider never creates substitutes for unbound or unproduced images.</summary>
public sealed class OriginalNativeDeferredImageOwner : IAsyncDisposable
{
    public LoadedOriginalDeferredImageSources Source { get; }
    public IReadOnlyList<OriginalDeferredSourceNull2D> FreshSourceNulls { get; }
    private volatile bool _retired;
    private OriginalNativeDeferredImageOwner(LoadedOriginalDeferredImageSources source,
        IReadOnlyList<OriginalDeferredSourceNull2D> nulls) => (Source, FreshSourceNulls) = (source, nulls);

    public static OriginalNativeDeferredImageOwner FromSource(LoadedOriginalDeferredTexturePlan deferred,
        OriginalMainLightConstructionPacket? explicitFreshLight = null,
        OriginalDeferredImageStateScope scope = OriginalDeferredImageStateScope.CurrentOrEditedState)
    {
        var source = OriginalDeferredImageSources.Read(deferred);
        IReadOnlyList<OriginalDeferredSourceNull2D> nulls = explicitFreshLight is null ? []
            : OriginalDeferredSourceNull2D.FromFreshConstruction(explicitFreshLight, scope);
        return new(source, nulls);
    }

    /// <summary>Reads actual graph-created shader views for t8/t9. The C9AB00
    /// source override and Camera field are explicit; no active branch is guessed.
    /// Frame creation alone does not establish that graph writers have run.</summary>
    public Task<IReadOnlyList<OriginalDeferredDeviceBinding>> ReadTargetBindingsAsync(
        OriginalDeferredTransparencyImageSelection selection,
        IReadOnlyList<OriginalRenderTargetFrame> explicitSourceFrames,
        bool explicitRefractionEnabled)
    {
        ArgumentNullException.ThrowIfNull(explicitSourceFrames);
        ObjectDisposedException.ThrowIf(_retired, this);
        var source = OriginalDeferredImageSources.Read(Source.Deferred);
        var frames = explicitSourceFrames.ToArray();
        if (frames.Any(f => f is null)) throw new InvalidDataException("Null deferred image source frame.");
        var desired = new List<OriginalDeferredTargetImageSource> { source.SelectSpecular(selection), source.BlurredDepth };
        if (explicitRefractionEnabled) desired.Insert(0, source.RefractionDepth);
        var selected = new List<(OriginalDeferredTargetImageSource Source, OriginalRenderTargetDeviceTexture Device)>();
        foreach (var input in desired)
        {
            var matches = frames.SelectMany(f => f.Textures.Values).Where(t => t.Source.Object.ObjectId == input.Texture.Object.ObjectId &&
                t.Source.Source.Source.ResourceUri == input.Texture.Source.Source.ResourceUri &&
                t.Source.Source.Source.DecodedSha256 == input.Texture.Source.Source.DecodedSha256).DistinctBy(t => t.StorageRid).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"Deferred {input.Role} requires one actual source graph target view.");
            var texture = matches[0];
            var canonical = OriginalRenderTextureResources.Read(texture.Source.Source, texture.Source.Object.ObjectId);
            if (canonical.Kind != input.Texture.Kind || canonical.Width != input.Texture.Width || canonical.Height != input.Texture.Height ||
                OriginalRenderTextureFormatMath.Resolve(canonical) != OriginalRenderTextureFormatMath.Resolve(input.Texture) ||
                canonical.LevelNum != input.Texture.LevelNum || canonical.MinMipLevel != input.Texture.MinMipLevel ||
                canonical.MultiSample != input.Texture.MultiSample)
                throw new InvalidDataException("Deferred auxiliary target differs from its exact source declaration.");
            selected.Add((input, texture));
        }
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync<IReadOnlyList<OriginalDeferredDeviceBinding>>(rd =>
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
            return Array.AsReadOnly(selected.Select(p => new OriginalDeferredDeviceBinding("Texture", p.Source.PixelBinding.BindPoint,
                p.Device.ShaderViewRid, p.Device.StorageRid,
                p.Source.Texture.Source.Source.ResourceUri + "#" + p.Source.Texture.Object.ObjectId.ToString("X8") +
                ";F103-source=" + p.Source.SourceArgument.NameHash.ToString("X8"))).ToArray());
        });
    }

    public Task<IReadOnlyList<OriginalDeferredDeviceBinding>> ReadTransparencyTargetBindingsAsync(
        OriginalDeferredTransparencyImageSelection selection, IReadOnlyList<OriginalRenderTargetFrame> explicitSourceFrames)
        => ReadTargetBindingsAsync(selection, explicitSourceFrames, false);

    public ValueTask DisposeAsync() { _retired = true; return ValueTask.CompletedTask; }
}
