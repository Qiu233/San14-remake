using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>The five concrete original Static sources requested by the default
/// class66 loader. These source owners retain encoded image bytes; actual device
/// readiness and the constructed source handle generation are supplied later.</summary>
public sealed class SourceResourceRegistrationGameDefault
{
    private readonly IReadOnlyDictionary<uint, StaticTextureBinding> resources;
    internal SourceResourceRegistrationGameDefault(StaticTextureBinding cube,
        IReadOnlyList<StaticTextureBinding> fixedSources, StaticTextureBinding lut)
    {
        AmbientCube = cube; FixedSources = Array.AsReadOnly(fixedSources.ToArray()); HdrLut = lut;
        var values = new Dictionary<uint, StaticTextureBinding>
        { [0x5A53AD58] = cube, [0x7D85DA95] = lut };
        foreach (StaticTextureBinding source in fixedSources)
            values.Add(source.Definition.ObjectId, source);
        resources = new ReadOnlyDictionary<uint, StaticTextureBinding>(values);
    }
    public StaticTextureBinding AmbientCube { get; }
    public IReadOnlyList<StaticTextureBinding> FixedSources { get; }
    public StaticTextureBinding HdrLut { get; }
    public IReadOnlyDictionary<uint, StaticTextureBinding> Resources => resources;
}

/// <summary>Explicit remake registration of the exact original five Static
/// declarations. This does not infer which original runtime source document was
/// loaded or turn source bytes into ready GPU resources.</summary>
public static class SourceResourceRegistration
{
    public static SourceResourceRegistrationGameDefault LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(SourceResourceRegistrationAmbientCube.LoadGameDefault(repository),
            SourceResourceRegistrationFixed.LoadGameDefault(repository),
            SourceResourceRegistrationLut.LoadGameDefault(repository));
    }

    public static void Validate(uint sourceObjectKey, StaticTextureBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Definition.ObjectId != sourceObjectKey)
            throw new InvalidDataException("Resource registration object key differs from its actual Static declaration.");
        switch (sourceObjectKey)
        {
            case SourceResourceRegistrationAmbientCube.ObjectKey:
                SourceResourceRegistrationAmbientCube.Validate(source); break;
            case SourceResourceRegistrationLut.ObjectKey:
                SourceResourceRegistrationLut.Validate(source); break;
            case 0x80F9DB7B or 0x7F97260C or 0x54F8C0DE:
                SourceResourceRegistrationFixed.Validate(source,
                    SourceResourceRegistrationFixed.Identities.Single(value => value.ObjectKey == sourceObjectKey)); break;
            default: throw new InvalidDataException("Default main-light resource registration has no other source keys.");
        }
    }

    public static string SourceProvenance(uint sourceObjectKey, StaticTextureBinding source)
    {
        Validate(sourceObjectKey, source);
        return $"{source.Definitions.Source.ResourceUri}#{source.Definition.ObjectId:X8}@{source.Definitions.Source.DecodedSha256};" +
            $"Static=FF7DBFD4;interface=B672FAA2;" +
            $"texture={source.Resource.Source.ResourceUri};TID={source.ImageIndex}@{source.Resource.Source.DecodedSha256}";
    }

    /// <summary>Associate a completed device generation's explicit receipts with
    /// these exact source owners, then construct the light seed. The device owns
    /// RID readiness checks; this adapter validates the complete source key set.</summary>
    public static MainLightConstructionPacket ConstructGameDefault(
        GameResourceRepository repository, SourceResourceRegistrationGameDefault resources,
        ReadOnlySpan<byte> explicitComplete10C0, MainLightConstructionInputs completedDeviceInputs)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(completedDeviceInputs);
        if (resources.Resources.Count != 5 || completedDeviceInputs.ResourceBindings.Count != 5)
            throw new InvalidDataException("Source-created main-light construction requires all five exact resource owners.");
        foreach (var pair in resources.Resources) Validate(pair.Key, pair.Value);
        foreach (MainLightConstructionResourceBinding ready in completedDeviceInputs.ResourceBindings)
        {
            if (!resources.Resources.TryGetValue(ready.SourceObjectKey, out StaticTextureBinding? resource) ||
                ready.DataHandle == 0 || !ready.CpuDataReady ||
                ready.InterfaceClassId != MainLightConstruction.EnvmapInterfaceClassId ||
                !ready.OwnerProvenance.Contains(resource.Definitions.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase) ||
                !ready.OwnerProvenance.Contains(resource.Resource.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Completed device receipt differs from its exact original Static and G1T owner.");
        }
        return MainLightConstruction.Apply(MainLightBindingSource.LoadGameDefault(repository),
            explicitComplete10C0, completedDeviceInputs);
    }
}
