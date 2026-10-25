using System.Runtime.CompilerServices;
using San14.Data;

public sealed record G1EffectMapDirectDrawBuild(int DrawCount,
    IReadOnlyList<G1EffectGpuFrameMissing> Missing)
{
    public bool IsComplete => Missing.Count == 0;
}

/// <summary>Connects the instance's actual Direct storage to its selected
/// normal material at the native188 scope. Allocation and indirect counts
/// remain device-owned; this owner never creates CPU particles for Direct.</summary>
public sealed class G1EffectMapDirectDraws
{
    private sealed record Storage(G1EffectDirectComputeStorage Value);
    private readonly ConditionalWeakTable<G1EffectGenericRuntime, Storage> storages = new();
    private readonly G1EffectDirectComputeFactory factory;

    public G1EffectMapDirectDraws(IG1EffectDirectComputeDevice device,
        Func<G1EffectDirectComputeView> currentView)
    {
        factory = new(device, currentView, (generic, storage) => storages.Add(generic, new(storage)));
    }

    public IG1EffectDirectParticleStorage? CreateStorage(G1EffectGenericRuntime generic) =>
        factory.CreateStorage(generic);

    /// <summary>Call in actual instance/system/generic traversal order after
    /// advancing the instance, before the same native Main frame. Ordinal is
    /// shared across the map's instances, independent of CPU depth buckets.</summary>
    public G1EffectMapDirectDrawBuild Prepare(ulong sequence, LoadedMapEffectResources resources,
        G1EffectInstance instance, G1EffectMapInputFrame frame,
        G1EffectGpuFramePublisher publisher, G1EffectGpuResources gpuResources,
        ref long submissionOrdinal)
    {
        var missing = new List<G1EffectGpuFrameMissing>();
        int drawCount = 0;
        foreach (var generic in instance.Generics)
        {
            if (!generic.Declaration.UsesDirectBirth) continue;
            if (generic.Record.Type == 10)
            {
                // Its linked GPU state is real, but sprite submission is not
                // the source light consumer. Keep the presentation request
                // pending until that scene-light owner consumes the state.
                missing.Add(new(generic.Record.FileOffset, "DirectLinkedLighting",
                    "Linked Direct GPU state has no current scene-light consumer."));
                continue;
            }
            long ordinal = submissionOrdinal++;
            if (!storages.TryGetValue(generic, out var storage) || !storage.Value.IsPrepared ||
                storage.Value.Allocation is not IG1EffectDirectDrawAllocation allocation)
            {
                missing.Add(new(generic.Record.FileOffset, "DirectDraw", "The actual generic has no prepared native draw allocation."));
                continue;
            }
            if (generic.ModuleEmissionScale is not { } opacity)
            {
                allocation.SetDraw(null, ordinal);
                missing.Add(new(generic.Record.FileOffset, "DirectOpacity", "The current module opacity owner is unavailable."));
                continue;
            }
            if (!G1EffectDirectDrawSource.IsAdmitted(generic.CurrentGenericBytes, opacity))
            { allocation.SetDraw(null, ordinal); continue; }
            var packet = G1EffectDirectDrawSource.ReadMetadata(resources, generic.Declaration,
                generic.CurrentGenericBytes, ordinal);
            var batch = new G1EffectDrawBatch(frame.CpuCamera, ReadOnlyMemory<byte>.Empty,
                [packet], Array.Empty<G1EffectDrawMissing>(), 0, 0);
            var built = publisher.BuildDirect(sequence, new(gpuResources, batch, frame.PacketInputs));
            if (!built.IsComplete)
            {
                allocation.SetDraw(null, ordinal);
                missing.AddRange(built.Missing);
                continue;
            }
            allocation.SetDraw(built.Publication, ordinal);
            drawCount += built.DrawCount;
        }
        return new(drawCount, missing.AsReadOnly());
    }
}
