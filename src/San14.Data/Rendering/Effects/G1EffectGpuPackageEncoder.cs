using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

/// <summary>Versioned in-memory ABI for the live native effect consumer. IDs
/// are local to this owner and remain stable as its selected resource set grows.</summary>
internal sealed class G1EffectGpuPackageEncoder(ulong generation)
{
    private readonly Dictionary<G1EffectGpuPipeline, int> pipelines = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<G1EffectGpuMesh, int> meshes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<G1EffectGpuTexture, int> textures = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, int> samplers = new(ReferenceEqualityComparer.Instance);
    private G1EffectGpuResourceUpload? upload;
    private bool dirty = true;

    private int Pipeline(G1EffectGpuPipeline value) => Register(pipelines, value);
    private int Mesh(G1EffectGpuMesh value) => Register(meshes, value);
    private int Texture(G1EffectGpuTexture value) => Register(textures, value);
    private int Register<T>(Dictionary<T, int> values, T value) where T : class
    {
        if (values.TryGetValue(value, out int found)) return found;
        int id = values.Count; values.Add(value, id); dirty = true; return id;
    }

    internal G1EffectGpuPublication Encode(ulong sequence, byte[] stream,
        IReadOnlyList<G1EffectGpuPreparedDraw> draws, bool direct = false)
    {
        foreach (var draw in draws)
        {
            Pipeline(draw.Pipeline);
            if (draw.Packet.MeshSubdrawIndex is { } subdraw)
                Mesh(draw.Resources.Meshes[draw.Resources.Subdraws[subdraw].MeshIndex]);
            foreach (var binding in draw.Textures.Concat(draw.Samplers))
                if (binding.Texture is { } texture) Texture(texture);
            foreach (var binding in draw.Samplers)
                if (binding.Texture is { } texture) Register(samplers, texture);
                else if (binding.Sampler is { } sampler) Register(samplers, sampler);
        }
        using var blob = new Blob();
        int header = blob.Reserve(48), rows = blob.Reserve(checked(draws.Count * 100));
        int constantCount = draws.Sum(draw => draw.Constants.Count);
        int textureCount = draws.Sum(draw => draw.Textures.Count), samplerCount = draws.Sum(draw => draw.Samplers.Count);
        int constants = blob.Reserve(checked(constantCount * 16));
        int textureBindings = blob.Reserve(checked(textureCount * 16));
        int samplerBindings = blob.Reserve(checked(samplerCount * 16));
        int streamOffset = blob.Append(stream, 16);
        blob.Words(header, 0x31464543, 1, streamOffset, stream.Length, draws.Count, rows,
            constantCount, constants, textureCount, textureBindings, samplerCount, samplerBindings);
        int ci = 0, ti = 0, si = 0;
        for (int index = 0; index < draws.Count; index++)
        {
            var draw = draws[index]; var packet = draw.Packet;
            if (packet.DynamicStride <= 0 || packet.DynamicCount < 0 || !direct && (draw.StreamOffset < 0 ||
                (long)draw.StreamOffset + (long)packet.DynamicStride * packet.DynamicCount > stream.Length)
                || direct && (stream.Length != 0 || packet.DynamicCount != 0 || draw.StreamOffset != 0))
                throw new InvalidDataException("Effect live draw escapes its actual stream.");
            int firstConstant = ci, firstTexture = ti, firstSampler = si;
            foreach (var constant in draw.Constants)
            {
                int offset = blob.Append(constant.Bytes.Span, 16);
                blob.Words(constants + ci++ * 16, constant.StageCode, constant.Slot, offset, constant.Bytes.Length);
            }
            foreach (var binding in draw.Textures) Binding(blob, textureBindings + ti++ * 16, binding, false);
            foreach (var binding in draw.Samplers) Binding(blob, samplerBindings + si++ * 16, binding, true);
            int meshId = -1, topology = direct ? 4 : 5, indexCount = 0, indexStart = 0, baseVertex = 0;
            if (packet.MeshSubdrawIndex is { } subdrawIndex)
            {
                var subdraw = draw.Resources.Subdraws[subdrawIndex];
                meshId = Mesh(draw.Resources.Meshes[subdraw.MeshIndex]);
                topology = subdraw.D3D11Topology; indexCount = subdraw.IndexCount;
                indexStart = subdraw.IndexStart; baseVertex = subdraw.BaseVertex;
            }
            int blendOffset = blob.Append(draw.Blend.Span);
            blob.Words(rows + index * 100, Pipeline(draw.Pipeline), draw.Scope.SourceNode,
                draw.Scope.View, draw.Scope.Timing, packet.Configuration, packet.RenderingType,
                packet.HeaderFlags, packet.Flags, packet.AlphaReference, draw.StreamOffset,
                packet.DynamicStride, packet.DynamicCount, unchecked((uint)meshId), topology,
                indexCount, indexStart, unchecked((uint)baseVertex), firstConstant, draw.Constants.Count,
                firstTexture, draw.Textures.Count, firstSampler, draw.Samplers.Count, blendOffset, draw.Blend.Length);
        }
        return new(CaptureResources(), blob.ToArray(), G1EffectGpuUpdateKind.Replace, sequence);
    }

