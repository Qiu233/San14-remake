using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ModelQueueStateSource(LoadedRenderGraph Graph, CompiledGraphNode Node,
    ushort Flag, uint Flag2, byte RenderType, uint ViewObjectHash, byte ViewId,
    byte ShaderStateOffset, bool ClockwiseRendering, IReadOnlyList<TaskGraphArgument> Arguments)
{
    /// <summary>E80C18 -> E80E12 -> E6C74D -> EA7641 -> EA7919 ->
    /// D47643 -> D363EB. The original queue Flag bit3 is inverted by the
    /// draw command producer before writing frontend +1BE.</summary>
    public bool BackendAlphaEnabled => (Flag & 8) == 0;
}

public sealed record ModelAlphaState(bool Enabled, uint CompareCode, uint Reference);
public sealed record ModelAlphaTransition(ModelAlphaState Next, uint? CommandWord);
public sealed record ModelDrawMaterialCache(string? MaterialToken, string? ModifierToken,
    uint CachedMaterialFlags);
public sealed record ModelDrawMaterialTransition(bool MaterialChanged,
    ModelDrawMaterialCache Next, ModelSpecularPreparation SpecularPreparation,
    ModelAlphaTransition Alpha);
public sealed record ModelQueueDepthState(ulong PackedSourceState, bool DepthTest,
    bool DepthWrite, uint CompareCode, bool StencilEnabled);
public sealed record ModelGroupRasterSource(LoadedModelShaderBindings Model,
    G1DrawShaderIdentifier Draw, byte GroupRasterCode, ulong PackedType10,
    uint NativeFillMode, uint NativeCullMode, bool FrontCounterClockwise);

