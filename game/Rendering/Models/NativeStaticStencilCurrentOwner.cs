using San14.Data;

/// <summary>Owned static renderer modifier generation with no external
/// stencil/plugin assignments. The current renderer really consumes the
/// selected source class18 profile; original imported modifier caches are
/// outside this construction scope and invalidate the owner.</summary>
public sealed class NativeStaticStencilCurrentOwner
{
    private readonly Dictionary<string,ModelOrdinaryQueueSelectionSource> sources=new(StringComparer.Ordinal);
    private bool retained=true;
    public string OwnerIdentity { get; }="owned-static-class18-stencil:"+Guid.NewGuid().ToString("N");
    private NativeStaticStencilCurrentOwner() { }
    public static NativeStaticStencilCurrentOwner CreateFreshSourceClass18NoExternalStencilAssignments()=>new();
    public void MarkImportedEditedOrUnresolved(string actualWriter)
    {ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);retained=false;sources.Clear();}
    public NativeGeometryStencilSubmission PrepareCurrent(NativeMapFrameContext context,
        MapSourceCreatedStaticPrimitiveAdmissionPlan admission,MapStaticModelPreparedFrame prepared)
    {
        if(!retained)throw new InvalidDataException("Static native modifier generation was edited, imported or unresolved.");
        return NativeGeometryStencilSubmission.PrepareStatic(context,admission,prepared,
            (primitive,record)=>PrepareCurrentPrimitive(context,admission,primitive,record));
    }

    public ModelOrdinaryNativeStencilReceipt PrepareCurrentPrimitive(NativeMapFrameContext context,
        MapSourceCreatedStaticPrimitiveAdmissionPlan admission, MapStaticModelPreparedState primitive,
        MapSourceCreatedKind1ViewRecord record)
    {
            if(!retained||primitive.Runtime.Raster.NativeStencil is not null||primitive.Runtime.ForcedAlphaReference is not null)
                throw new InvalidDataException("Current static modifier requires an unmodified source renderer generation and no explicit stencil/alpha overrides.");
            var selected=primitive.Context.Selection.Source??throw new InvalidDataException("Current selected static shader source is unknown.");
            var graph=selected.GraphSource.Graph;
            var declared=selected.GraphSource.Interface;
            var provider=declared.Provider??throw new InvalidDataException("Current selected static provider is unknown.");
            var dictionary=declared.Owner.Objects.Source;
            var shader=selected.Shader.Source;
            string key=$"{graph.Source.ResourceUri}@{graph.Source.DecodedSha256}/node{selected.GraphSource.Node.Index}/state{declared.StateIndex}|"+
                $"{dictionary.ResourceUri}@{dictionary.DecodedSha256}/{declared.Owner.Object.ObjectId:X8}/{provider.Object.ObjectId:X8}|"+
                $"{shader.ResourceUri}@{shader.DecodedSha256}";
            if(!sources.TryGetValue(key,out var cached))
            {
                var queue=ModelRendererStateSourceReader.ReadQueue(graph,selected.GraphSource.Node.Index);
                cached=ModelOrdinaryQueueSelectionSource.ReadFreshSelected(queue,declared,selected.Shader);
                sources.Add(key,cached);
            }
            if (cached.Queue.Node.Index is not (54 or 55) || !cached.SourceQueueSelectsShader ||
                primitive.Runtime.BackendAlphaEnabled != cached.Queue.BackendAlphaEnabled)
                throw new InvalidDataException("Static primitive alpha state differs from its actual selected node54/55 branch.");
            string identity=primitive.Prepared.Packet.InstanceKey;
            var current=new ModelFreshOwnedStencilPublication(context.FrameNumber,admission.ViewIdentity,identity,
                OwnerIdentity,cached.SelectedRenderer.ProviderObjectId,cached.SelectedRenderer.ShaderSha256,true,true,true,true,true,
                OwnerIdentity+"/actual-static-selected-profile/frame="+context.FrameNumber+"/"+identity);
            if (!cached.SelectsCurrentPrimitive(current))
                throw new InvalidDataException("Current static primitive is not selected by this actual alpha queue.");
            return cached.SelectedRenderer.PrepareCurrent(new(context.FrameNumber,admission.ViewIdentity,record.Mask78,true,
                record.CurrentCpuPrimitiveCount,true,record.CurrentCountPublicationIdentity),admission.Frame.Common,
                admission.Frame.Lighting.ContextFlags3E4,current);
    }
}
