using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual Render2D CPU.v28 metadata, after its current alias getter.
/// Its resource and both shader views are physical caller inputs; this
/// structure does not prove a writer or choose an Indirect texture.</summary>
public sealed record DeferredEarlyRender2DMetadata(ulong PhysicalResource0, ulong ShaderView8, ulong ShaderView10,
    ushort Width30, ushort Height32, ushort PackedFlags36, byte[] SamplerKey38, byte[] Raw72)
{
    public ulong SelectedShaderView => (PackedFlags36 & 0x8000) != 0 ? ShaderView10 : ShaderView8;
}

public sealed record DeferredEarlyDepthClearCommand(uint Word0, uint ColorWord,
    uint DepthBits, uint ClearFlags)
{
    public bool ProvesGpuCompletion => false;
}

public sealed record DeferredEarlySubShadowDeclaration(CompiledGraphNode Node,
    uint ViewObjectId, byte ViewId, uint DepthObjectId, uint NormalObjectId,
    uint PrimaryShaderObjectId, uint AlternateShaderObjectId, byte SourceFlags,
    IReadOnlyList<uint> PatternBanks);

/// <summary>The two separate source predecessor domains before early
/// Indirect: EA depth bind17/clear18/queue19/pop20, and AB0 SubShadow62..64.
/// Load imports actual graph/resources/native sources once; methods never
/// reread source files or turn a graph declaration into pixel completion.</summary>
public sealed class LoadedDeferredEarlyPredecessorSource
{
    internal LoadedDeferredEarlyPredecessorSource(string native,
        LoadedRenderGraphTargets depth, LoadedRenderGraphTargets subShadow,
        CompiledGraphNode clear, CompiledGraphNode queue, CompiledGraphNode pop20,
        DeferredEarlySubShadowDeclaration draw, CompiledGraphNode pop64)
    {
        NativeSourceEvidence = native; ShadowDepthTargets17 = depth; SubShadowTargets62 = subShadow;
        DepthClear18 = clear; ShadowQueue19 = queue; ShadowDepthPop20 = pop20;
        SubShadow63 = draw; SubShadowPop64 = pop64;
    }
    public string NativeSourceEvidence { get; }
    public LoadedRenderGraphTargets ShadowDepthTargets17 { get; }
    public LoadedRenderGraphTargets SubShadowTargets62 { get; }
    public CompiledGraphNode DepthClear18 { get; }
    public CompiledGraphNode ShadowQueue19 { get; }
    public CompiledGraphNode ShadowDepthPop20 { get; }
    public DeferredEarlySubShadowDeclaration SubShadow63 { get; }
    public CompiledGraphNode SubShadowPop64 { get; }
    public RenderTextureDefinition ShadowDepthDeclaration => ShadowDepthTargets17.DepthTarget!;
    public RenderTextureFormats ShadowDepthFormats => RenderTextureFormatMath.Resolve(ShadowDepthDeclaration);
    public ushort ShadowQueueSourceFlags => 0x1008;
    public byte ShadowQueueRenderType => 2;
    public byte ShadowQueueShaderState => 3;
    public uint ShadowDepthPopFlags => 0;
    public uint SubShadowPopFlags => 1;

    /// <summary>Node18 has no clear shader. E93470 passes source depth1
    /// unchanged because CPU18.bit0=1; E738C0 emits opcode4 only when the
    /// actual current row's depth getter returns nonnull. Caller performs
    /// the real row/command submission and retains its resource lifetime.</summary>
    public DeferredEarlyDepthClearCommand? PrepareNode18NativeDepthClear(int currentHost3F0,
        bool actualCurrentDepthSurfacePresent)
        => currentHost3F0 < 0 || !actualCurrentDepthSurfacePresent ? null :
            new(0x00100004, 0x00FFFFFF, 0x3F800000, 0x10);

    /// <summary>Call this with bytes returned by the actual CPU.v28 getter,
    /// after aliases are followed. C0C770 returns B0; C0BB00 returns B8 and
    /// therefore is a different domain. The original 96C5A0 copies 72B.</summary>
    public DeferredEarlyRender2DMetadata ReadActualRender2DMetadata(ReadOnlySpan<byte> actualMetadata72)
    {
        if (actualMetadata72.Length != 72)
            throw new InvalidDataException("Actual Render2D CPU.v28 metadata must retain its complete 72-byte source layout.");
        return new(BinaryPrimitives.ReadUInt64LittleEndian(actualMetadata72),
            BinaryPrimitives.ReadUInt64LittleEndian(actualMetadata72.Slice(8)),
            BinaryPrimitives.ReadUInt64LittleEndian(actualMetadata72.Slice(0x10)),
            BinaryPrimitives.ReadUInt16LittleEndian(actualMetadata72.Slice(0x30)),
            BinaryPrimitives.ReadUInt16LittleEndian(actualMetadata72.Slice(0x32)),
            BinaryPrimitives.ReadUInt16LittleEndian(actualMetadata72.Slice(0x36)),
            actualMetadata72.Slice(0x38, 8).ToArray(), actualMetadata72.ToArray());
    }
}

