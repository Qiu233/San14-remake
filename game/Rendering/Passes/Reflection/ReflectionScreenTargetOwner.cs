using Godot;
using San14.Data;

/// <summary>EC82A0 target views owned for one actual target-storage generation.
/// It borrows the selected terminal texture and, only for null descriptors,
/// the actual current framebuffer. It never allocates replacement RR storage.</summary>
public sealed class ReflectionScreenTargetOwner : IAsyncDisposable
{
    public LoadedReflectionProducerPlan Source { get; }
    public ReflectionScreenTargetPlan Plan { get; }
    public RenderTargetFrame TargetStorage { get; }
    public RenderTargetFrame? InheritedFramebuffer { get; }
    public IReadOnlyList<ReflectionFinalAttachment> Attachments { get; }
    public uint Width { get; }
    public uint Height { get; }
    private readonly IReadOnlyList<Rid> framebuffers;
    private readonly IReadOnlyList<Rid> owned;
    private Task? disposal;

    private ReflectionScreenTargetOwner(LoadedReflectionProducerPlan source, ReflectionScreenTargetPlan plan,
        RenderTargetFrame storage, RenderTargetFrame? inherited, ReflectionFinalAttachment[] targets,
        uint width, uint height, Rid[] framebuffers, List<Rid> owned)
        => (Source, Plan, TargetStorage, InheritedFramebuffer, Attachments, Width, Height, this.framebuffers, this.owned) =
            (source, plan, storage, inherited, Array.AsReadOnly(targets), width, height,
                Array.AsReadOnly(framebuffers), owned.AsReadOnly());

