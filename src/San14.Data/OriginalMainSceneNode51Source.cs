using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual current inputs of CPU.v8, distinct from factory.v70.
/// A null raw Q word is permitted only when A+50 is explicitly absent.
/// The selected slot and its owners come from the current View collection.</summary>
public sealed record OriginalMainSceneNode51Invocation(long FrameNumber,
    string ExecutionOwnerIdentity, string CurrentViewCollectionOwnerIdentity,
    uint CurrentViewCollectionSourceNameHash, bool? SourceCpuAndBindingsRetained,
    bool? ContextPointer50Absent, uint? CurrentContextWord3F8,
    int CurrentSelectedViewIndex, OriginalMapLightingBinding SelectedView,
    bool? SelectedViewSlotPresent, string CurrentProducerProvenance)
{
    public string? CurrentContextOwnerIdentity { get; init; }
}

/// <summary>One actual RGK argument imported at the source boundary. Raw
/// bytes are retained as immutable text, never reread in normal execution.</summary>
public sealed record OriginalGeneralSceneSourceArgument(int Index, uint NameHash,
    uint DescriptorWord, string RawHex, uint Value);

/// <summary>Source-created GeneralScene51/56 execution, not a GPU submission or a live
/// original frame. Flags come from RGK, while selection comes from CPU.v8's
/// actual current arguments. No default camera slot is supplied.</summary>
public sealed class OriginalMainSceneNode51Execution
{
    internal OriginalMainSceneNode51Execution(OriginalMainSceneNode51Source source,
        OriginalMainSceneNode51Invocation invocation)
    { Source = source; Invocation = invocation; }
    public OriginalMainSceneNode51Source Source { get; }
    public OriginalMainSceneNode51Invocation Invocation { get; }
    public long FrameNumber => Invocation.FrameNumber;
    public byte CurrentCallFlags => Source.InitialCallFlags;
    public uint CurrentFlag2 => Source.Flag2;
    public uint TerrainArgument9 => CurrentFlag2;
    public bool SuppressesOrdinaryTerrainF8 => (CurrentFlag2 & 1) != 0;
    public int SelectedViewIndex => Invocation.CurrentSelectedViewIndex;
    public string SourceProvenance => Source.SourceProvenance + ";" + Invocation.CurrentProducerProvenance;

    public void RequireSelectedView(long frame, OriginalMapLightingBinding selected)
    {
        if (FrameNumber != frame || Invocation.SelectedView != selected)
            throw new InvalidDataException("GeneralScene execution and geometry belong to different current View owners or frames.");
    }
}

/// <summary>E7EF10 creates CPU16E68D8, whose v8 is EA4520. The ordinary
/// branch calls Models D2B110 before Terrain D31D30. Both original nodes51
/// and56 create this CPU; their actual Flag2 decides the later F8 gate. Factory.v70 is a
/// different callback and must not be used to infer this execution route.</summary>
public sealed class OriginalMainSceneNode51Source
{
    private readonly LoadedRenderGraph graph;
    private OriginalMainSceneNode51Source(LoadedRenderGraph graph, string locale, string ruleVersion,
        int nodeIndex, uint lookupKey, uint factoryClass, uint[] edges,
        OriginalGeneralSceneSourceArgument[] arguments, LoadedOriginalGeneralSceneTerrainCallerSource caller)
    {
        this.graph = graph; Locale = locale; RuleVersion = ruleVersion;
        NodeIndex = nodeIndex; LookupKey = lookupKey; FactoryClassId = factoryClass;
        ConnectedNodeIndices = Array.AsReadOnly(edges); Arguments = Array.AsReadOnly(arguments);
        ViewCollectionSourceNameHash = arguments[0].Value;
        SourceStateCollectionNameHash = arguments[1].Value;
        SourceViewId = checked((byte)arguments[2].Value);
        InitialCallFlags = checked((byte)arguments[4].Value); Flag2 = arguments[12].Value;
        CallerSource = caller;
    }
    public string Locale { get; }
    public string RuleVersion { get; }
    public int NodeIndex { get; }
    public uint LookupKey { get; }
    public uint FactoryClassId { get; }
    public IReadOnlyList<uint> ConnectedNodeIndices { get; }
    public IReadOnlyList<OriginalGeneralSceneSourceArgument> Arguments { get; }
    public byte InitialCallFlags { get; }
    public byte SourceViewId { get; }
    public uint ViewCollectionSourceNameHash { get; }
    public uint SourceStateCollectionNameHash { get; }
    public uint Flag2 { get; }
    public LoadedOriginalGeneralSceneTerrainCallerSource CallerSource { get; }
    public string SourceProvenance => $"{graph.Source.ResourceUri}@{graph.Source.DecodedSha256};{Locale}:{RuleVersion};" +
        $"node{NodeIndex}/{LookupKey:X8}/factory{FactoryClassId:X8};" +
        $"ViewID{SourceViewId}/flags{InitialCallFlags:X2}/Flag2={Flag2:X8};" +
        "Models-before-Terrain-caller;F8-arg9-bit0-skip;" + CallerSource.SourceProvenance;

