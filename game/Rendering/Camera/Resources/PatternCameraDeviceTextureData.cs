using San14.Data;

/// <summary>Actual Static device/CPU wrapper published by its existing ready
/// owner. It borrows the same texture and sampler, with no second upload.</summary>
public sealed class PatternCameraDeviceTextureData : IPatternCameraCurrentNamedData
{
    private readonly object actualOwner;
    private readonly Action requireOwner;
    internal PatternCameraDeviceTextureData(SourceResourceRegistrationDeviceEntry entry,
        object actualReadyOwner, Action requireActualOwner)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        actualOwner = actualReadyOwner ?? throw new ArgumentNullException(nameof(actualReadyOwner));
        requireOwner = requireActualOwner ?? throw new ArgumentNullException(nameof(requireActualOwner));
        if (entry.Source.Definition.ClassId != SourceResourceRegistrationFixed.StaticClassId ||
            entry.SourceObjectKey != entry.Source.Definition.ObjectId || !entry.TextureRid.IsValid || !entry.SamplerRid.IsValid)
            throw new InvalidDataException("Pattern Static Data requires its actual created resource wrapper and sampler.");
        RequireCurrent();
    }
    public SourceResourceRegistrationDeviceEntry Entry { get; }
    public uint ObjectKey => Entry.SourceObjectKey;
    public uint FactoryClassId => SourceResourceRegistrationFixed.StaticClassId;
    public object CurrentDataOwner => actualOwner;
    public string SourceEvidence => Entry.OwnerProvenance + ";borrowed-actual-Static-ready-Data";
    public void RequireCurrent() => requireOwner();
}
