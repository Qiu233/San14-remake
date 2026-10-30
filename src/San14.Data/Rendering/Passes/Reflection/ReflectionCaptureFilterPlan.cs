namespace San14.Data;

/// <summary>The actual wrapper configuration copied by FD8300. Fresh Field
/// construction supplies signed count4 and surface scale32; neither is ready.</summary>
public readonly record struct ReflectionCaptureFilterConfiguration(sbyte PropagationPasses, uint SurfaceScale)
{
    public static ReflectionCaptureFilterConfiguration FreshField => new(4, 32);
}

public enum ReflectionCaptureFilterPhase { Idle, DirectLighting, Propagation, Publication }
public enum ReflectionCaptureFilterStepKind { DirectRaster, IntegrateDirect, PropagationRaster, IntegratePropagation, TransferPropagation, PublishRadiance, PublishCoefficients, RadianceMip }
public sealed record ReflectionCaptureFilterStep(ReflectionCaptureFilterStepKind Kind,
    uint Pass, uint Layer, uint Mip = 0, int Iteration = 0);

/// <summary>One wrapper's scheduling state, separate from Field capture cursor
/// and shader-global indices. Changes are committed after real submissions.</summary>
public sealed class ReflectionCaptureFilterProgress
{
    public uint Mode { get; private set; }
    public ReflectionCaptureFilterPhase Phase { get; private set; }
    public sbyte LayerCursor { get; private set; }
    public sbyte PropagationIteration { get; private set; }
    public bool Parity { get; private set; }

    public void SetMode(uint mode)
    {
        if (mode == Mode) return;
        Mode = mode;
        if (mode == 0) { Phase = 0; LayerCursor = 0; PropagationIteration = 0; Parity = false; }
    }

    public IReadOnlyList<ReflectionCaptureFilterStep> PrepareDirect(uint actualLayerCount)
    {
        if (Mode is not (0 or 1)) return [];
        if (Phase == ReflectionCaptureFilterPhase.Idle) Phase = ReflectionCaptureFilterPhase.DirectLighting;
        if (Phase != ReflectionCaptureFilterPhase.DirectLighting) return [];
        uint layer = CurrentLayer(actualLayerCount);
        return [new(ReflectionCaptureFilterStepKind.DirectRaster, 6, layer),
            new(ReflectionCaptureFilterStepKind.IntegrateDirect, 2, layer)];
    }

    public void CommitDirect(uint actualLayerCount)
    {
        uint layer = CurrentLayer(actualLayerCount);
        LayerCursor = layer < actualLayerCount - 1 ? unchecked((sbyte)(layer + 1)) : (sbyte)0;
        if (LayerCursor == 0) Phase = ReflectionCaptureFilterPhase.Propagation;
    }

    public IReadOnlyList<ReflectionCaptureFilterStep> PreparePropagation(uint actualLayerCount,
        ReflectionCaptureFilterConfiguration configuration)
    {
        if (Mode is not (0 or 1) || Phase != ReflectionCaptureFilterPhase.Propagation) return [];
        if (configuration.PropagationPasses == 0) return [];
        uint layer = CurrentLayer(actualLayerCount);
        var steps = new List<ReflectionCaptureFilterStep> {
            new(ReflectionCaptureFilterStepKind.PropagationRaster, 7, layer, Iteration: PropagationIteration + 1),
            new(ReflectionCaptureFilterStepKind.IntegratePropagation, 2, layer) };
        if (layer == actualLayerCount - 1) steps.Add(new(ReflectionCaptureFilterStepKind.TransferPropagation, 8, layer));
        return steps;
    }

    public void CommitPropagation(uint actualLayerCount, ReflectionCaptureFilterConfiguration configuration)
    {
        if (configuration.PropagationPasses == 0) { Phase = ReflectionCaptureFilterPhase.Publication; return; }
        uint layer = CurrentLayer(actualLayerCount);
        LayerCursor = layer < actualLayerCount - 1 ? unchecked((sbyte)(layer + 1)) : (sbyte)0;
        if (LayerCursor == 0) PropagationIteration = unchecked((sbyte)(PropagationIteration + 1));
        if (configuration.PropagationPasses <= PropagationIteration)
        { Phase = ReflectionCaptureFilterPhase.Publication; PropagationIteration = 0; }
    }

    public IReadOnlyList<ReflectionCaptureFilterStep> PreparePublication(uint actualLayerCount, uint actualMipCount)
    {
        if (Mode is not (0 or 1) || Phase != ReflectionCaptureFilterPhase.Publication) return [];
        var result = new List<ReflectionCaptureFilterStep> {
            new(ReflectionCaptureFilterStepKind.PublishRadiance, 9, 0),
            new(ReflectionCaptureFilterStepKind.PublishCoefficients, 8, 0) };
        for (uint layer = 0; layer < actualLayerCount; layer++)
            for (uint mip = 1; mip < actualMipCount; mip++) result.Add(new(ReflectionCaptureFilterStepKind.RadianceMip, 11, layer, mip));
        return result;
    }

    public void CommitPublication()
    { LayerCursor = 0; Phase = ReflectionCaptureFilterPhase.Idle; Parity = !Parity; }

    private uint CurrentLayer(uint count)
    {
        if (count == 0) throw new InvalidDataException("reflection.filter.layer_extent");
        uint current = unchecked((uint)(int)LayerCursor);
        return current <= count - 1 ? current : count - 1;
    }
}
