using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The finite numerical tail of one actual Directional kind1
/// publication. This supplies neither its Shadow CPU nor its caster inventory.</summary>
public sealed class OriginalDeferredEarlyShadowRecordNumericFields
{
    internal OriginalDeferredEarlyShadowRecordNumericFields(OriginalMapWorldKind1CurrentRecord record,
        object actorOwner, OriginalDirectionalLightConstructorMetadata metadata, string evidence)
    {
        Record = record; ActorOwner = actorOwner;
        Record7C = metadata.Value3C; Record80 = metadata.Value40; Record84 = metadata.Value44;
        FirstFamilyCounterCapacity = OriginalDeferredEarlyShadowRecordProducer.ReadFirstFamilyCounterCapacity(record.FirstCounterCapacity);
        SourceEvidence = evidence;
    }
    public OriginalMapWorldKind1CurrentRecord Record { get; }
    public object ActorOwner { get; }
    public uint Record7C { get; }
    public uint Record80 { get; }
    public uint Record84 { get; }
    /// <summary>CFD34F..367's distinct ordinary-capacity-derived counter bound.
    /// This is not Record7C, an accepted count or an inventory publication.</summary>
    public uint FirstFamilyCounterCapacity { get; }
    public string SourceEvidence { get; }
    public bool HasShadowConstructorInput => Record7C != 0;

    /// <summary>D067A0's three actual tail stores, preserving all other bytes.
    /// The caller owns the record allocation and subsequent publication order.</summary>
    public void WriteOwnedRecordNumericTail(Span<byte> actualOwnedRecord)
    {
        if (actualOwnedRecord.Length < 0x88)
            throw new ArgumentException("Kind1 numeric tail needs the actual record through offset84.", nameof(actualOwnedRecord));
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedRecord[0x7C..], Record7C);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedRecord[0x80..], Record80);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedRecord[0x84..], Record84);
    }
}

/// <summary>CE3870 -> D653F0 -> D65620 -> CFD2D0 -> D067A0's finite
/// source-retained Directional actor fields. Provider-selected CPU ownership
/// remains distinct from registered-root ownership and ordinary draw counts.</summary>
public static class OriginalDeferredEarlyShadowRecordProducer
{
    public const int Record7CSourcePropertyOrdinal = 160;
    public const uint Record7CSourcePropertyHash = 0x6F486116;
    public const string PublicationEvidence = "CE3A88/CE3B80:selectedCPU3C;D65530;D6571A;CFD343/CFD8E7;D06828:record7C;" +
        "selectedCPU40/44->D06832/D0683F:record80/84;not-ordinary-capacity/not-caster-count";

    public static uint ReadFirstFamilyCounterCapacity(int actualOrdinaryCapacity)
    {
        if (actualOrdinaryCapacity < 0) throw new ArgumentOutOfRangeException(nameof(actualOrdinaryCapacity));
        return Math.Max(64u, (uint)actualOrdinaryCapacity >> 8);
    }

    /// <summary>Direct CE3870 receiver, retaining its constructed numeric CPU
    /// fields. Call this only for the direct actor publication branch.</summary>
    public static OriginalDeferredEarlyShadowRecordNumericFields FromDirectSourceRetainedActor(
        OriginalMapWorldKind1CurrentRecord record, OriginalDirectionalLightConstructionPacket actualActor)
    {
        RequireRegisteredSource(record, actualActor);
        return new(record, actualActor, actualActor.Metadata, PublicationEvidence + ";direct-constructed-actor");
    }

    /// <summary>CE35F0/D1A4B0's selected ready source actor. Registered root
    /// identifies the record; selected CPU supplies3C/40/44.</summary>
    public static OriginalDeferredEarlyShadowRecordNumericFields FromSourceRetainedProvider(
        OriginalMapWorldKind1CurrentRecord record, OriginalDirectionalProviderCurrentFrame actualProvider)
    {
        ArgumentNullException.ThrowIfNull(actualProvider);
        RequireRegisteredSource(record, actualProvider.RegisteredRoot);
        return new(record, actualProvider, actualProvider.SelectedConstruction.Metadata,
            PublicationEvidence + ";CE35F0/D1A4B0-selected-ready-actor");
    }

    /// <summary>The exact current zero-pair temporary scope already produced
    /// by CDC980: temp3C/40/44 are copied from the registered root. No generic
    /// interpolation or external Camera provider branch is inferred.</summary>
    public static OriginalDeferredEarlyShadowRecordNumericFields FromFreshZeroPairTemporary(
        OriginalMapWorldKind1CurrentRecord record, OriginalDirectionalProviderCallerCurrentFrame actualProvider)
    {
        ArgumentNullException.ThrowIfNull(actualProvider);
        RequireRegisteredSource(record, actualProvider.RegisteredRoot);
        return new(record, actualProvider.Temporary, actualProvider.RegisteredRoot.Metadata,
            PublicationEvidence + ";CDC980-zero-pair:temp3C/40/44<-root3C/40/44");
    }

    private static void RequireRegisteredSource(OriginalMapWorldKind1CurrentRecord record,
        OriginalDirectionalLightConstructionPacket actor)
    {
        ArgumentNullException.ThrowIfNull(record); ArgumentNullException.ThrowIfNull(actor);
        if (record.Source.SourceObjectId != actor.SourceObjectId || record.Source.SourceDeclarationIdentity !=
            $"{actor.SourceUri}#{actor.SourceObjectId:X8}@{actor.SourceSha256}")
            throw new InvalidDataException("Directional record and registered actor belong to different imported source owners.");
    }
}

public sealed partial class OriginalMapSourceCreatedLightingPacket
{
    /// <summary>Read the finite source-retained current actor fields already
    /// owned by this accepted packet. This does not construct Shadow240/130,
    /// publish a caster list, or stand in for the whole prepared Scene View.</summary>
    public OriginalDeferredEarlyShadowRecordNumericFields CaptureRetainedDirectionalRecordNumericFields(
        OriginalMapWorldKind1CurrentRecord actualRecord)
    {
        if (directionalConstruction is null)
            throw new NotSupportedException("This finite record producer requires the accepted Directional actor.");
        return directionalProviderFrame is { } provider
            ? OriginalDeferredEarlyShadowRecordProducer.FromFreshZeroPairTemporary(actualRecord, provider)
            : OriginalDeferredEarlyShadowRecordProducer.FromDirectSourceRetainedActor(actualRecord, directionalConstruction);
    }
}
