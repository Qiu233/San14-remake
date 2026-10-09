using San14.Data;

/// <summary>Owns the finite fresh class18 lifecycle of these actual body and
/// flag preparations. Current culling is already in the World publication;
/// this owner certifies retained shader modifiers, not actor eligibility.</summary>
public sealed class ArmyWorldQueueCurrentSourceOwner : IArmyWorldQueueCurrentSource
{
    private readonly ArmyWorldQueuePreparedPackets body, flags;
    private readonly ArmyWorldPrimitivePublication publication;
    private readonly Dictionary<string, ArmyWorldQueuePrimitiveSource> sources;
    private readonly Action requireActualCurrentWorld;
    private bool retained = true;
    private readonly string identity = "owned-Army-class18:" + Guid.NewGuid().ToString("N");
    private ArmyWorldQueueCurrentSourceOwner(ArmyWorldQueuePreparedPackets body,
        ArmyWorldQueuePreparedPackets flags, ArmyWorldPrimitivePublication publication, Action current)
    {
        this.body = body; this.flags = flags; this.publication = publication; requireActualCurrentWorld = current;
        sources = body.Sources.Concat(flags.Sources).ToDictionary(s => s.PacketKey, StringComparer.Ordinal);
        if (!sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(publication.Primitives.Select(p => p.PacketKey).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Army renderer lifecycle requires the complete actual selected source primitive inventory.");
        RequireCurrent(body.Frame, publication);
    }
    /// <summary>Use only for the project's actual freshly created source
    /// renderer owners, with no external primitive/modifier/plugin assignments.
    /// Imports or another unhandled source writer invalidate the whole owner.</summary>
    public static ArmyWorldQueueCurrentSourceOwner CaptureFreshNoExternalAssignments(
        ArmyWorldQueuePreparedPackets body, ArmyWorldQueuePreparedPackets flags,
        ArmyWorldPrimitivePublication actualPublication, Action requireActualCurrentWorld)
    {
        ArgumentNullException.ThrowIfNull(body); ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(actualPublication); ArgumentNullException.ThrowIfNull(requireActualCurrentWorld);
        return new(body, flags, actualPublication, requireActualCurrentWorld);
    }
    public void MarkImportedEditedOrUnhandledNativeWriter(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); retained = false; }
    public void RequireCurrent(NativeMapArmyPreparedFrame actualArmy, ArmyWorldPrimitivePublication actualPublication)
    {
        requireActualCurrentWorld(); body.RequireCurrent(); flags.RequireCurrent();
        if (!retained || !ReferenceEquals(actualArmy, body.Frame) || !ReferenceEquals(flags.Frame, body.Frame) ||
            !ReferenceEquals(actualPublication, publication) || actualArmy.Context.FrameNumber != publication.Counts.Queue.FrameNumber)
            throw new InvalidDataException("Army shader/modifier lifecycle does not belong to this retained current owned World prefix.");
    }
    public ModelFreshOwnedStencilPublication ReadActualSelectedRenderer(ArmyWorldQueuePrimitiveCandidate candidate)
    {
        RequireCurrent(body.Frame, publication);
        if (!sources.TryGetValue(candidate.Primitive.PacketKey, out var actual) ||
            !publication.Primitives.Contains(candidate.Primitive) ||
            !candidate.Primitive.AdmittedRecordOwners.Contains(candidate.RecordOwnerIdentity, StringComparer.Ordinal) ||
            !ReferenceEquals(candidate.SelectedSource, candidate.QueueNodeIndex switch {49 => actual.Opaque49,50 => actual.Alpha50,_ => null}))
            throw new InvalidDataException("Army current selected renderer differs from this owning-record source candidate.");
        var selected = candidate.SelectedSource.SelectedRenderer;
        return new(publication.Counts.Queue.FrameNumber, publication.Counts.Queue.Binding.ViewIdentity,
            candidate.Primitive.PacketKey, identity + "/" + selected.OwnerObjectId.ToString("X8"),
            selected.ProviderObjectId, selected.ShaderSha256, true, true, true, true, true,
            identity + ";actual-fresh-owned-source-class18-constructor/registry-retained;" +
            "no-external-modifier/plugin/stencil-writer;" + publication.Counts.PublicationIdentity);
    }
}
