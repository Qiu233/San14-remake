namespace San14.Data;

/// <summary>Identity of the render-graph scope which owns this population.
/// Ordinary188 and the two special tail scopes are distinct consumers.</summary>
public readonly record struct G1EffectGpuDrawScope(uint SourceNode, uint View, uint Timing);

public enum G1EffectGpuGlobalTexture : uint
{
    MainDepthMirror = 1,
    AutoExposure = 2,
    ScatteringFogAcceleration = 3,
    SceneCapture = 4
}

/// <summary>A physical shader slot and its actual source. Uploaded textures
/// belong to their normal resource generation; graph images are resolved by
/// the native graph at the draw scope, rather than copied or fabricated here.</summary>
public sealed record G1EffectGpuSampler(ReadOnlyMemory<byte> Description);

public sealed record G1EffectGpuFrameBinding(uint StageCode, uint Slot,
    G1EffectGpuTexture? Texture, G1EffectGpuGlobalTexture? Global, G1EffectGpuSampler? Sampler = null)
{
    public static G1EffectGpuFrameBinding Uploaded(uint stage, uint slot, G1EffectGpuTexture texture) =>
        new(stage, slot, texture, null);
    public static G1EffectGpuFrameBinding Graph(uint stage, uint slot, G1EffectGpuGlobalTexture global) =>
        new(stage, slot, null, global);
    public static G1EffectGpuFrameBinding StandaloneSampler(uint stage, uint slot, G1EffectGpuSampler sampler) =>
        new(stage, slot, null, null, sampler);
}

/// <summary>Current callback inputs for one already sorted CPU packet. The
/// complete blend descriptor comes from the current renderer's recipe owner.
/// ExternalBindings maps real RDEF declarations to current graph providers.</summary>
public sealed record G1EffectGpuPacketInputs(int RuntimeBankIndex,
    ReadOnlyMemory<byte> FeatureRequest48, G1EffectGpuDrawScope Scope,
    G1EffectGpuConstantInputs Constants, ReadOnlyMemory<byte> BlendDescription,
    Func<G1EffectGpuExternalBinding, G1EffectGpuFrameBinding?>? ExternalBindings = null,
    G1EffectGpuTexture? SourceDefaultTexture = null);

public sealed record G1EffectGpuFrameSource(G1EffectGpuResources Resources,
    G1EffectDrawBatch Batch, Func<G1EffectDrawPacket, G1EffectGpuPacketInputs> Inputs,
    Func<LoadedMapEffectResources, G1EffectGpuResources>? ResourceResolver = null);

public sealed record G1EffectGpuFrameMissing(int GenericFileOffset, string Operation, string Reason);
public sealed record G1EffectGpuFrameBuild(G1EffectGpuPublication Publication,
    int DrawCount, IReadOnlyList<G1EffectGpuFrameMissing> Missing)
{
    public bool IsComplete => Missing.Count == 0;
}

internal sealed record G1EffectGpuPreparedDraw(G1EffectGpuResources Resources,
    G1EffectDrawPacket Packet, G1EffectGpuPipeline Pipeline, G1EffectGpuDrawScope Scope,
    int StreamOffset, IReadOnlyList<G1EffectGpuConstantBuffer> Constants,
    IReadOnlyList<G1EffectGpuFrameBinding> Textures,
    IReadOnlyList<G1EffectGpuFrameBinding> Samplers, ReadOnlyMemory<byte> Blend);

/// <summary>A normal-resource publication owner for a native render session.
/// The immutable upload changes only when a new selected pipeline or source
/// image is encountered. A frame contains actual sorted live streams only.</summary>
public sealed partial class G1EffectGpuFramePublisher
{
    private readonly G1EffectGpuPackageEncoder encoder;

    public G1EffectGpuFramePublisher(ulong resourceGeneration)
    {
        if (resourceGeneration == 0) throw new ArgumentOutOfRangeException(nameof(resourceGeneration));
        encoder = new(resourceGeneration);
    }

    public G1EffectGpuFrameBuild Build(ulong sequence, IReadOnlyList<G1EffectGpuFrameSource> sources)
        => BuildCore(sequence, sources, false);