/// <summary>Finite original FC64 model queue and frontend state operations.
/// Queue execution eligibility, the type10 raster payload, and previous state
/// are separate caller inputs. No source shader name supplies alpha enable.</summary>
public static class ModelRendererStateSourceReader
{
    /// <summary>ABB5C7 copies original group raw+18 to native group+8. F57C70
    /// constructs a one-word type10 only when that source byte is nonzero:
    /// ((ulong)byte&lt;&lt;4)|3, supplied to primitive+60. D71710 sends fill and
    /// cull to the original frontend. Native94E300 chooses CullMode from the
    /// exact table {1,3,2} and writes FrontCounterClockwise=1 literally.
    /// Null means the primitive has no group raster override and must retain
    /// its separately sourced queue/context raster. Queue overrides and the
    /// draw's selected type10 ordinal remain separate preparation inputs.</summary>
    public static ModelGroupRasterSource? ReadGroupRaster(LoadedModelShaderBindings model, int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.ModelObject.Kind != DokObjectKind.Inline || model.ModelObject.ClassId != ModelShaderResources.ModelClass)
            throw new InvalidDataException("Group raster requires an inline model declaration.");
        G1ModelGeometry geometry = model.Model.Geometry;
        G1DrawShaderIdentifier draw = G1ModelShaderIdentifierReader.Read(geometry).Draws.SingleOrDefault(row => row.Draw.Index == drawIndex) ??
            throw new InvalidDataException("Original group raster draw is absent from its source group.");
        byte code = draw.Group.RawBytes[18];
        if (code == 0) return null;
        if (code > 3)
            throw new InvalidDataException("Original group raster code exceeds the verified native cull table.");
        uint nativeCull = code switch { 1 => 1u, 2 => 3u, 3 => 2u, _ => throw new InvalidDataException() };
        return new(model, draw, code, ((ulong)code << 4) | 3, 3, nativeCull, true);
    }

    public static ModelQueueStateSource ReadQueue(LoadedRenderGraph graph, int nodeIndex)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.Document.GraphKind != CompiledGraphKind.RenderGraph)
            throw new InvalidDataException("Model queue requires a render graph.");
        CompiledGraphNode node = graph.Document.Nodes.SingleOrDefault(n => n.Index == nodeIndex) ??
            throw new InvalidDataException($"Original model queue node {nodeIndex} is absent.");
        if (node.Call.FactoryClassId != ModelShaderResources.QueueFactoryClass)
            throw new InvalidDataException("Original model queue requires its exact FC64DCB6 factory.");
        TaskGraphArgument Require(uint hash, uint descriptor, int bytes)
        {
            TaskGraphArgument[] rows = node.Call.Arguments.Where(a => a.NameHash == hash).ToArray();
            if (rows.Length != 1 || rows[0].DescriptorWord != descriptor || rows[0].RawValue.Length != bytes)
                throw new InvalidDataException($"Original model queue {nodeIndex} field {hash:X8} requires its complete original typed literal.");
            return rows[0];
        }
        var fields = new[]
        {
            Require(0x05D92ACC, 0x03000001, 2), Require(0x5B2B79DA, 0x05000001, 4),
            Require(0x092BC250, 0x00000001, 1), Require(0x72DB7EBD, 0x05000001, 4),
            Require(0x9075EDA0, 0x01000001, 1), Require(0xA98962A5, 0x01000001, 1),
            Require(0xB287F508, 0x01000001, 1)
        };
        if (fields[2].RawValue[0] != 0 || fields[5].RawValue[0] == byte.MaxValue)
            throw new InvalidDataException("Original model queue requires the supported ordinary RenderType and explicit state-array offset.");
        return new(graph, node, BinaryPrimitives.ReadUInt16LittleEndian(fields[0].RawValue),
            BinaryPrimitives.ReadUInt32LittleEndian(fields[1].RawValue), fields[2].RawValue[0],
            BinaryPrimitives.ReadUInt32LittleEndian(fields[3].RawValue), fields[4].RawValue[0],
            fields[5].RawValue[0], fields[6].RawValue[0] != 0, Array.AsReadOnly(fields));
    }

    public static ModelAlphaTransition PrepareQueueAlpha(ModelQueueStateSource queue,
        ModelAlphaState explicitPrevious)
    {
        ArgumentNullException.ThrowIfNull(queue); ArgumentNullException.ThrowIfNull(explicitPrevious);
        bool enabled = queue.BackendAlphaEnabled;
        return new(explicitPrevious with { Enabled = enabled }, explicitPrevious.Enabled == enabled ? null :
            (enabled ? 0x00010017u : 0x00000017u));
    }

    /// <summary>D28538 compares the material and modifier pointers as a pair;
    /// equal identities skip both the material setter and reference command.
    /// Tokens name prepared payload identities, not merely material byte equality.
    /// Modifier payload semantics require a separately prepared source.</summary>
    public static ModelDrawMaterialTransition PrepareUnmodifiedMaterial(
        ModelGbufferMaterialSource source, string explicitMaterialToken,
        ModelDrawMaterialCache explicitPrevious, ModelAlphaState explicitAlphaState)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(explicitPrevious);
        ArgumentNullException.ThrowIfNull(explicitAlphaState);
        if (string.IsNullOrWhiteSpace(explicitMaterialToken))
            throw new InvalidDataException("Original model draw material requires its prepared payload identity.");
        bool changed = explicitPrevious.MaterialToken != explicitMaterialToken || explicitPrevious.ModifierToken is not null;
        if (!changed)
            return new(false, explicitPrevious, new(false, null, explicitPrevious.CachedMaterialFlags),
                new(explicitAlphaState, null));
        ModelSpecularPreparation specular = ModelGbufferMaterialSourceReader.PrepareSpecular(
            source, explicitPrevious.CachedMaterialFlags);
        var alpha = explicitAlphaState with { CompareCode = 4, Reference = source.SourceAlphaReference };
        uint? command = explicitAlphaState.CompareCode == 4 && explicitAlphaState.Reference == source.SourceAlphaReference ?
            null : ((uint)source.SourceAlphaReference << 24) | 0x00040018u;
        return new(true, new(explicitMaterialToken, null, specular.NextCachedFlags), specular, new(alpha, command));
    }

    /// <summary>95C4A0 dispatches low16 to table14BE230. Entry17 is958340,
    /// entry18 is958370, each consumes exactly one four-byte command.</summary>
    public static ModelAlphaState InterpretAlphaCommand(uint word, ModelAlphaState explicitPrevious)
    {
        ArgumentNullException.ThrowIfNull(explicitPrevious);
        return (word & 0xFFFF) switch
        {
            0x17 => explicitPrevious with { Enabled = (word & 0xFFFF0000) != 0 },
            0x18 => explicitPrevious with { CompareCode = (word >> 16) & 0xFF, Reference = word >> 24 },
            _ => throw new InvalidDataException("Original alpha command requires opcode17 or opcode18.")
        };
    }

    /// <summary>9954B0 restores the renderer begin snapshot's +15D1 byte;
    /// that byte was copied from frontend +1BE at99491A. It is not a shader
    /// header flag or an unconditional enabled state.</summary>
    public static ModelAlphaTransition RestoreAlpha(bool explicitSavedEnabled,
        ModelAlphaState explicitPrevious)
    {
        ArgumentNullException.ThrowIfNull(explicitPrevious);
        return new(explicitPrevious with { Enabled = explicitSavedEnabled },
            explicitSavedEnabled == explicitPrevious.Enabled ? null : (explicitSavedEnabled ? 0x00010017u : 0x17u));
    }

    public static float PrepareAlphaTest(ModelAlphaState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Enabled) return BitConverter.UInt32BitsToSingle(0xFF7FFFFF);
        if (state.CompareCode != 4 || state.Reference > byte.MaxValue)
            throw new InvalidDataException("Original model alpha threshold requires the supported compare4 and byte reference.");
        return ModelGbufferMaterialSourceReader.PrepareAlphaTest((byte)state.Reference, true);
    }

    /// <summary>EA6582 derives the ordinary queue depth compare from renderer
    /// +3E4 bit29; source Flag bit11 reverses the ordered comparison; Flag2
    /// bit1 uses equal with no write. The renderer mode remains explicit.
    /// D71500 reads packed bits0,1..2,3..6,7 for test/write/compare/stencil.</summary>
    public static ModelQueueDepthState PrepareQueueDepth(ModelQueueStateSource queue,
        bool explicitRendererFlagBit29)
    {
        ArgumentNullException.ThrowIfNull(queue);
        uint compare = explicitRendererFlagBit29 ? 6u : 3u;
        if ((queue.Flag & 0x800) != 0) compare = compare == 3 ? 6u : 3u;
        bool equalPass = (queue.Flag2 & 2) != 0;
        if (equalPass) compare = 2;
        ulong packed = 0x7000700000000001UL | (((ulong)compare * 4 | (equalPass ? 0UL : 1UL)) * 2);
        return new(packed, (packed & 1) != 0, (packed & 6) != 0,
            (uint)(packed >> 3) & 0xF, (packed & 0x80) != 0);
    }
}
