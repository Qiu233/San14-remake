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
        IReadOnlyList<G1EffectGpuPreparedDraw> draws, bool direct = false, bool mainBankZeroOwner = false)
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
        int header = blob.Reserve(64), rows = blob.Reserve(checked(draws.Count * 108));
        int constantCount = draws.Sum(draw => draw.Constants.Count);
        int textureCount = draws.Sum(draw => draw.Textures.Count), samplerCount = draws.Sum(draw => draw.Samplers.Count);
        int constants = blob.Reserve(checked(constantCount * 16));
        int textureBindings = blob.Reserve(checked(textureCount * 16));
        int samplerBindings = blob.Reserve(checked(samplerCount * 16));
        int streamOffset = blob.Append(stream, 16);
        blob.Words(header, 0x31464543, 3, streamOffset, stream.Length, draws.Count, rows,
            constantCount, constants, textureCount, textureBindings, samplerCount, samplerBindings);
        // CEF3 extends the header, not the draw ABI or native API envelope.
        blob.Words(header + 48, mainBankZeroOwner ? 1 : 0, 0, 0, 0);
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
            blob.Words(rows + index * 108, Pipeline(draw.Pipeline), draw.Scope.SourceNode,
                draw.Scope.View, draw.Scope.Timing, packet.Configuration, packet.RenderingType,
                packet.HeaderFlags, packet.Flags, packet.AlphaReference, draw.StreamOffset,
                packet.DynamicStride, packet.DynamicCount, unchecked((uint)meshId), topology,
                indexCount, indexStart, unchecked((uint)baseVertex), firstConstant, draw.Constants.Count,
                firstTexture, draw.Textures.Count, firstSampler, draw.Samplers.Count, blendOffset, draw.Blend.Length);
            blob.Words(rows + index * 108 + 100, unchecked((uint)draw.Scope.SourceOwner),
                unchecked((uint)(draw.Scope.SourceOwner >> 32)));
        }
        return new(CaptureResources(), blob.ToArray(), G1EffectGpuUpdateKind.Replace, sequence);
    }

    private void Binding(Blob blob, int offset, G1EffectGpuFrameBinding binding, bool sampler)
    {
        int count = (binding.Texture is null ? 0 : 1) + (binding.Global is null ? 0 : 1) +
            (binding.Sampler is null ? 0 : 1);
        if (count != 1 || !sampler && binding.Sampler is not null)
            throw new InvalidDataException("Effect binding requires exactly one current source.");
        long id = sampler ? binding.Texture is { } wrapped ? Register(samplers, wrapped) :
            binding.Sampler is { } standalone ? Register(samplers, standalone) : 0 :
            binding.Texture is { } image ? Texture(image) : 0;
        if (binding.Global == G1EffectGpuGlobalTexture.PatternLighting)
            id = binding.ExternalOwnerObjectId ?? throw new InvalidDataException("Effect Pattern binding has no current outer Light owner.");
        else if (binding.ExternalOwnerObjectId.HasValue)
            throw new InvalidDataException("Effect external object identity belongs to the Pattern binding.");
        blob.Words(offset, binding.StageCode, binding.Slot, (uint?)binding.Global ?? 0, id);
    }

    internal G1EffectGpuResourceUpload CaptureResources()
    {
        if (!dirty && upload is not null) return upload;
        using var blob = new Blob(MeasureResourceBytes());
        int header = blob.Reserve(48);
        int pipelineRows = blob.Reserve(checked(pipelines.Count * 32));
        int meshRows = blob.Reserve(checked(meshes.Count * 28));
        int textureRows = blob.Reserve(checked(textures.Count * 40));
        int samplerRows = blob.Reserve(checked(samplers.Count * 12));
        bool hasTextureArrayViews = textures.Keys.Any(texture => texture.ViewDimension == 5);
        blob.Words(header, 0x31524543, hasTextureArrayViews ? 2 : 1, pipelines.Count, pipelineRows, meshes.Count, meshRows,
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
            if (texture.Source is null || texture.Width <= 0 || texture.Height <= 0 ||
                texture.ArraySize <= 0 || texture.Mips.Count == 0 || texture.Mips.Count % texture.ArraySize != 0 ||
                texture.ViewDimension is not (4 or 5) || texture.ViewDimension == 4 && texture.ArraySize != 1)
                throw new InvalidDataException("Effect texture requires complete source dimensions and subresources.");
            int mipCount = texture.MipCount;
            if (mipCount > 1 + System.Numerics.BitOperations.Log2((uint)Math.Max(texture.Width, texture.Height)))
                throw new InvalidDataException("Effect texture mip chain exceeds its dimensions.");
            int subresources = blob.Reserve(checked(texture.Mips.Count * 16));
            for (int index = 0; index < texture.Mips.Count; index++)
            {
                var mip = texture.Mips[index];
                int level = index % mipCount, slice = index / mipCount;
                if (mip.Level != level || mip.ArraySlice != slice ||
                    mip.Width != Math.Max(1, texture.Width >> level) ||
                    mip.Height != Math.Max(1, texture.Height >> level) ||
                    mip.RowPitch <= 0 || mip.SlicePitch < mip.RowPitch || mip.Bytes.Length != mip.SlicePitch)
                    throw new InvalidDataException("Effect texture subresources must retain actual D3D slice and mip order and pitches.");
                int bytes = blob.Append(mip.Bytes.Span);
                blob.Words(subresources + index * 16, bytes, mip.Bytes.Length, mip.RowPitch, mip.SlicePitch);
            }
            blob.Words(textureRows + id * 40, id, texture.Width, texture.Height, mipCount,
                texture.ResourceFormat, texture.ViewFormat, subresources, texture.Mips.Count,
                hasTextureArrayViews ? texture.ArraySize : 0,
                hasTextureArrayViews ? texture.ViewDimension : 0);
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
        upload = new(generation, blob.TakeArray()); dirty = false; return upload;
    }

    // Compute storage from the same selected resources and CER row widths.
    // Only lengths are read; no encoded payload is copied during this pass.
    private int MeasureResourceBytes()
    {
        int size = 0;
        void Reserve(int length) => size = checked(((size + 3) & ~3) + length);
        Reserve(48);
        Reserve(checked(pipelines.Count * 32));
        Reserve(checked(meshes.Count * 28));
        Reserve(checked(textures.Count * 40));
        Reserve(checked(samplers.Count * 12));
        foreach (var pipeline in pipelines.Keys)
        {
            Reserve(checked(pipeline.Stages.Count * 12));
            foreach (var stage in pipeline.Stages) Reserve(stage.Bytecode.Length);
            Reserve(checked(pipeline.InputLayout.Count * 56));
        }
        foreach (var mesh in meshes.Keys)
        {
            Reserve(mesh.VertexBytes.Length);
            Reserve(mesh.IndexBytes.Length);
        }
        foreach (var texture in textures.Keys)
        {
            Reserve(checked(texture.Mips.Count * 16));
            foreach (var mip in texture.Mips) Reserve(mip.Bytes.Length);
        }
        foreach (var sampler in samplers.Keys) Reserve(52);
        return size;
    }

    private sealed class Blob : IDisposable
    {
        private readonly MemoryStream stream;
        internal Blob() => stream = new();
        internal Blob(int capacity) => stream = new(capacity);
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
        // Final write has completed; this local stream never reuses its storage.
        // Keep the payload exact if a later format change does not fill its estimate.
        internal byte[] TakeArray() => stream.Length == stream.Capacity ? stream.GetBuffer() : stream.ToArray();
        public void Dispose() => stream.Dispose();
    }
}