    /// <summary>Prepares the actual direct generic's material and selected
    /// programs. No CPU particle or vertex bytes stand in for the GPU pool;
    /// the receiving allocation binds its own output and indirect arguments.</summary>
    public G1EffectGpuFrameBuild BuildDirect(ulong sequence, G1EffectGpuFrameSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Batch.DynamicStream.IsEmpty || source.Batch.Packets.Count != 1 ||
            source.Batch.Packets[0].DynamicCount != 0 || source.Batch.Packets[0].DynamicByteOffset != 0)
            throw new InvalidDataException("A direct material publication contains one actual generic and no CPU particle stream.");
        return BuildCore(sequence, [source], true);
    }

    private G1EffectGpuFrameBuild BuildCore(ulong sequence, IReadOnlyList<G1EffectGpuFrameSource> sources,
        bool direct)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var missing = new List<G1EffectGpuFrameMissing>();
        var draws = new List<G1EffectGpuPreparedDraw>();
        using var stream = new MemoryStream();
        foreach (var source in sources)
        {
            foreach (var item in source.Batch.Missing)
                missing.Add(new(item.GenericFileOffset, "DrawWriter", item.Reason));
            if (source.Batch.CapacityRejectedCount != 0)
                missing.Add(new(-1, "DrawCapacity", $"The actual draw writer rejected {source.Batch.CapacityRejectedCount} rows."));
            int streamBase = checked((int)stream.Position);
            stream.Write(source.Batch.DynamicStream.Span);
            foreach (var packet in source.Batch.Packets)
            {
                int before = missing.Count;
                var input = source.Inputs(packet);
                var resources = source.ResourceResolver?.Invoke(packet.Resources) ?? source.Resources;
                var selected = resources.PrepareDrawResources(input.RuntimeBankIndex,
                    input.FeatureRequest48, packet, input.SourceDefaultTexture);
                foreach (var item in selected.Pipeline.Missing.Concat(selected.Missing))
                    missing.Add(new(packet.GenericFileOffset, item.Operation, item.Reason));
                if (input.BlendDescription.Length != 264)
                    missing.Add(new(packet.GenericFileOffset, "Blend", "Current callback requires its complete 264-byte blend descriptor."));
                var constants = new List<G1EffectGpuConstantBuffer>();
                foreach (var stage in selected.Pipeline.Stages)
                {
                    var built = G1EffectGpuConstants.Build(stage, input.Constants);
                    constants.AddRange(built.Buffers);
                    foreach (var item in built.Missing)
                        missing.Add(new(packet.GenericFileOffset, "ConstantBuffer", $"{item.Buffer}.{item.Variable}: {item.Reason}"));
                }
                var textures = new List<G1EffectGpuFrameBinding>();
                var samplers = new List<G1EffectGpuFrameBinding>();
                foreach (var texture in selected.Textures)
                {
                    textures.Add(G1EffectGpuFrameBinding.Uploaded(texture.StageCode, texture.TextureRegister, texture.Texture));
                    if (texture.SamplerRegister is { } slot)
                        samplers.Add(G1EffectGpuFrameBinding.Uploaded(texture.StageCode, slot, texture.Texture));
                }
                foreach (var external in selected.ExternalBindings)
                {
                    if (external.Declaration.BindCount != 1 || external.Declaration.InputType is not (2 or 3))
                    {
                        missing.Add(new(packet.GenericFileOffset, "ExternalBinding",
                            $"Selected {external.Declaration.Name.Text} requires its actual array or typed resource consumer."));
                        continue;
                    }
                    var binding = input.ExternalBindings?.Invoke(external);
                    int bindingSources = binding is null ? 0 : (binding.Texture is null ? 0 : 1) +
                        (binding.Global is null ? 0 : 1) + (binding.Sampler is null ? 0 : 1);
                    if (binding is null || binding.StageCode != external.StageCode ||
                        binding.Slot != external.Declaration.BindPoint ||
                        bindingSources != 1 || external.Declaration.InputType != 3 && binding.Sampler is not null)
                    {
                        missing.Add(new(packet.GenericFileOffset, "ExternalBinding",
                            $"Selected stage {external.StageCode} requires current {external.Declaration.Name.Text} at slot {external.Declaration.BindPoint}."));
                        continue;
                    }
                    if (external.Declaration.InputType == 3 && binding.Global is { } global &&
                        global != G1EffectGpuGlobalTexture.AutoExposure)
                        missing.Add(new(packet.GenericFileOffset, "ExternalSampler",
                            $"Selected {external.Declaration.Name.Text} has no current native sampler provider."));
                    else if (external.Declaration.InputType == 3) samplers.Add(binding);
                    else textures.Add(binding);
                }
                if (before == missing.Count)
                    draws.Add(new(resources, packet, selected.Pipeline, input.Scope,
                        checked(streamBase + packet.DynamicByteOffset), constants, textures, samplers,
                        input.BlendDescription));
            }
        }
        var publication = encoder.Encode(sequence, stream.ToArray(), draws, direct);
        return new(publication, draws.Count, missing.AsReadOnly());
    }

    public G1EffectGpuPublication Clear(ulong sequence) =>
        G1EffectGpuPublication.Clear(encoder.CaptureResources(), sequence);
}
