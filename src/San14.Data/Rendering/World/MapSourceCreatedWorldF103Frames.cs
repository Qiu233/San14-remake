namespace San14.Data;

public sealed record MapSourceCreatedWorldF103Pass(DeferredF103WorldPassAssociation Association,
    MapSourceCreatedF103CurrentFrame Frame, MapSourceCreatedLightingPacket InnerLighting);

/// <summary>One complete counted View and the original ordered F103 outer/inner
/// passes. These are CPU preparations, without a draw or GPU completion claim.</summary>
public sealed record MapSourceCreatedWorldF103Frames(MapSourceCreatedViewFieldsPacket View,
    DeferredF103WorldPassPlan Plan, IReadOnlyList<MapSourceCreatedWorldF103Pass> Passes)
{
    private IReadOnlyDictionary<string,MapSourceCreatedLightingPacket> completeRecordLighting =
        new Dictionary<string,MapSourceCreatedLightingPacket>(StringComparer.Ordinal);
    public int CompleteRecordPayloadCount=>completeRecordLighting.Count;

    /// <summary>Copy one exact member of the complete World queue. Later
    /// consumers must not recover unsubmitted group4/10 payloads from the
    /// primary light or the subset of F103 outer/inner passes.</summary>
    public MapSourceCreatedLightingPacket CopyAcceptedRecordLighting(MapWorldKind1CurrentRecord record)
    {
        var packet=BorrowAcceptedRecordLighting(record);
        return packet.CopyAcceptedSnapshot();
    }

    /// <summary>Borrow the immutable packet already accepted at World
    /// preparation. Current lookups do not replay its original constructors.</summary>
    public MapSourceCreatedLightingPacket BorrowAcceptedRecordLighting(MapWorldKind1CurrentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var queue=View.CurrentWorldQueue ??throw new InvalidDataException("World payload lookup has no actual complete queue.");
        if(!queue.Records.Any(r=>ReferenceEquals(r,record)) ||
            completeRecordLighting.Count!=queue.Records.Count ||
            !completeRecordLighting.TryGetValue(record.RecordOwnerIdentity,out var packet) ||
            packet.FrameNumber!=queue.FrameNumber || packet.SourceIdentity.MainLightObjectId!=record.Source.SourceObjectId ||
            packet.SourceIdentity.DeclaredGroupMask!=record.Mask78)
            throw new InvalidDataException("World record lookup is foreign or does not retain every exact current payload owner.");
        return packet;
    }

    /// <summary>Evaluate the actual later Q snapshot on this same counted
    /// View and complete lighting roster. Geometry keeps its earlier packets;
    /// later selected programs bind current fields even when the key is equal.</summary>
    public MapSourceCreatedWorldF103Frames PrepareForCurrentContext(
        MapSourceCreatedF103CurrentFrameFactory factory,
        MainMapContextCurrentPacket actualCurrentContext, string preparationIdentity)
    {
        ArgumentNullException.ThrowIfNull(factory); ArgumentNullException.ThrowIfNull(actualCurrentContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparationIdentity);
        var first = Passes.FirstOrDefault()?.Frame ?? throw new InvalidDataException("Current context needs the actual nonempty World pass roster.");
        if (actualCurrentContext.FrameNumber != first.FrameNumber ||
            !ReferenceEquals(actualCurrentContext.Source, first.Context.Source))
            throw new InvalidDataException("Later World context belongs to another frame or Host owner.");
        var population = first.CpuPopulation ?? throw new InvalidDataException("Later World context needs its actual current CPU population.");
        var passes = new List<MapSourceCreatedWorldF103Pass>(Passes.Count);
        MapSourceCreatedF103FeaturePacket? previous = null;
        foreach (var currentPass in Passes)
        {
            var outer = completeRecordLighting[currentPass.Association.OuterRecord.RecordOwnerIdentity];
            var inner = completeRecordLighting[currentPass.Association.InnerRecord.RecordOwnerIdentity];
            var frame = factory.Prepare(population.MainCamera.Camera, outer, View, first.Common,
                actualCurrentContext, first.ActualReflectionQueryD57E20, preparationIdentity + "/pass/" + passes.Count,
                population, inner, previous);
            passes.Add(new(currentPass.Association, frame, inner)); previous = frame.Feature;
        }
        return new(View, Plan, Array.AsReadOnly(passes.ToArray())) { completeRecordLighting = completeRecordLighting };
    }
    public static MapSourceCreatedWorldF103Frames Prepare(
        MapSourceCreatedF103CurrentFrameFactory factory, DeferredF103WorldPassSource source,
        MapStaticModelCurrentOwnedFrame population, MapWorldKind1CurrentQueue queue,
        MapSourceCreatedLightingPacket primaryLighting,
        IReadOnlyDictionary<string, MapSourceCreatedLightingPacket> completeRecordLighting,
        MapCommonRenderState common, MainMapContextCurrentPacket context,
        MapSourceCreatedF103PopulationPublicationInputs publication,
        IReadOnlyDictionary<string, int>? completeOtherCountsByRecordOwner,
        int? reflectionQuery, bool? constructorFlags594Retained, string preparationIdentity)
    {
        ArgumentNullException.ThrowIfNull(factory); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queue); ArgumentNullException.ThrowIfNull(completeRecordLighting);
        ArgumentNullException.ThrowIfNull(primaryLighting); ArgumentException.ThrowIfNullOrWhiteSpace(preparationIdentity);
        if (queue.Binding != primaryLighting.Binding ||
            !queue.Records.Select(r => r.RecordOwnerIdentity).Order(StringComparer.Ordinal)
                .SequenceEqual(completeRecordLighting.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("World F103 needs all five actual record payload owners and its independently selected primary frame.");
        var snapshots = new Dictionary<string, MapSourceCreatedLightingPacket>(StringComparer.Ordinal);
        foreach (var record in queue.Records)
        {
            var lighting = completeRecordLighting[record.RecordOwnerIdentity];
            if (lighting.SourceLightClassId != DirectionalLightSource.SourceClass ||
                lighting.SourceIdentity.MainLightObjectId != record.Source.SourceObjectId ||
                record.Source.SourceDeclarationIdentity != $"{lighting.SourceIdentity.MainLightResourceUri}#{lighting.SourceIdentity.MainLightObjectId:X8}@{lighting.SourceIdentity.MainLightSha256}" ||
                lighting.SourceIdentity.DeclaredGroupMask != record.Mask78 || lighting.FrameNumber != queue.FrameNumber ||
                lighting.Binding.ViewIdentity != queue.Binding.ViewIdentity ||
                lighting.Binding.CameraOwnerIdentity != queue.Binding.CameraOwnerIdentity ||
                !lighting.PreparedCameraPrefix.Span.SequenceEqual(primaryLighting.PreparedCameraPrefix.Span))
                throw new InvalidDataException("World F103 record does not own its exact declared Directional and same-frame Main Camera payload.");
            snapshots.Add(record.RecordOwnerIdentity, lighting.CopyAcceptedSnapshot());
        }
        // Prepare the counted View once. This seed feature is not submitted and
        // does not initialize the native loop's ordered kind cache.
        var seed = factory.PrepareFromWorldPopulation(population, queue, primaryLighting, common, context,
            publication, completeOtherCountsByRecordOwner, reflectionQuery, preparationIdentity + "/counted-View");
        var plan = DeferredF103WorldPassAssociations.Prepare(source, queue, seed.ViewFields, constructorFlags594Retained,
            common.AuxiliaryPresent, common.ViewGroupSelector);
        var passes = new List<MapSourceCreatedWorldF103Pass>();
        MapSourceCreatedF103FeaturePacket? previous = null;
        foreach (var association in plan.Associations)
        {
            var outer = snapshots[association.OuterRecord.RecordOwnerIdentity];
            var inner = snapshots[association.InnerRecord.RecordOwnerIdentity];
            if (outer.Binding != seed.ViewFields.Binding)
                throw new NotSupportedException("This initial counted View requires its selected primary outer record; another outer binding needs its own current View association.");
            var frame = factory.Prepare(population.MainCamera.Camera, outer, seed.ViewFields, common, context,
                reflectionQuery, preparationIdentity + "/pass/" + passes.Count, population, inner, previous);
            passes.Add(new(association, frame, inner)); previous = frame.Feature;
        }
        return new(seed.ViewFields, plan, Array.AsReadOnly(passes.ToArray()))
        {completeRecordLighting=new System.Collections.ObjectModel.ReadOnlyDictionary<string,MapSourceCreatedLightingPacket>(snapshots)};
    }
}
