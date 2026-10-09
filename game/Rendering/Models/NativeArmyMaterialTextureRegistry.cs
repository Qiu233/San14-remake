using San14.Data;
using San14.Data.Formats;

/// <summary>Retained CPU typed record of the actual texture-group producer.
/// Shader tables/providers have a different type and ownership domain.</summary>
public interface INativeArmyMaterialTextureTypedRecord { }

/// <summary>The actual current registered texture-group directory of this
/// owned renderer. Registration requires the real dependency owner and its
/// typed record. Source DOK membership alone never registers a resource.</summary>
public sealed class NativeArmyMaterialTextureRegistry
{
    private readonly Dictionary<uint, NativeArmyMaterialTextureResource> registered = [];
    private readonly HashSet<NativeArmyMaterialTextureResource> retained = [];
    private bool alive = true;

    /// <summary>At the actual texture-group creation/ready boundary. The
    /// typed record is an owned reference identity, independent of shader
    /// tables, resource IDs and pixel contents. A ready CPU getter may return
    /// null, so a present resource with a null record is explicitly supported.</summary>
    public NativeArmyMaterialTextureResource RegisterReadyTextureGroup(
        LoadedObjects actualObjects, DokObject actualTextureGroup,
        Func<INativeArmyMaterialTextureTypedRecord?> readActualTypedRecord,
        Action requireActualReadyResourceOwner)
    {
        ArgumentNullException.ThrowIfNull(actualObjects); ArgumentNullException.ThrowIfNull(actualTextureGroup);
        ArgumentNullException.ThrowIfNull(readActualTypedRecord);
        ArgumentNullException.ThrowIfNull(requireActualReadyResourceOwner);
        ObjectDisposedException.ThrowIf(!alive, this); requireActualReadyResourceOwner();
        if (actualTextureGroup.Kind != DokObjectKind.Inline || actualTextureGroup.ClassId != 0x3059B9C3 ||
            !actualObjects.Document.Objects.Any(o => ReferenceEquals(o, actualTextureGroup)))
            throw new InvalidDataException("Army texture registration requires the actual loaded texture-group declaration.");
        var resource = new NativeArmyMaterialTextureResource(this, actualObjects,
            actualTextureGroup, readActualTypedRecord, requireActualReadyResourceOwner);
        retained.Add(resource); registered[actualTextureGroup.ObjectId] = resource;
        return resource;
    }
    public NativeArmyMaterialTextureResource? LookupCurrentRegistered(uint actualObjectId)
    {
        ObjectDisposedException.ThrowIf(!alive, this);
        if (actualObjectId == 0 || !registered.TryGetValue(actualObjectId, out var resource)) return null;
        resource.RequireRetained(); return resource;
    }
    /// <summary>Remove this directory lookup. Materials that already retain
    /// this Data keep their reference until their actual owner releases it.</summary>
    public void Unregister(NativeArmyMaterialTextureResource actualResource)
    {
        RequireRetained(actualResource);
        if (registered.TryGetValue(actualResource.ObjectId, out var current) && ReferenceEquals(current, actualResource))
            registered.Remove(actualResource.ObjectId);
    }
    internal void RequireRetained(NativeArmyMaterialTextureResource actualResource)
    {
        if (!alive || !retained.Contains(actualResource))
            throw new InvalidDataException("Army texture-group Data was retired or belongs to another registered resource owner.");
    }
    public void Invalidate(string actualResourceWriter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualResourceWriter);
        alive = false; registered.Clear(); retained.Clear();
    }
}

/// <summary>One actual retained registered Data/CPU. Directory replacement
/// does not rewrite a material's prior Data pointer. Reading +8 is a real
/// resource-owner operation; caller-owned aliases keep the same object.</summary>
public sealed class NativeArmyMaterialTextureResource
{
    private readonly NativeArmyMaterialTextureRegistry registry;
    private readonly Action requireActualReadyOwner;
    private readonly Func<INativeArmyMaterialTextureTypedRecord?> readActualTypedRecord;
    internal NativeArmyMaterialTextureResource(NativeArmyMaterialTextureRegistry registry,
        LoadedObjects objects, DokObject group, Func<INativeArmyMaterialTextureTypedRecord?> readActualTypedRecord,
        Action requireActualReadyOwner)
        => (this.registry, Objects, Group, this.readActualTypedRecord, this.requireActualReadyOwner) =
            (registry, objects, group, readActualTypedRecord, requireActualReadyOwner);
    public LoadedObjects Objects { get; }
    public DokObject Group { get; }
    public uint ObjectId => Group.ObjectId;
    public INativeArmyMaterialTextureTypedRecord? ReadActualTypedRecord()
    { RequireRetained(); return readActualTypedRecord(); }
    public void RequireRetained()
    { registry.RequireRetained(this); requireActualReadyOwner(); }
}