    public static OriginalMainSceneNode51Source Load(OriginalGameRepository repository, string locale, int nodeIndex = 51)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var caller = OriginalGeneralSceneTerrainCallerSource.Create(locale);
        return ReadCore(repository.LoadRenderGraph("system", 0x024496EE), locale, nodeIndex, caller);
    }

    public static OriginalMainSceneNode51Source Read(LoadedRenderGraph graph, string locale, int nodeIndex = 51)
        => ReadCore(graph, locale, nodeIndex, OriginalGeneralSceneTerrainCallerSource.Create(locale));

    private static OriginalMainSceneNode51Source ReadCore(LoadedRenderGraph graph, string locale,
        int nodeIndex, LoadedOriginalGeneralSceneTerrainCallerSource caller)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (nodeIndex is not (51 or 56))
            throw new NotSupportedException("GeneralScene source closes the actual original nodes51 and56 only.");
        _ = RenderRuleIdentity.For("general-scene", locale);
        if (
            graph.Source.ResourceUri != GameResourceId.Rdb("system", 0x024496EE).ToString() ||
            graph.Source.FileId != 0x024496EE)
            throw new InvalidDataException("Node51 source graph resource reference differs.");
        // Reparse bytes instead of trusting a caller's independently supplied metadata.
        var document = RenderGraphReader.Read(graph.Source.Bytes);
        var call = document.Nodes.Single(n => n.Index == nodeIndex).Call;
        uint[] names = [0x72DB7EBD, 0x3BC0516E, 0x9075EDA0, 0x5E7B0199, 0x05D92ACC,
            0xB7CCDDBA, 0x67CF5EC9, 0x61A17857, 0xD9768D64, 0xDB2B6603, 0xDCE03EA2, 0xF6E79F61, 0x5B2B79DA];
        uint[] values = [0xE1B6A687, 0x4ADDFBDE, 0, 0, 0x11, 0, 0, 0,
            0x56673F30, 0x77D2D8B2, 0x671D0BF1, 0x6CE6F7A1, nodeIndex == 51 ? 1u : 0u];
        if (document.Nodes.Count != 211 || call.LookupKey != (nodeIndex == 51 ? 0x9CC40A63u : 0xC0DFE7A3u) ||
            call.FactoryClassId != caller.FactoryClassId || !call.ConnectedNodeIndices.SequenceEqual(new uint[] { (uint)nodeIndex + 1 }) ||
            call.Arguments.Count != names.Length)
            throw new InvalidDataException("Canonical GeneralScene factory, lookup or edges changed.");
        var arguments = new OriginalGeneralSceneSourceArgument[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var a = call.Arguments[i]; bool small = i is 2 or 4;
            if (a.Index != i || a.NameHash != names[i] || a.DescriptorWord != (small ? 0x01000001u : 0x05000001u) ||
                a.RawValue.Length != (small ? 1 : 4) || (small ? a.RawValue[0] : BinaryPrimitives.ReadUInt32LittleEndian(a.RawValue)) != values[i])
                throw new InvalidDataException($"GeneralScene node{nodeIndex} argument{i} changed from its paired native loader contract.");
            arguments[i] = new(i, a.NameHash, a.DescriptorWord, Convert.ToHexString(a.RawValue),
                small ? a.RawValue[0] : BinaryPrimitives.ReadUInt32LittleEndian(a.RawValue));
        }
        return new(graph, locale, RenderRuleIdentity.Version, nodeIndex, call.LookupKey, call.FactoryClassId,
            call.ConnectedNodeIndices.ToArray(), arguments, caller);
    }

    public OriginalMainSceneNode51Execution PrepareCurrent(OriginalMainSceneNode51Invocation current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.FrameNumber < 1 || current.SelectedView is null ||
            new[] { current.ExecutionOwnerIdentity, current.CurrentViewCollectionOwnerIdentity,
                current.SelectedView.ViewIdentity, current.SelectedView.CameraOwnerIdentity,
                current.SelectedView.LightOwnerIdentity, current.CurrentProducerProvenance }.Any(string.IsNullOrWhiteSpace) ||
            current.CurrentViewCollectionSourceNameHash != ViewCollectionSourceNameHash ||
            current.SourceCpuAndBindingsRetained != true || current.SelectedViewSlotPresent != true)
            throw new InvalidDataException("Node51 requires its retained CPU and actual nonnull selected View collection slot.");
        uint rawBase = current.ContextPointer50Absent switch {
            true when current.CurrentContextWord3F8 is null && current.CurrentContextOwnerIdentity is null => 0,
            false when current.CurrentContextWord3F8 is uint word && !string.IsNullOrWhiteSpace(current.CurrentContextOwnerIdentity) => word & 0x7FFFFFFF,
            _ => throw new InvalidDataException("Current A+50 Q absence or complete Q3F8 must be explicit.") };
        uint slot = unchecked(rawBase + (uint)(SourceViewId & 0x7F));
        if (slot >= 32 || current.CurrentSelectedViewIndex != slot)
            throw new InvalidDataException("Current View slot differs from EA4520's unsigned Q3F8 plus source ViewID selection.");
        return new(this, current);
    }

}
