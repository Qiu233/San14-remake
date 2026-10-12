namespace San14.Data;

/// <summary>One Main Sky instance's installed program and retained sampling
/// bindings. Changed physical inputs require a new device-resource instance.</summary>
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

    /// <summary>Compare images when a new package or resource generation arrives.
    /// Zero references retain this instance's existing bindings. Requested shader
    /// resources come from the caller's actual renderer-resource generation.</summary>
    public void RequireCompatibleGeneration(GameResourceRepository repository,
        SkyMainProgramResources requestedPrograms, MapSourceCreatedSeasonLightingFrame present)
    {
        Sampling.RequireCurrent();
        RequireResources(requestedPrograms);
        var package = Package(present);
        var groups = present.AcceptedSeasonCamera.Inputs.Source.CloudTextures;
        if (ReferenceEquals(package, acceptedPackage) && ReferenceEquals(groups, acceptedCloudTextures)) return;

        bindings.RequireCompatiblePackage(SkySeasonPackageTextures.Read(package),
            reference => SkySeasonPackageTextures.LoadSampling(repository, reference));
        var candidate = SkyAppliedCloudTextureSet.Import(groups);
        foreach (var input in media?.Textures.Where(input => input.Slot < 3) ?? Enumerable.Empty<D3D11MainSkyTextureInput>())
        {
            var replacement = candidate.StaInputs[checked((int)input.Slot)];
            if (input.Texture.Identity != replacement.Identity ||
                input.Texture.SamplerDescriptorSha256 != replacement.SamplerDescriptorSha256)
                throw new NotSupportedException("Changed Main Sky atmosphere images require scene resource replacement.");
        }
        acceptedPackage = package;
        acceptedCloudTextures = groups;
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
