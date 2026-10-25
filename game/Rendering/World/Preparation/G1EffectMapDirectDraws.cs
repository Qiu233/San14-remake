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
    private readonly IG1EffectDirectLightingDevice? lightingDevice;

    public G1EffectMapDirectDraws(IG1EffectDirectComputeDevice device,
        Func<G1EffectDirectComputeView> currentView)
    {
        lightingDevice=device as IG1EffectDirectLightingDevice;
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
        var admitted=instance.EnumerateCurrentGenerics().ToHashSet();
        foreach (var generic in instance.Generics)
        {
            if (!generic.Declaration.UsesDirectBirth) continue;
            if (generic.Record.Type == 10)
            {
                long lightOrdinal=submissionOrdinal++;
                var main=G1EffectDirectLightingSource.FindMain(generic,instance.Generics);
                if(!admitted.Contains(generic))
                {
                    if(storages.TryGetValue(main,out var inactive)&&inactive.Value.IsPrepared&&
                        inactive.Value.Allocation is IG1EffectDirectLightingAllocation oldLight)
                        oldLight.SetLighting(null,lightOrdinal);
                    continue;
                }
                if(!storages.TryGetValue(main,out var mainStorage)||!mainStorage.Value.IsPrepared||
                    mainStorage.Value.Allocation is not IG1EffectDirectLightingAllocation lightAllocation)
                {
                    missing.Add(new(generic.Record.FileOffset,"DirectLinkedLighting","The linked point producer has no actual prepared allocation."));
                }
                else if(lightingDevice is null||frame.LightingCluster is not { IsReady:true } cluster)
                {
                    lightAllocation.SetLighting(null,lightOrdinal);
                    missing.Add(new(generic.Record.FileOffset,"DirectLinkedLighting","The current normal node71 cluster owner is incomplete."));
                }
                else
                {
                    lightingDevice.SetLightingCluster(cluster,frame.LightingIndexFormat);
                    lightAllocation.SetLighting(G1EffectDirectLightingSource.Read(main,generic,mainStorage.Value,
                        instance.AutoHdrScaleExposure),lightOrdinal);
                }
                continue;
            }
            long ordinal = submissionOrdinal++;
            if(!admitted.Contains(generic))
            {
                if(storages.TryGetValue(generic,out var inactive)&&inactive.Value.IsPrepared&&
                    inactive.Value.Allocation is IG1EffectDirectDrawAllocation oldDraw)oldDraw.SetDraw(null,ordinal);
                continue;
            }
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
