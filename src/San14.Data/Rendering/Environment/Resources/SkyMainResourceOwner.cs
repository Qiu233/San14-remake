namespace San14.Data;

/// <summary>One Sky instance's installed program and retained sampling bindings.
/// Main and WORK own separate instances and replace them after device commit.</summary>
public sealed class SkyMainResourceOwner : IDisposable
{
    private readonly Sky2TextureBindings bindings;
    private readonly SkyAppliedCloudTextureSet atmosphere;
    private readonly SkyMainProgramResources resources;
    private MapSeasonCameraPackage acceptedPackage;
    private SkyAppliedSeasonCloudTextureSources acceptedCloudTextures;
    private D3D11MainSkyInputPreparation? media;

    private SkyMainResourceOwner(SkyMainProgramResources resources,
        SkyAppliedCloudTextureSet atmosphere, MapSeasonCameraPackage package,
        Sky2TextureBindings bindings, SkyAppliedSeasonCloudTextureSources groups)
    {
        this.resources = resources;
        this.atmosphere = atmosphere;
        acceptedPackage = package;
        acceptedCloudTextures = groups;
        this.bindings = bindings;
        Sampling = bindings.Capture();
    }

    public Sky2TextureBindingSnapshot Sampling { get; }
    public SkyMainProgramSelection Program => Media.Program;
    public D3D11MainSkyInputPreparation Media => media ??
        throw new InvalidOperationException("Main Sky media has not been prepared from its actual selected program.");

    public static SkyMainResourceOwner CreateFresh(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present, SkyMainProgramResources programs,
        SkyAppliedCloudTextureSet atmosphere)
    {
        var package = Package(present);
        var bindings = new Sky2TextureBindings();
        try
        {
            bindings.ApplyPackage(SkySeasonPackageTextures.Read(package),
                reference => SkySeasonPackageTextures.LoadSampling(repository, reference));
            return new(programs, atmosphere, package, bindings, present.AcceptedSeasonCamera.Inputs.Source.CloudTextures);
        }
        catch
        {
            bindings.Dispose();
            throw;
        }
    }

    /// <summary>Use the selection already produced by numerical preparation.
    /// The media writer and initial material therefore share one actual owner.</summary>
    public D3D11MainSkyInputPreparation Prepare(SkyMainProgramSelection selection)
    {
        Sampling.RequireCurrent();
        RequireResources(selection.Resources);
        if (media is null) media = D3D11MainSkyInputPreparation.Load(selection, atmosphere, Sampling);
        else RequireCurrent(selection);
        return media;
    }

    /// <summary>Prepare new physical inputs without retiring the installed Sky.
    /// Zero package references inherit its existing Earth, Moon and Milky Way.
    /// A Plane-only change also receives an independent sampling lifetime.</summary>
    public SkyMainResourceOwner? PrepareReplacement(GameResourceRepository repository,
        SkyMainProgramResources requestedPrograms, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedCloudTextureSet images)
    {
        Sampling.RequireCurrent();
        RequireResources(requestedPrograms);
        var package = Package(present);
        var groups = present.AcceptedSeasonCamera.Inputs.Source.CloudTextures;
        if (ReferenceEquals(package, acceptedPackage) && ReferenceEquals(groups, acceptedCloudTextures) &&
            (ReferenceEquals(images, atmosphere) || atmosphere.SameSta(images))) return null;

        bool planeChanged = !atmosphere.SameSta(images);
        var replacementBindings = bindings.PrepareReplacement(SkySeasonPackageTextures.Read(package),
            reference => SkySeasonPackageTextures.LoadSampling(repository, reference), planeChanged);
        if (replacementBindings is null)
        {
            acceptedPackage = package;
            acceptedCloudTextures = groups;
            return null;
        }
        var replacement = new SkyMainResourceOwner(requestedPrograms, images, package, replacementBindings, groups);
        try
        {
            if (media is not null) replacement.Prepare(media.Program);
            return replacement;
        }
        catch
        {
            replacement.Dispose();
            throw;
        }
    }

    /// <summary>Current material must target the installed vertex and pixel
    /// programs. Different combination rows can select the same device pair.</summary>
    public void RequireCurrent(SkyMainProgramSelection selection)
    {
        Sampling.RequireCurrent();
        RequireResources(selection.Resources);
        if (selection.Programs.VertexProgram != Program.Programs.VertexProgram ||
            selection.Programs.PixelProgram != Program.Programs.PixelProgram)
            throw new NotSupportedException("Changed Main Sky program selection requires scene resource replacement.");
    }

    public void Dispose() => bindings.Dispose();

    private void RequireResources(SkyMainProgramResources requested)
    {
        var installed = resources.Shader.Source;
        var candidate = requested.Shader.Source;
        if (installed.ResourceUri != candidate.ResourceUri || installed.DecodedSha256 != candidate.DecodedSha256)
            throw new NotSupportedException("Changed Main Sky shader resources require scene resource replacement.");
    }

    private static MapSeasonCameraPackage Package(MapSourceCreatedSeasonLightingFrame present)
    {
        var selection = present.AcceptedSeasonCamera.Inputs.Source.Selection;
        if (selection.SecondPackage is not null)
            throw new NotSupportedException("Main Sky resources currently require a direct selected season package.");
        return selection.FirstPackage;
    }
}