public static class DeferredEarlyPredecessorSource
{
    public static LoadedDeferredEarlyPredecessorSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string native = RenderSamplerSourceAdapters.LoadEarlyPredecessorNativeSource(repository.SourceDirectory, locale);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var objects = repository.LoadObjects("system", 0x93FB3280);
        if (graph.Source.ResourceUri != "san14://rdb/system/024496EE" ||
            Convert.ToHexString(SHA256.HashData(objects.Source.Bytes)) != DeferredAuxiliaryTargetSources.ActualObjectsSha256)
            throw new InvalidDataException("Early predecessor source graph/object directory differs.");
        graph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        objects = objects with { Document = DokReader.Read(objects.Source.Bytes) };
        CompiledGraphNode Node(int index, uint key, uint cls)
        {
            var n = graph.Document.Nodes[index];
            if (n.Call.LookupKey != key || n.Call.FactoryClassId != cls ||
                !n.Call.ConnectedNodeIndices.SequenceEqual(new[] { (uint)index + 1 }))
                throw new InvalidDataException($"Early predecessor node{index} source identity/edge differs.");
            return n;
        }
        byte[] Arg(CompiledGraphNode n, uint name, uint packed, int count)
        {
            var a = n.Call.Arguments.Single(a => a.NameHash == name);
            if (a.DescriptorWord != packed || a.RawValue.Length != count)
                throw new InvalidDataException("Early predecessor has a different typed source argument.");
            return a.RawValue;
        }
        uint Word(CompiledGraphNode n, uint name) => BinaryPrimitives.ReadUInt32LittleEndian(Arg(n, name, 0x05000001, 4));
        var depth = RenderGraphTargetResources.Read(graph, 17, objects);
        var sub = RenderGraphTargetResources.Read(graph, 62, objects);
        Node(17, 0x960F7BA0, 0xE2C0442A); Node(62, 0x2038D5B6, 0xE2C0442A);
        if (depth.Flag != 5 || depth.ColorTargets.Count != 0 || depth.DepthObjectNameHash != 0xEA36C27D ||
            depth.DepthSurfaceId != 0 || sub.Flag != 0x12 || sub.ColorTargets.Count != 1 ||
            sub.ColorTargets[0].ObjectNameHash != 0x8B18CE44 || sub.ColorTargets[0].SurfaceId != 0 ||
            sub.DepthObjectNameHash != 0xE13DDB4F || sub.DepthSurfaceId != 0)
            throw new InvalidDataException("EA depth17 and AB0 SubShadow62 require their distinct actual attachments.");
        var ea = depth.DepthTarget!;
        if (ea.Kind != RenderTextureKind.Texture2D || ea.Width != 4096 || ea.Height != 4096 ||
            ea.PixelFormat != 0x4E || ea.ColorSpace != 0 || ea.OutputColorSpace != 0 ||
            ea.LevelNum != 1 || ea.MinMipLevel != 0 || ea.MultiSample != 0 || ea.NeverInternalCopy != 1)
            throw new InvalidDataException("EA shadow depth declaration differs from its actual fixed Render2D creation.");
        var clear = Node(18, 0x9C7153BC, 0xCD713E46);
        var queue = Node(19, 0x76E823DA, 0xFC64DCB6);
        var pop20 = Node(20, 0xF53776AE, 0x1F7C5CF8);
        if (BinaryPrimitives.ReadUInt32LittleEndian(Arg(clear, 0xAA8D0DB1, 0x04000001, 4)) != 0x10 ||
            !Arg(clear, 0xC8D7689D, 0x01000004, 4).SequenceEqual(new byte[] { 255, 255, 255, 0 }) ||
            BinaryPrimitives.ReadUInt32LittleEndian(Arg(clear, 0x00000AE6, 0x08000001, 4)) != 0x3F800000 ||
            BinaryPrimitives.ReadUInt32LittleEndian(Arg(clear, 0x349AC1A4, 0x04000001, 4)) != 0 ||
            Word(clear, 0x51332A47) != 0 || Word(clear, 0x05D92ACC) != 1 ||
            BinaryPrimitives.ReadInt32LittleEndian(Arg(clear, 0x39E192FF, 0x04000001, 4)) != -1 ||
            Word(pop20, 0x636CA9AE) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(Arg(queue, 0x05D92ACC, 0x03000001, 2)) != 0x1008 ||
            Arg(queue, 0x092BC250, 0x00000001, 1)[0] != 2 ||
            Arg(queue, 0xA98962A5, 0x01000001, 1)[0] != 3 || Word(queue, 0x61A17857) != 0xEA36C27D)
            throw new InvalidDataException("Actual EA predecessor clear/RenderType2-state3 queue/pop source differs.");
        var draw = Node(63, 0x04780E8E, 0x406CB6FF);
        var pop64 = Node(64, 0x5CFC5E44, 0x1F7C5CF8);
        var banksRaw = Arg(draw, 0x71550276, 0x05000008, 32);
        var banks = Enumerable.Range(0, 8).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(banksRaw.AsSpan(i * 4))).ToArray();
        if (Word(draw, 0x72DB7EBD) != 0xE1B6A687 || Arg(draw, 0x9075EDA0, 0x01000001, 1)[0] != 0 ||
            Word(draw, 0xB7CCDD1F) != 0xE13DDB4F || Word(draw, 0xF43A3947) != 0x7E94C315 ||
            Word(draw, 0xD7B0F360) != 0x361E1B96 || Word(draw, 0xD7B0FEA3) != 0x7BAF801C ||
            Arg(draw, 0x05D92ACC, 0x01000001, 1)[0] != 0 ||
            !banks.SequenceEqual(new uint[] { 1, 2, 0, 0, 0, 0, 0, 0 }) || Word(pop64, 0x636CA9AE) != 1)
            throw new InvalidDataException("SubShadow63 declaration/own provider banks/pop differs.");
        return new(native, depth, sub, clear, queue, pop20,
            new(draw, 0xE1B6A687, 0, 0xE13DDB4F, 0x7E94C315, 0x361E1B96, 0x7BAF801C, 0, Array.AsReadOnly(banks)), pop64);
    }
}
