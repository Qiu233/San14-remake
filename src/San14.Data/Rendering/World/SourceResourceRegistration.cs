namespace San14.Data;

/// <summary>The five concrete original Static sources requested by the default
/// class66 loader. These source owners retain encoded image bytes; actual device
/// readiness and the constructed source handle generation are supplied later.</summary>
public sealed class SourceResourceRegistrationGameDefault
{
    internal SourceResourceRegistrationGameDefault(StaticTextureBinding cube,
        IReadOnlyList<StaticTextureBinding> fixedSources, StaticTextureBinding lut)
    {
        AmbientCube = cube; FixedSources = Array.AsReadOnly(fixedSources.ToArray()); HdrLut = lut;
    }
    public StaticTextureBinding AmbientCube { get; }
    public IReadOnlyList<StaticTextureBinding> FixedSources { get; }
    public StaticTextureBinding HdrLut { get; }
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
}
