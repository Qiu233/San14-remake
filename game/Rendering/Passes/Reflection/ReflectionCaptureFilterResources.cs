using Godot;
using San14.Data;

public sealed record ReflectionFilterSurface(Rid View, Rid Framebuffer, uint Width, uint Height);

/// <summary>FD9230's actual per-wrapper storage and retained mip inputs.
/// Godot's public slice API cannot express an array-wide mip-range SRV.
/// Selected previous mips use exact same-format GPU copies, refreshed only
/// after a real write to that mip. No CPU data or automatic mip generation.</summary>
public sealed class ReflectionCaptureFilterResources : IAsyncDisposable
{
    public ReflectionCaptureFieldResources Field { get; }
    public ReflectionCaptureFilterConfiguration Configuration { get; }
    public Rid ScratchRadiance { get; }
    public Rid WorkingCoefficients { get; }
    public Rid PropagationRaster { get; }
    public Rid PropagatedCoefficients { get; }
    public uint PropagationExtent { get; }
    public IReadOnlyList<ReflectionFilterSurface> ScratchSurfaces { get; }
    public ReflectionFilterSurface PropagationSurface { get; }
    private readonly Dictionary<(uint Layer, uint Mip), ReflectionFilterSurface> mipTargets;
    private readonly Rid[] mipInputs;
    private readonly ulong[] mipVersions, inputVersions;
    private readonly IReadOnlyList<Rid> owned;
    private Task? disposal;

    private ReflectionCaptureFilterResources(ReflectionCaptureFieldResources field, ReflectionCaptureFilterConfiguration configuration,
        Rid scratch, Rid working, Rid propagation, Rid propagated, uint extent, ReflectionFilterSurface[] surfaces,
        ReflectionFilterSurface propagationSurface, Dictionary<(uint, uint), ReflectionFilterSurface> targets, Rid[] inputs, List<Rid> owned)
    {
        (Field, Configuration, ScratchRadiance, WorkingCoefficients, PropagationRaster, PropagatedCoefficients, PropagationExtent,
            ScratchSurfaces, PropagationSurface, mipTargets, mipInputs, this.owned) =
            (field, configuration, scratch, working, propagation, propagated, extent, Array.AsReadOnly(surfaces), propagationSurface,
                targets, inputs, owned.AsReadOnly());
        mipVersions = new ulong[field.Plan.RadianceMipLevels];
        inputVersions = Enumerable.Repeat(ulong.MaxValue, 12).ToArray();
    }

    public static Task<ReflectionCaptureFilterResources> BuildAsync(ReflectionCaptureFieldResources field,
        ReflectionCaptureFilterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(field); field.RequireCurrent();
        uint extent = checked(field.Plan.AtlasWidth / field.Source.TileSize * configuration.SurfaceScale);
        if (extent == 0) throw new InvalidDataException("reflection.filter.propagation_extent");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            field.RequireCurrent(); var owned = new List<Rid>();
            Rid Own(Rid value) { if (!value.IsValid) throw new InvalidDataException("reflection.filter.device_storage"); owned.Add(value); return value; }
            Rid Texture(uint width, uint height, uint layers, RenderingDevice.TextureUsageBits usage)
            {
                using var format = new RDTextureFormat { Format = RenderingDevice.DataFormat.B10G11R11UfloatPack32,
                    Width = width, Height = height, Depth = 1, ArrayLayers = layers, Mipmaps = 1,
                    TextureType = RenderingDevice.TextureType.Type2DArray, Samples = RenderingDevice.TextureSamples.Samples1, UsageBits = usage };
                using var view = new RDTextureView(); return Own(rd.TextureCreate(format, view));
            }
            ReflectionFilterSurface Surface(Rid storage, uint layer, uint mip, uint width, uint height)
            {
                using var view = new RDTextureView();
                Rid target = Own(rd.TextureCreateSharedFromSlice(view, storage, layer, mip, 1, RenderingDevice.TextureSliceType.Slice2D));
                Rid framebuffer = Own(rd.FramebufferCreate([target]));
                if (!rd.FramebufferIsValid(framebuffer)) throw new InvalidDataException("reflection.filter.framebuffer");
                return new(target, framebuffer, width, height);
            }
            try
            {
                var plan = field.Plan;
                var usage = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                Rid scratch = Texture(plan.AtlasWidth, plan.AtlasHeight, plan.AtlasLayers, usage);
                Rid working = Own(rd.StorageBufferCreate((uint)plan.SphericalHarmonicsBytes));
                Rid propagation = Texture(extent, extent, 1, usage);
                Rid propagated = Own(rd.StorageBufferCreate((uint)plan.SphericalHarmonicsBytes));
                var surfaces = Enumerable.Range(0, checked((int)plan.AtlasLayers))
                    .Select(layer => Surface(scratch, (uint)layer, 0, plan.AtlasWidth, plan.AtlasHeight)).ToArray();
                var propagationSurface = Surface(propagation, 0, 0, extent, extent);
                var targets = new Dictionary<(uint, uint), ReflectionFilterSurface>(); var inputs = new Rid[12];
                for (uint mip = 0; mip < Math.Min(12u, plan.RadianceMipLevels); mip++)
                    inputs[mip] = Texture(Math.Max(1, plan.AtlasWidth >> (int)mip), Math.Max(1, plan.AtlasHeight >> (int)mip),
                        plan.AtlasLayers, RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyToBit);
                for (uint layer = 0; layer < plan.AtlasLayers; layer++)
                    for (uint mip = 1; mip < plan.RadianceMipLevels; mip++)
                        targets.Add((layer, mip), Surface(field.Radiance, layer, mip,
                            Math.Max(1, plan.AtlasWidth >> (int)mip), Math.Max(1, plan.AtlasHeight >> (int)mip)));
                var result = new ReflectionCaptureFilterResources(field, configuration, scratch, working, propagation, propagated,
                    extent, surfaces, propagationSurface, targets, inputs, owned);
                // The real FD9230 success tail, after all required resources
                // were installed. Failure above leaves capture cursor intact.
                field.Instance.ResetCaptureCursorAfterFilterStorageRebuilt();
                return result;
            }
            catch { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); throw; }
        });
    }

    internal ReflectionFilterSurface RequireMipTarget(uint layer, uint mip) => mipTargets.TryGetValue((layer, mip), out var target)
        ? target : throw new InvalidDataException("reflection.filter.mip_target");
    internal void MarkRadianceWrite(uint mip) => mipVersions[mip]++;
    internal Rid BorrowPreviousMip(RenderingDevice rd, uint mip)
    {
        RequireCurrent();
        if (mip >= mipInputs.Length || !mipInputs[mip].IsValid) throw new InvalidDataException("reflection.filter.retained_mip_alias");
        if (inputVersions[mip] != mipVersions[mip])
        {
            uint width = Math.Max(1, Field.Plan.AtlasWidth >> (int)mip), height = Math.Max(1, Field.Plan.AtlasHeight >> (int)mip);
            for (uint layer = 0; layer < Field.Plan.AtlasLayers; layer++)
                if (rd.TextureCopy(Field.Radiance, mipInputs[mip], Vector3.Zero, Vector3.Zero, new(width, height, 1), mip, 0, layer, layer) != Error.Ok)
                    throw new InvalidDataException("reflection.filter.actual_mip_copy");
            inputVersions[mip] = mipVersions[mip];
        }
        return mipInputs[mip];
    }
    internal void RequireCurrent() { ObjectDisposedException.ThrowIf(disposal is not null, this); Field.RequireCurrent(); }
    public ValueTask DisposeAsync() => new(disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); return true; }));
}
