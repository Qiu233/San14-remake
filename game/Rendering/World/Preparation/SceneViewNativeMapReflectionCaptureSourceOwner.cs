using San14.Data;

public sealed record ReflectionCaptureCurrentPrimitive(DeferredEarlyShadowCurrentModelCandidate Model,
    int SourceDrawIndex,ulong? FirstCounterKey)
{
    /// <summary>Null before the actual shared tuple owner supplies its span.</summary>
    public object? ActualTupleIdentity {get;init;}
}
public sealed record ReflectionCaptureCurrentFaceCounter(int Face,
    IReadOnlyList<ReflectionCaptureCurrentPrimitive> InsertionOrder,int DroppedCategorySpans)
{
    /// <summary>Null until the actual parent ViewParam sort has run.</summary>
    public IReadOnlyList<ReflectionCaptureCurrentPrimitive>? OrderedPrimitives {get;init;}
}

/// <summary>One real same-family current World traversal. This is CPU capture
/// publication, not a Scene registry entry, shader packet or GPU receipt.</summary>
public sealed record ReflectionCaptureCurrentFamilyPublication(long FrameNumber,string ViewIdentity,string Family,
    ReflectionCaptureFieldCellGeometry Cell,ReflectionCaptureCullingSource Culling,
    ReflectionCaptureVisibleCameraFields Camera,IReadOnlyList<ReflectionCaptureCurrentFaceCounter> Faces);

