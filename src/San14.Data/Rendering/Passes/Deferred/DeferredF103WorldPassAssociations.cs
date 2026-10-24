using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Node166 declaration and E69720's exact constructor packing of594.
/// A later edited/imported CPU requires a separate current field producer.</summary>
public sealed class DeferredF103WorldPassSource
{
    internal DeferredF103WorldPassSource(LoadedRdbAsset source, byte view, uint group, byte ocean)
    {
        ResourceUri = source.ResourceUri; DecodedSha256 = source.DecodedSha256;
        ViewId = view; LightGroupId = group; RawIsOceanRender = ocean;
        ConstructorFlags594 = ((uint)view << 24) | (ocean != 0 ? 0x100u : 0u) | (byte)group;
    }
    public string ResourceUri { get; }
    public string DecodedSha256 { get; }
    public byte ViewId { get; }
    public uint LightGroupId { get; }
    public byte RawIsOceanRender { get; }
    public uint ConstructorFlags594 { get; }
}

public sealed record DeferredF103WorldPassViewInputs(long FrameNumber, string ViewIdentity,
    bool? ConstructorFlags594Retained, uint? ViewFlags19AC, uint? ViewFlags19B0,
    bool? CommonView240Present, byte? CurrentAux3F2, string CurrentProducerProvenance);

/// <summary>Ordered native nested-loop association. Current L is the outer
/// record; arg17 is D59210(inner.L), with a null fallback to outer.H. Selected
/// shadow View1BD0 remains a separate source owner.</summary>
public sealed record DeferredF103WorldPassAssociation(
    MapWorldKind1CurrentRecord OuterRecord, MapWorldKind1CurrentRecord InnerRecord,
    uint EnabledInnerBit);

public sealed record DeferredF103WorldPassPlan(DeferredF103WorldPassSource Source,
    MapWorldKind1CurrentQueue Queue, DeferredF103WorldPassViewInputs CurrentView,
    uint OuterExclusionMask, uint EnabledInnerMask,
    IReadOnlyList<DeferredF103WorldPassAssociation> Associations);

public static class DeferredF103WorldPassAssociations
{
    public static DeferredF103WorldPassSource LoadNode166(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadNode166(repository.LoadRenderGraph("system", 0x024496EE));
    }

    public static DeferredF103WorldPassSource ReadNode166(LoadedRenderGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var source = graph.Source;
        if (source.FileId != 0x024496EE || source.ResourceUri != GameResourceId.Rdb("system", 0x024496EE).ToString() ||
            source.DecodedSha256 != MapSourceCreatedShaderSelection.GraphSha256 ||
            source.OverrideSource is not null)
            throw new InvalidDataException("F103 associations require the original complete node166 graph source.");
        //The repository owns resource decoding, provenance and document
        //parsing. Retain only the imported constructor values, not a second
        //whole graph buffer to compare again on every current World frame.
        if (graph.Document.GraphKind != CompiledGraphKind.RenderGraph || graph.Document.Nodes.Count <= 166)
            throw new InvalidDataException("F103 associations require a RenderGraph with node166.");
        var node = graph.Document.Nodes[166];
        if (node.Call.FactoryClassId != 0xF103DB00)
            throw new InvalidDataException("World light association source is not node166's F103 factory.");
        var view = Argument(node, 0x9075EDA0, 1, 1);
        var group = Argument(node, 0x59BD6FC4, 5, 4);
        var ocean = Argument(node, 0xE8F3DC36, 1, 1);
        return new(source, view.RawValue[0], BinaryPrimitives.ReadUInt32LittleEndian(group.RawValue), ocean.RawValue[0]);
    }

    /// <summary>Use the actual current View publisher's separate19AC/19B0
    /// owners. The original group/Aux outer mask is derived here, without a
    /// caller-supplied Field admission mask.</summary>
    public static DeferredF103WorldPassPlan Prepare(DeferredF103WorldPassSource source,
        MapWorldKind1CurrentQueue queue, MapSourceCreatedViewFieldsPacket view,
        bool? constructorFlags594Retained, bool? currentCommonView240Present, byte? currentAux3F2)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(queue); ArgumentNullException.ThrowIfNull(view);
        if (!ReferenceEquals(view.CurrentWorldQueue, queue) || view.Binding != queue.Binding)
            throw new InvalidDataException("F103 association View must retain this exact counted current world queue.");
        return Prepare(source, queue, new(queue.FrameNumber, queue.Binding.ViewIdentity, constructorFlags594Retained,
            view.ViewFlags19AC, view.StatisticalOceanFlags19B0, currentCommonView240Present, currentAux3F2, view.SourceProvenance));
    }

    public static DeferredF103WorldPassPlan Prepare(DeferredF103WorldPassSource source,
        MapWorldKind1CurrentQueue queue, DeferredF103WorldPassViewInputs current)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(queue); ArgumentNullException.ThrowIfNull(current);
        queue.ValidateSources();
        if (current.FrameNumber <= 0 || current.FrameNumber != queue.FrameNumber || current.ViewIdentity != queue.Binding.ViewIdentity ||
            string.IsNullOrWhiteSpace(current.CurrentProducerProvenance))
            throw new InvalidDataException("F103 nested associations require one current frame/View queue and actual View field producer.");
        if (current.ConstructorFlags594Retained != true)
            throw new NotSupportedException("Unknown or edited F103 CPU594 cannot use the declaration constructor word as current state.");
        uint enabled = (source.ConstructorFlags594 & 0x100) != 0 ?
            current.ViewFlags19B0 ?? throw new InvalidDataException("Ocean F103 requires current View19B0; it is not implicitly zero.") :
            (current.ViewFlags19AC ?? throw new InvalidDataException("Ordinary F103 requires current View19AC.")) & 0x9FFFFFFF;
        if (current.CommonView240Present is not bool present)
            throw new InvalidDataException("F103 outer group override requires the actual Common240 presence branch.");
        uint selectedGroup = source.ConstructorFlags594;
        if (present)
        {
            byte aux = current.CurrentAux3F2 ?? throw new InvalidDataException("Present Common240 requires current Aux3F2.");
            if (aux != 0) selectedGroup = aux;
        }
        uint exclusion = ~(1u << (int)(unchecked(selectedGroup - 1) & 31));
        var result = new List<DeferredF103WorldPassAssociation>();
        var records = queue.Records;
        foreach (var outer in records)
        {
            if ((outer.Mask78 & exclusion) != 0) continue;
            for (uint bit = 1; bit <= 8; bit <<= 1)
            {
                if ((enabled & bit) == 0) continue;
                foreach (var inner in records)
                    if (inner.Mask78 == bit) result.Add(new(outer, inner, bit));
            }
        }
        return new(source, queue, current, exclusion, enabled, Array.AsReadOnly(result.ToArray()));
    }

    private static TaskGraphArgument Argument(CompiledGraphNode node, uint hash, uint type, int bytes)
    {
        var matches = node.Call.Arguments.Where(a => a.NameHash == hash).ToArray();
        if (matches.Length != 1 || matches[0].RawType != type || matches[0].RawElementCount != 1 || matches[0].RawValue.Length != bytes)
            throw new InvalidDataException($"F103 node166 constructor field {hash:X8} has a different typed descriptor.");
        return matches[0];
    }
}
