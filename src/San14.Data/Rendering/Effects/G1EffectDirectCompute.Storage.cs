namespace San14.Data;

/// <summary>The actual renderer allocation. A successful implementation owns compiled compute
/// programs and device buffers, submits ordered dispatches and obtains GPU liveness feedback.
/// An upload receipt alone cannot implement this interface as prepared storage.</summary>
public interface IG1EffectDirectComputeAllocation : IDisposable
{
    bool IsPrepared { get; }
    bool? HasLiveParticles { get; }
    bool Submit(G1EffectDirectCommands commands);
}

public interface IG1EffectDirectComputeDevice
{
    IG1EffectDirectComputeAllocation? Create(G1EffectDirectComputePackage package);
}

/// <summary>Real object-buffer coordinates and current normal data uploads supplied by the
/// module owner. Source/definition/generic references must share this coordinate system.</summary>
public sealed record G1EffectDirectBirthBinding(int SourceByteOffset, int DefinitionByteOffset,
    int GenericByteOffset, IReadOnlyList<G1EffectDirectUpload> Uploads);

public sealed record G1EffectDirectFrameBinding(int GenericByteOffset,
    IReadOnlyList<G1EffectDirectUpload> Uploads,
    IReadOnlyList<G1EffectDirectDispatch> AfterControllers,
    IReadOnlyList<G1EffectDirectConstantBinding> ControllerConstants);

/// <summary>Source-ordered integer birth accumulation and device submission. The latest emitter
/// and definition replace the pending references while counts accumulate, as in the original
/// direct manager. Particle initialization and advancement execute exclusively on the GPU.</summary>
public sealed class G1EffectDirectComputeStorage : IG1EffectDirectParticleStorage
{
    private readonly G1EffectDirectComputePackage package;
    private readonly G1EffectRandom random;
    private readonly IG1EffectDirectComputeAllocation allocation;
    private readonly Func<G1EffectDirectBirthCommand, G1EffectDirectBirthBinding> bindBirth;
    private readonly Func<float, G1EffectDirectFrameBinding> bindFrame;
    private G1EffectDirectBirthCommand? pending;
    private int pendingCount, startIndex;
    private readonly HashSet<(int, ReadOnlyMemory<byte>)> validatedBirths = [];
    private bool disposed;

    private G1EffectDirectComputeStorage(G1EffectDirectComputePackage package, G1EffectRandom random,
        IG1EffectDirectComputeAllocation allocation,
        Func<G1EffectDirectBirthCommand, G1EffectDirectBirthBinding> bindBirth,
        Func<float, G1EffectDirectFrameBinding> bindFrame)
    { this.package = package; this.random = random; this.allocation = allocation; this.bindBirth = bindBirth; this.bindFrame = bindFrame; }

    public bool IsPrepared => !disposed && package.MissingKernels.Count == 0 && allocation.IsPrepared;
    public G1EffectDirectComputePackage Package => package;
    public IG1EffectDirectComputeAllocation Allocation => allocation;
    // Unknown GPU feedback remains live; it cannot complete an effect prematurely.
    public bool IsAlive => !disposed && (pendingCount != 0 || allocation.HasLiveParticles != false);

    public static G1EffectDirectComputeStorage? Create(G1EffectDirectComputePackage package,
        G1EffectRandom sharedRandom, IG1EffectDirectComputeDevice device,
        Func<G1EffectDirectBirthCommand, G1EffectDirectBirthBinding> bindBirth,
        Func<float, G1EffectDirectFrameBinding> bindFrame)
    {
        ArgumentNullException.ThrowIfNull(package); ArgumentNullException.ThrowIfNull(sharedRandom);
        ArgumentNullException.ThrowIfNull(device); ArgumentNullException.ThrowIfNull(bindBirth); ArgumentNullException.ThrowIfNull(bindFrame);
        if (package.MissingKernels.Count != 0) return null;
        if (package.Declaration.Record.Type is not (0 or 1)) return null;
        var actual = device.Create(package);
        if (actual is null) return null;
        try
        {
            if (!actual.IsPrepared || !actual.Submit(new([], package.InitializeAll()))) { actual.Dispose(); return null; }
            return new(package, sharedRandom, actual, bindBirth, bindFrame);
        }
        catch { actual.Dispose(); throw; }
    }

    public void QueueBirth(G1EffectDirectBirthCommand command)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(command);
        if (!IsPrepared) throw new InvalidOperationException("Direct GPU allocation is not prepared.");
        if (command.ScheduledAmount.HasValue)
            throw new NotSupportedException("Fractional screen-space direct birth requires the screen/depth compute path.");
        if (command.Generic.FileOffset != package.Declaration.Record.FileOffset)
            throw new ArgumentException("Direct birth belongs to a different generic.", nameof(command));
        if (command.ScheduledCount < 0) throw new ArgumentOutOfRangeException(nameof(command));
        var identity = (command.Definition.Record.FileOffset, command.SourceContext);
        if (!validatedBirths.Contains(identity))
        { G1EffectDirectValidation.BirthCommand(command); validatedBirths.Add(identity); }
        pendingCount = checked(pendingCount + command.ScheduledCount);
        pending = command; // Actual live owner memory is read when the stage is submitted.
    }

    public void Advance(float seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsPrepared) throw new InvalidOperationException("Direct GPU allocation is not prepared.");
        if (!float.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var frame = bindFrame(seconds);
        var uploads = new List<G1EffectDirectUpload>(frame.Uploads);
        uploads.Add(new(G1EffectDirectComputePackage.LiveCountBuffer, 0, new byte[4]));
        var dispatches = new List<G1EffectDirectDispatch>();
        int count = pendingCount;
        G1EffectDirectBirthBinding? birthBinding = null;
        if (pending is { } command && count > 0)
        {
            var binding = bindBirth(command);
            birthBinding = binding;
            uploads.AddRange(binding.Uploads);
            uint seed = random.NextUInt() >> 16;
            dispatches.Add(package.Birth(count, startIndex, binding.SourceByteOffset,
                binding.DefinitionByteOffset, binding.GenericByteOffset, seed));
        }
        dispatches.Add(package.Update(seconds, frame.GenericByteOffset) with { AdditionalConstants = frame.ControllerConstants });
        bool linkedUpdated = false;
        void AppendLinked()
        {
            if (linkedUpdated || package.LinkedDeclaration is not { } linked) return;
            int linkedOffset = checked(frame.GenericByteOffset + linked.Record.FileOffset - package.Declaration.Record.FileOffset);
            if (birthBinding is { } binding && count > 0)
                dispatches.Add(package.LinkedBirth(count, startIndex, binding.SourceByteOffset,
                    binding.DefinitionByteOffset, binding.GenericByteOffset, linkedOffset, random.NextUInt() >> 16));
            dispatches.Add(package.LinkedUpdate(seconds, frame.GenericByteOffset, linkedOffset));
            linkedUpdated = true;
        }
        foreach (var stage in frame.AfterControllers)
        {
            if (stage.Kernel == G1EffectDirectKernel.ResetDraw) AppendLinked();
            dispatches.Add(stage);
        }
        AppendLinked();
        if (!allocation.Submit(new(uploads.AsReadOnly(), dispatches.AsReadOnly())))
            throw new InvalidOperationException("The GPU rejected the direct particle command batch.");
        startIndex = (int)(((long)startIndex + count) % package.Capacity);
        pendingCount = 0; pending = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; pendingCount = 0; pending = null; allocation.Dispose();
    }
}