/// <summary>Reads the existing current WorldF103/SceneView owners, including
/// offscreen shadow-eligible Models. Main visibility is only the ordinary
/// branch of C394F0, never the capture culler or complete population.</summary>
public static class SceneViewNativeMapReflectionCaptureSourceOwner
{
    public static ReflectionCaptureCurrentFamilyPublication PrepareCurrentStatic(
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame current,ReflectionCaptureFieldInstance field,
        uint sourceOrdinal,uint actualClipProfile,NativeCameraTangentDispatch dispatch)
    {
        var admissions=current.PreparedShadowStaticAdmissions??throw new NotSupportedException(
            "Capture requires the actual current static World mask publication; Main visibility cannot replace it.");
        var population=current.Admission.CpuPopulation;
        var visible=population.Visibility.Visible.Select(instance=>instance.InstanceKey).ToHashSet(StringComparer.Ordinal);
        return Prepare(current,field,sourceOrdinal,actualClipProfile,dispatch,admissions,visible,"Static");
    }
    /// <summary>D799D0/D79920 passes the current parent ViewParam Shadow
    /// descriptor to every capture face's D4B7E0. Table field3 comes from the
    /// same current final material table used by state12 packet preparation.</summary>
    public static ReflectionCaptureCurrentFamilyPublication PrepareCurrentStaticSorted(
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame current,ReflectionCaptureFieldInstance field,
        uint sourceOrdinal,uint actualClipProfile,NativeCameraTangentDispatch dispatch,
        SceneViewNativeMapModelSourceOwner actualModels,WorldPrimitiveOrderSource actualCurrentViewParameter,
        DeferredEarlyShadowCurrentTupleBank actualPriorTupleBank)
    {
        var publication=PrepareCurrentStatic(current,field,sourceOrdinal,actualClipProfile,dispatch);
        ArgumentNullException.ThrowIfNull(actualPriorTupleBank);
        if(actualPriorTupleBank.FrameNumber!=publication.FrameNumber||actualPriorTupleBank.ViewIdentity!=publication.ViewIdentity)
            throw new InvalidDataException("Capture must borrow the same current View's actual shared tuple bank.");
        // Current Main and shadow counters may already own this span/key.
        // This bounded adapter can borrow a genuinely published prior span;
        // an unobserved/allocated-but-unpublished span requires its real owner.
        var prior=new Dictionary<(DeferredEarlyShadowCurrentModelCandidate Model,int Draw),DeferredEarlyShadowCurrentPrimitiveTuple>();
        for(int counter=0;counter<11;counter++)
            foreach(var tuple in actualPriorTupleBank.ReadInsertionOrder(counter))
                if(tuple.Category==0)prior.TryAdd((tuple.Model,tuple.SourceDrawIndex),tuple);
        ReflectionCaptureCurrentPrimitive Borrow(ReflectionCaptureCurrentPrimitive primitive)
        {
            if(!prior.TryGetValue((primitive.Model,primitive.SourceDrawIndex),out var tuple))
                throw new NotSupportedException("Capture tuple key requires the actual existing shared category span; absence from prior counters is not proof that no span was allocated.");
            return primitive with{FirstCounterKey=tuple.Key8,ActualTupleIdentity=tuple.TupleIdentity};
        }
        var instances=current.Admission.CpuPopulation.Population.Instances.ToDictionary(value=>value.InstanceKey,StringComparer.Ordinal);
        var tables=new Dictionary<(string Model,int Draw),NativeWorldShaderTableState>();
        ulong Key(ReflectionCaptureCurrentPrimitive tuple,int selectedField)
        {
            if(selectedField==14)return tuple.FirstCounterKey??throw new InvalidDataException("Current capture tuple key has no actual producer.");
            if(selectedField!=3)throw new NotSupportedException("Current capture tuple has no producer for selected Shadow sort field "+selectedField);
            var key=(Model:tuple.Model.ModelIdentity,Draw:tuple.SourceDrawIndex);
            if(!tables.TryGetValue(key,out var state))
            {
                state=actualModels.LoadFinalOwnedTable(instances[key.Model].Asset,key.Draw);
                actualModels.RequireCurrentShaderState(state);tables.Add(key,state);
            }
            return state.OrderedAllocationIdentity;
        }
        return publication with{Faces=Array.AsReadOnly(publication.Faces.Select(face=>
        {
            var insertion=Array.AsReadOnly(face.InsertionOrder.Select(Borrow).ToArray());
            return face with{InsertionOrder=insertion,
                OrderedPrimitives=Array.AsReadOnly(actualCurrentViewParameter.Sort(WorldPrimitiveSortFamily.Shadow,insertion,Key))};
        }).ToArray())};
    }
    public static ReflectionCaptureCurrentFamilyPublication PrepareCurrentArmy(
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame current,ReflectionCaptureFieldInstance field,
        uint sourceOrdinal,uint actualClipProfile,NativeCameraTangentDispatch dispatch)
    {
        var admissions=current.PreparedShadowArmyAdmissions??throw new NotSupportedException(
            "Capture requires the actual current Army World mask publication.");
        var visible=current.ArmyPublication.Actors.Where(actor=>!actor.Visibility.RejectedByCulling)
            .Select(actor=>actor.ActorIdentity).ToHashSet(StringComparer.Ordinal);
        return Prepare(current,field,sourceOrdinal,actualClipProfile,dispatch,admissions,visible,"Army");
    }
    private static ReflectionCaptureCurrentFamilyPublication Prepare(
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame current,ReflectionCaptureFieldInstance field,
        uint ordinal,uint profile,NativeCameraTangentDispatch dispatch,
        SceneViewNativeMapShadowModelAdmissions admissions,HashSet<string> mainVisible,string family)
    {
        field.RequireCurrent();var camera=current.PreparedMainCamera;
        if(admissions.Models.FrameNumber!=camera.FrameNumber||
            !ReferenceEquals(admissions.Common,current.GeometryFirst.Common))
            throw new InvalidDataException("Capture Model callers must belong to this current World/Camera.");
        var cell=field.CaptureCell(ordinal);
        var culling=ReflectionCaptureCullingSource.Prepare(cell,camera,profile,dispatch);
        var faces=Enumerable.Range(0,6).Select(_=>new List<ReflectionCaptureCurrentPrimitive>()).ToArray();
        var dropped=new int[6];
        foreach(var row in admissions.Rows)
        {
            var model=row.Model;var masks=row.Caller.Masks;
            if(!((mainVisible.Contains(model.ModelIdentity)&&masks.FirstOrdinarySlotEligible)||masks.ShadowSlotEligible))continue;
            if(model.Category0.Count==0&&model.Category1.Count==0)continue;
            if(model.Category1.Count!=0)throw new NotSupportedException(
                "Capture category1 requires its actual destination-counter and shared tuple-span owner before nonempty publication.");
            var visibility=culling.TestAndAccumulate(model.Bounds);
            if(visibility.FaceMask==0)continue;
            // Bounds supply face distances, not the shared tuple's first key.
            // Main/shadow may already have allocated it; the real bank must
            // supply that span before ordering or packet submission.
            var tuples=model.Category0.SourceDrawIndices.Select(draw=>new ReflectionCaptureCurrentPrimitive(model,draw,null)).ToArray();
            for(int face=0;face<6;face++)
            {
                if((visibility.FaceMask&(1u<<face))==0)continue;
                if((ulong)faces[face].Count+(ulong)tuples.Length>=field.Source.MaxPrimitiveNum){dropped[face]++;continue;}
                faces[face].AddRange(tuples);
            }
        }
        return new(camera.FrameNumber,admissions.Models.ViewIdentity,family,cell,culling,
            ReflectionCaptureCubeCameraSource.PrepareVisible(cell.Position,culling.VisibleMask,culling.NearClamp,culling.FarClamp,culling.FaceDepths),
            Array.AsReadOnly(faces.Select((items,face)=>new ReflectionCaptureCurrentFaceCounter(face,items.AsReadOnly(),dropped[face])).ToArray()));
    }
}
