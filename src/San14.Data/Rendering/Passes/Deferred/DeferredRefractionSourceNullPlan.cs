using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual node139/140 source-null execution. This proves absence of
/// shader submissions in RenderType0; it does not claim empty terrain, empty
/// primitive banks, a depth copy, or completion of any device operation.</summary>
public sealed class DeferredRefractionSourceNullPlan
{
    private readonly MapSourceCreatedF103CurrentFrame frame;
    private readonly DeferredSourceViewFamilyPacket families;
    private readonly DeferredF103SelectedSource selection;
    private readonly byte[] selectionFeature, selectionVariant, selectionCombination, selectionCamera;
    private DeferredRefractionSourceNullPlan(MapSourceCreatedF103CurrentFrame frame,
        DeferredSourceViewFamilyPacket families, ModelQueueStateSource first,
        ModelQueueStateSource second)
    {
        (this.frame,this.families,FirstQueue,SecondQueue)=(frame,families,first,second);
        selection=frame.Selection;
        selectionFeature=selection.CurrentFeature48.ToArray();
        selectionVariant=selection.RequestedVariant96.ToArray();
        selectionCombination=selection.Combination.RawRow.ToArray();
        selectionCamera=selection.Camera.Prefix.ToArray();
    }

    public ModelQueueStateSource FirstQueue { get; }
    public ModelQueueStateSource SecondQueue { get; }
    public long FrameNumber=>frame.FrameNumber;
    public uint TargetObjectId=>0xB6169FDB;
    public int OrdinaryCandidateCount=>families.OrdinaryCandidateCount;
    public string SourceProvenance=>families.CurrentCandidatePublicationProvenance+
        ";nodes139/140=RenderType0/state16;all-current-final-interfaces-null;"+
        "TerrainExcludedByRenderType0;sourceShapeRootAbsent;sourceFullscreenCount0;no-pixel-writes";

    public static DeferredRefractionSourceNullPlan Prepare(
        MapSourceCreatedF103CurrentFrame frame, DeferredSourceViewFamilyPacket families)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(families);
        families.ValidateCurrent(frame);
        if(!families.Shapes1C18Absent || families.SpecializedFullscreenCount1550!=0 ||
            !families.TerrainExcludedByRenderType0 || families.SelectedMode16ShaderSubmissions!=0)
            throw new NotSupportedException("Refraction requires execution of its current non-null source families.");
        var graph=frame.Selection.SourcePlan.Graph;
        var first=ModelRendererStateSourceReader.ReadQueue(graph,139);
        var second=ModelRendererStateSourceReader.ReadQueue(graph,140);
        var canonical=RenderGraphReader.Read(graph.Source.Bytes);
        if(graph.Source.OverrideSource is not null || graph.Source.ResourceUri!=DeferredAuxiliaryPixelSourceRecipes.GraphResourceUri ||
            graph.Source.DecodedSha256!=DeferredAuxiliaryPixelSourceRecipes.GraphSha256 ||
            frame.Selection.SourcePlan.ViewId!=0 || first.RenderType!=0 || second.RenderType!=0 ||
            first.ShaderStateOffset!=16 || second.ShaderStateOffset!=16 || first.ViewId!=0 || second.ViewId!=0 ||
            first.ViewObjectHash!=second.ViewObjectHash || canonical.Nodes.Count<=140 ||
            canonical.Nodes[97].Call.FactoryClassId!=0xFD1176C6 ||
            canonical.Nodes[138].Call.FactoryClassId!=0xE2C0442A)
            throw new InvalidDataException("Source-null refraction requires its original node97/138/139/140 Main View route.");
        var attachment=canonical.Nodes[138].Call.Arguments.SingleOrDefault(a=>a.NameHash==0xC13FB0BE);
        if(attachment is null || attachment.DescriptorWord!=0x05000001 || attachment.RawValue.Length!=4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(attachment.RawValue)!=0xB6169FDB)
            throw new InvalidDataException("Source-null geometry is not attached to the original B6169FDB depth target.");
        return new(frame,families,first,second);
    }

    public void ValidateCurrent(MapSourceCreatedF103CurrentFrame accepted,
        DeferredSourceViewFamilyPacket acceptedFamilies)
    {
        if(!ReferenceEquals(frame,accepted) || !ReferenceEquals(families,acceptedFamilies))
            throw new InvalidDataException("Source-null refraction belongs to another accepted frame or family publication.");
        families.ValidateCurrent(accepted);
        // Prepare already checked the exact original queue/attachment and
        // derived shader selection. The plan retains that immutable result;
        // runtime leases check mutable source bytes and accepted key snapshots,
        // rather than parse both full source resources again on the render thread.
        if(!ReferenceEquals(selection,accepted.Selection) ||
            !ReferenceEquals(FirstQueue.Graph,selection.SourcePlan.Graph) ||
            !ReferenceEquals(SecondQueue.Graph,selection.SourcePlan.Graph) ||
            !selectionFeature.AsSpan().SequenceEqual(selection.CurrentFeature48.Span) ||
            !selectionVariant.AsSpan().SequenceEqual(selection.RequestedVariant96.Span) ||
            !selectionCombination.AsSpan().SequenceEqual(selection.Combination.RawRow.Span) ||
            !selectionCamera.AsSpan().SequenceEqual(selection.Camera.Prefix.Span) ||
            Convert.ToHexString(SHA256.HashData(selection.SourcePlan.Graph.Source.Bytes))!=
                DeferredAuxiliaryPixelSourceRecipes.GraphSha256 ||
            Convert.ToHexString(SHA256.HashData(selection.SourcePlan.Shader.Source.Bytes))!=
                MapSourceCreatedShaderSelection.ShaderSha256)
            throw new InvalidDataException("Source-null refraction differs from its accepted graph or source selection.");
        if(selection.OwnedFeatureSource is { } feature &&
            (feature.FrameNumber!=selection.Current.FrameNumber ||
             !feature.CurrentFeature48.Span.SequenceEqual(selectionFeature) ||
             !feature.AcceptedCamera.Prefix.Span.SequenceEqual(selectionCamera) ||
             feature.CurrentView.CurrentProducerProvenance!=selection.Current.CurrentProducerProvenance))
            throw new InvalidDataException("Source-null refraction selected feature has changed its accepted Camera/light/View owner.");
    }
}
