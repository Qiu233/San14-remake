namespace San14.Data;

/// <summary>Scene-owned importer used only when a normal model asset is first
/// loaded. The model cache owns the returned dependency registration.</summary>
public sealed class D3D11SdfModelDeclarationPreparation
{
    private readonly Func<uint, SdfVolumeResourcePayload>? readActualReadyKind20;

    private D3D11SdfModelDeclarationPreparation(LoadedSdfVolumeResourceSource source,
        Func<uint, SdfVolumeResourcePayload>? readActualReadyKind20)
        => (Source, this.readActualReadyKind20) = (source, readActualReadyKind20);

    public LoadedSdfVolumeResourceSource Source { get; }

    public static D3D11SdfModelDeclarationPreparation Load(
        GameResourceRepository repository, string locale,
        Func<uint, SdfVolumeResourcePayload>? readActualReadyKind20 = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(SdfVolumeResourceSource.Load(repository, locale), readActualReadyKind20);
    }

    public D3D11SdfModelRegistration RegisterFreshOwnedModel(SceneModelReference actualLoadedModel)
    {
        ArgumentNullException.ThrowIfNull(actualLoadedModel);
        return new(Source.ImportModel(actualLoadedModel.Dictionary,
            actualLoadedModel.ModelObject.ObjectId), readActualReadyKind20);
    }
}

/// <summary>Actual Model dependency4 readiness, separate from ordinary geometry
/// readiness. Missing nonzero SDF media never means a registered null volume.</summary>
public sealed class D3D11SdfModelRegistration
{
    private readonly Func<uint, SdfVolumeResourcePayload>? readActualReadyKind20;
    private SdfModelVolumeRegistration? registered;

    internal D3D11SdfModelRegistration(LoadedSdfModelDeclaration source,
        Func<uint, SdfVolumeResourcePayload>? readActualReadyKind20)
    {
        Source = source;
        this.readActualReadyKind20 = readActualReadyKind20;
        // Source zero skips dependency4 entirely. Use the real constructor,
        // rather than assigning null for an unresolved nonzero declaration.
        if (source.SdfResourceHash == 0)
            registered = source.RegisterFreshOwnedModel(ReadReadyDependency);
    }

    public LoadedSdfModelDeclaration Source { get; }
    public bool IsDependencyReady => registered is not null;
    public string? UnresolvedOwner => IsDependencyReady ? null :
        "native-map.sdf.resource_kind20_ready:" + Source.SdfResourceHash.ToString("X8");

    /// <summary>Only the actual SDF collection branch requires the volume.
    /// Ordinary/shadow draws can retain unresolved SDF media independently.
    /// A real completed resource is registered once and keeps its own current
    /// lifetime checks. A failed dependency load remains retryable.</summary>
    public SdfVolumeResourceTexture? RequireCurrentVolumeD0()
    {
        registered ??= Source.RegisterFreshOwnedModel(ReadReadyDependency);
        registered.VolumeD0?.RequireCurrent();
        return registered.VolumeD0;
    }

    private SdfVolumeResourcePayload ReadReadyDependency(uint sourceHash)
        => readActualReadyKind20?.Invoke(sourceHash) ?? throw new NotSupportedException(
            "native-map.sdf.resource_kind20_reader:" + sourceHash.ToString("X8"));
}