    public static Task<ReflectionScreenTargetOwner> BuildAsync(LoadedReflectionProducerPlan source,
        RenderTargetFrame actualTargetStorage, RenderTargetFrame? actualCurrentFramebuffer, bool active)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualTargetStorage);
        if (!actualTargetStorage.Textures.TryGetValue(source.Target.Object.ObjectId, out var target) ||
            target.Source.Object.ObjectId != source.Target.Object.ObjectId ||
            target.Source.Source.Source.ResourceUri != source.Target.Source.Source.ResourceUri ||
            target.Source.Source.Source.DecodedSha256 != source.Target.Source.Source.DecodedSha256 ||
            target.ExplicitExtent.X <= 0 || target.ExplicitExtent.Y <= 0)
            throw new InvalidDataException("reflection.screen.current_logical_target_owner");
        var plan = ReflectionScreenTargetPlan.Prepare(target.EffectiveSource, active);
        if (!plan.RawSliceDescriptorsPresent && (actualCurrentFramebuffer is null ||
            actualCurrentFramebuffer.ColorViews.Count < (active ? 3 : 1)))
            throw new InvalidDataException("reflection.screen.current_framebuffer_slots");
        // A nonnull raw descriptor is interpreted only within this real
        // terminal. The ordinary RR3 fails Prepare for raw4/5 before any device
        // view is created; no modulo, descriptor expansion or alias is used.
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid value) { if (!value.IsValid) throw new InvalidDataException("reflection.screen.target_resource"); owned.Add(value); return value; }
            try
            {
                if (!rd.TextureIsValid(target.StorageRid)) throw new ObjectDisposedException(nameof(actualTargetStorage));
                using (var format = rd.TextureGetFormat(target.StorageRid))
                    if (format.TextureType != RenderingDevice.TextureType.Type2DArray ||
                        format.ArrayLayers != target.EffectiveSource.ArrayDepth || format.Mipmaps != 1 ||
                        format.Width != target.ExplicitExtent.X || format.Height != target.ExplicitExtent.Y)
                        throw new InvalidDataException("reflection.screen.terminal_storage_shape");
                var attachments = new ReflectionFinalAttachment[3];
                for (int index = 0; index < attachments.Length; index++)
                {
                    var selection = plan.Targets[index];
                    if (selection.InheritedFramebufferSlot is uint slot)
                    {
                        var current = actualCurrentFramebuffer!;
                        if (!rd.FramebufferIsValid(current.Framebuffer)) throw new ObjectDisposedException(nameof(actualCurrentFramebuffer));
                        var selected = current.Source.ColorTargets[checked((int)slot)];
                        var backing = current.Textures[selected.ObjectNameHash];
                        attachments[index] = new(current.ColorViews[checked((int)slot)], backing.StorageRid,
                            backing.Source.Source.Source.ResourceUri + "#current-framebuffer/" + slot + "/" + backing.EffectiveSource.OwnerIdentity);
                    }
                    else
                    {
                        using var view = new RDTextureView { FormatOverride = target.TargetFormat };
                        Rid slice = Own(rd.TextureCreateSharedFromSlice(view, target.StorageRid,
                            selection.ArrayLayer!.Value, 0, 1, RenderingDevice.TextureSliceType.Slice2D));
                        attachments[index] = new(slice, target.StorageRid,
                            target.Source.Source.Source.ResourceUri + "#raw/" + selection.RawDescriptorIndex + "/" + target.EffectiveSource.OwnerIdentity);
                    }
                }
                uint width = checked((uint)target.ExplicitExtent.X), height = checked((uint)target.ExplicitExtent.Y);
                foreach (var attachment in attachments) Validate(rd, attachment, width, height, active);
                if (active && attachments.Select(value => value.ColorView).Distinct().Count() != 3)
                    throw new InvalidDataException("reflection.screen.active_distinct_attachments");
                Rid[] framebuffers;
                if (active)
                {
                    Rid framebuffer = Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>(attachments.Select(value => value.ColorView))));
                    if (!rd.FramebufferIsValid(framebuffer)) throw new InvalidDataException("reflection.screen.actual_final_framebuffer");
                    framebuffers = [framebuffer];
                }
                else
                {
                    var singles = new Dictionary<Rid, Rid>(); framebuffers = new Rid[3];
                    for (int index = 0; index < attachments.Length; index++)
                    {
                        Rid view = attachments[index].ColorView;
                        if (!singles.TryGetValue(view, out Rid framebuffer))
                        {
                            framebuffer = Own(rd.FramebufferCreate([view]));
                            if (!rd.FramebufferIsValid(framebuffer)) throw new InvalidDataException("reflection.screen.actual_clear_framebuffer");
                            singles.Add(view, framebuffer);
                        }
                        framebuffers[index] = framebuffer;
                    }
                }
                return new ReflectionScreenTargetOwner(source, plan, actualTargetStorage,
                    plan.RawSliceDescriptorsPresent ? null : actualCurrentFramebuffer,
                    attachments, width, height, framebuffers, owned);
            }
            catch { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); throw; }
        });
    }

    private static void Validate(RenderingDevice rd, ReflectionFinalAttachment attachment, uint width, uint height, bool active)
    {
        if (!rd.TextureIsValid(attachment.ColorView) || !rd.TextureIsValid(attachment.OwnerTexture))
            throw new ObjectDisposedException(nameof(ReflectionScreenTargetOwner));
        using var format = rd.TextureGetFormat(attachment.ColorView);
        if (format.Width != width || format.Height != height || format.Samples != RenderingDevice.TextureSamples.Samples1 ||
            !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit) ||
            active && format.Format != RenderingDevice.DataFormat.B10G11R11UfloatPack32)
            throw new InvalidDataException("reflection.screen.current_attachment_format_extent");
    }

    public void RequireCurrent(RenderTargetFrame actualTargetStorage, RenderTargetFrame? actualCurrentFramebuffer)
    {
        ObjectDisposedException.ThrowIf(disposal is not null, this);
        if (!ReferenceEquals(actualTargetStorage, TargetStorage) ||
            !Plan.RawSliceDescriptorsPresent && !ReferenceEquals(actualCurrentFramebuffer, InheritedFramebuffer))
            throw new InvalidDataException("reflection.screen.retired_target_generation");
    }

    /// <summary>Records the three real count1 bind/clear operations. A null
    /// source descriptor therefore clears inherited slot0 three times.</summary>
    internal Task<int> ClearFallbackAsync()
    {
        ObjectDisposedException.ThrowIf(disposal is not null, this);
        if (Plan.Active) throw new InvalidOperationException("Active reflection targets are shader outputs, not fallback clears.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            for (int index = 0; index < Attachments.Count; index++)
            {
                Validate(rd, Attachments[index], Width, Height, false);
                if (!rd.FramebufferIsValid(framebuffers[index])) throw new ObjectDisposedException(nameof(ReflectionScreenTargetOwner));
                long list = rd.DrawListBegin(framebuffers[index], RenderingDevice.DrawFlags.ClearColorAll, [new Color(0, 0, 0, 0)]);
                rd.DrawListEnd();
            }
            return 3;
        });
    }

    public ValueTask DisposeAsync() => new(disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); return true; }));
}
