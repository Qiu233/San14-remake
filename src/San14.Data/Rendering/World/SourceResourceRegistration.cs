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

/// <summary>Explicit remake registration of the default light's five Static
/// declarations. This does not infer which original runtime source document was
/// loaded or turn source bytes into ready GPU resources.</summary>
public static class SourceResourceRegistration
{
    /// <summary>Reuse an already imported ambient cube when assembling the
    /// remaining resources for that device generation.</summary>
    public static SourceResourceRegistrationGameDefault LoadGameDefault(GameResourceRepository repository,
        StaticTextureBinding? ambientCube = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(ambientCube ?? SourceResourceRegistrationAmbientCube.LoadGameDefault(repository),
            SourceResourceRegistrationFixed.LoadGameDefault(repository),
            SourceResourceRegistrationLut.LoadGameDefault(repository));
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