    private void Binding(Blob blob, int offset, G1EffectGpuFrameBinding binding, bool sampler)
    {
        int count = (binding.Texture is null ? 0 : 1) + (binding.Global is null ? 0 : 1) +
            (binding.Sampler is null ? 0 : 1);
        if (count != 1 || !sampler && binding.Sampler is not null)
            throw new InvalidDataException("Effect binding requires exactly one current source.");
        int id = sampler ? binding.Texture is { } wrapped ? Register(samplers, wrapped) :
            binding.Sampler is { } standalone ? Register(samplers, standalone) : 0 :
            binding.Texture is { } image ? Texture(image) : 0;
        blob.Words(offset, binding.StageCode, binding.Slot, (uint?)binding.Global ?? 0,
            id);
    }

    internal G1EffectGpuResourceUpload CaptureResources()
    {
        if (!dirty && upload is not null) return upload;
        using var blob = new Blob();
        int header = blob.Reserve(48);
        int pipelineRows = blob.Reserve(checked(pipelines.Count * 32));
        int meshRows = blob.Reserve(checked(meshes.Count * 28));
        int textureRows = blob.Reserve(checked(textures.Count * 40));
        int samplerRows = blob.Reserve(checked(samplers.Count * 12));
        blob.Words(header, 0x31524543, 1, pipelines.Count, pipelineRows, meshes.Count, meshRows,
            textures.Count, textureRows, samplers.Count, samplerRows, 0, 0);
        foreach (var (pipeline, id) in pipelines)
        {
            int stages = blob.Reserve(checked(pipeline.Stages.Count * 12));
            for (int index = 0; index < pipeline.Stages.Count; index++)
            {
                var stage = pipeline.Stages[index]; int code = blob.Append(stage.Bytecode.Span);
                blob.Words(stages + index * 12, stage.StageCode, code, stage.Bytecode.Length);
            }
            int elements = blob.Reserve(checked(pipeline.InputLayout.Count * 56));
            for (int index = 0; index < pipeline.InputLayout.Count; index++)
            {
                var element = pipeline.InputLayout[index];
                byte[] semantic = Encoding.ASCII.GetBytes(element.SemanticName);
                if (semantic.Length >= 32 || semantic.Any(value => value == 0))
                    throw new InvalidDataException("Effect input semantic does not fit the native declaration.");
                blob.Put(elements + index * 56, semantic);
                blob.Words(elements + index * 56 + 32, element.SemanticIndex, element.DxgiFormat,
                    element.InputSlot, element.ByteOffset, element.InputClassification, element.InstanceStepRate);
            }
            blob.Words(pipelineRows + id * 32, id, pipeline.Pass, pipeline.RuntimeBankIndex,
                stages, pipeline.Stages.Count, elements, pipeline.InputLayout.Count, 0);
        }
        foreach (var (mesh, id) in meshes)
        {
            int vertex = blob.Append(mesh.VertexBytes.Span), index = blob.Append(mesh.IndexBytes.Span);
            blob.Words(meshRows + id * 28, id, mesh.VertexStride, mesh.IndexFormat,
                vertex, mesh.VertexBytes.Length, index, mesh.IndexBytes.Length);
        }
        foreach (var (texture, id) in textures)
        {
            int subresources = blob.Reserve(checked(texture.Mips.Count * 16));
            for (int index = 0; index < texture.Mips.Count; index++)
            {
                var mip = texture.Mips[index]; int bytes = blob.Append(mip.Bytes.Span);
                blob.Words(subresources + index * 16, bytes, mip.Bytes.Length, mip.RowPitch, mip.SlicePitch);
            }
            blob.Words(textureRows + id * 40, id, texture.Width, texture.Height, texture.Mips.Count,
                texture.ResourceFormat, texture.ViewFormat, subresources, texture.Mips.Count, 0, 0);
        }
        foreach (var (source, id) in samplers)
        {
            ReadOnlyMemory<byte> description = source switch
            {
                G1EffectGpuTexture texture => texture.SamplerDescription,
                G1EffectGpuSampler sampler => sampler.Description,
                _ => throw new InvalidOperationException("Effect sampler owner has an unsupported type.")
            };
            if (description.Length != 52) throw new InvalidDataException("Effect sampler requires its complete source descriptor.");
            int offset = blob.Append(description.Span);
            blob.Words(samplerRows + id * 12, id, offset, description.Length);
        }
        upload = new(generation, blob.ToArray()); dirty = false; return upload;
    }

    private sealed class Blob : IDisposable
    {
        private readonly MemoryStream stream = new();
        internal int Reserve(int length, int alignment = 4)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            long start = checked((stream.Length + alignment - 1) & ~(alignment - 1L));
            stream.SetLength(checked(start + length)); stream.Position = stream.Length;
            return checked((int)start);
        }
        internal int Append(ReadOnlySpan<byte> value, int alignment = 4)
        { int offset = Reserve(value.Length, alignment); Put(offset, value); return offset; }
        internal void Put(int offset, ReadOnlySpan<byte> value) => value.CopyTo(stream.GetBuffer().AsSpan(offset, value.Length));
        internal void Words(int offset, params long[] words)
        {
            foreach (long word in words)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(stream.GetBuffer().AsSpan(offset, 4), checked((uint)word));
                offset += 4;
            }
        }
        internal byte[] ToArray() => stream.ToArray();
        public void Dispose() => stream.Dispose();
    }
}
