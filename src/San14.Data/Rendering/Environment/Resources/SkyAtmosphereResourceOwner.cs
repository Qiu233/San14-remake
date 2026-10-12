namespace San14.Data;

/// <summary>Installed atmosphere media and the two Sky sampling instances that
/// borrow it. Animation, render targets and completed pixels belong to Native.</summary>
public sealed class SkyAtmosphereResourceOwner : IDisposable
{
    private SkyAppliedCloudTextureSet images;
    private Replacement? pending;
    private bool disposed;

    public SkyAtmosphereResourceOwner(D3D11SkyAtmosphereResources atmosphere,
        SkyAppliedCloudTextureSet images, SkyMainResourceOwner mainSky, SkyMainResourceOwner? workSky)
    {
        Atmosphere = atmosphere;
        this.images = images;
        MainSky = mainSky;
        WorkSky = workSky;
    }

    public D3D11SkyAtmosphereResources Atmosphere { get; private set; }
    public SkyMainResourceOwner MainSky { get; private set; }
    public SkyMainResourceOwner? WorkSky { get; private set; }

    /// <summary>Attach WORK's independently retained sampling to the same
    /// atmosphere family during actual cold media preparation.</summary>
    public SkyMainResourceOwner PrepareWorkSky(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present, SkyMainProgramResources programs)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (WorkSky is not null)
            throw new InvalidOperationException("WORK Sky resources have already been prepared.");
        return WorkSky = SkyMainResourceOwner.CreateFresh(repository, present, programs, images);
    }

    /// <summary>Prepare actual replacement inputs without retiring installed
    /// sampling. A stable source returns no candidate and writes no files.</summary>
    public Replacement? PrepareReplacement(GameResourceRepository repository,
        SkyMainProgramResources programs, MapSourceCreatedSeasonLightingFrame present)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (pending is not null)
            throw new InvalidOperationException("Atmosphere resources already have an unconsumed candidate.");
        var source = present.AcceptedSeasonCamera.Inputs.Source;
        var nextImages = SkyAppliedCloudTextureSet.Import(source.CloudTextures);
        var atmosphere = Atmosphere.PrepareReplacement(source, nextImages);
        SkyMainResourceOwner? main = null, work = null;
        try
        {
            main = MainSky.PrepareReplacement(repository, programs, present, nextImages);
            work = WorkSky?.PrepareReplacement(repository, programs, present, nextImages);
            if (atmosphere is null && main is null && work is null &&
                (ReferenceEquals(images, nextImages) || images.SamePhysical(nextImages)))
                return null;
            return pending = new(this, atmosphere, nextImages, main, work);
        }
        catch
        {
            main?.Dispose();
            work?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pending?.Dispose();
        MainSky.Dispose();
        WorkSky?.Dispose();
    }

    /// <summary>A real prepared media candidate. Commit only after Native has
    /// installed the resources and completed the corresponding request.</summary>
    public sealed class Replacement : IDisposable
    {
        private readonly SkyAtmosphereResourceOwner owner;
        private readonly D3D11SkyAtmosphereResources? atmosphere;
        private readonly SkyAppliedCloudTextureSet images;
        private SkyMainResourceOwner? main, work;
        private bool finished;

        internal Replacement(SkyAtmosphereResourceOwner owner,
            D3D11SkyAtmosphereResources? atmosphere, SkyAppliedCloudTextureSet images,
            SkyMainResourceOwner? main, SkyMainResourceOwner? work)
        {
            this.owner = owner; this.atmosphere = atmosphere; this.images = images;
            this.main = main; this.work = work;
        }
        public SkyMainResourceOwner MainSky => main ?? owner.MainSky;
        public SkyMainResourceOwner? WorkSky => work ?? owner.WorkSky;

        public void WriteNativeInputs(string inputRoot, D3D11CloudRenderResources surfaces)
        {
            RequirePending();
            (atmosphere ?? owner.Atmosphere).WriteNativeInputs(inputRoot);
            surfaces.WriteImageInputs(inputRoot, D3D11CloudImageResources.Prepare(images, MainSky.Sampling));
            if ((atmosphere ?? owner.Atmosphere).HasRenderingAtmosphere)
            {
                MainSky.Media.WriteNativeInputs(Path.Combine(inputRoot, "main-sky-input"));
                WorkSky!.Media.WriteNativeInputs(Path.Combine(inputRoot, "ambient-input", "sky"));
            }
        }

        public void Commit()
        {
            RequirePending();
            if (main is not null)
            {
                var previous = owner.MainSky; owner.MainSky = main; main = null; previous.Dispose();
            }
            if (work is not null)
            {
                var previous = owner.WorkSky; owner.WorkSky = work; work = null; previous?.Dispose();
            }
            owner.Atmosphere = atmosphere ?? owner.Atmosphere;
            owner.images = images;
            owner.pending = null;
            finished = true;
        }

        public void Dispose()
        {
            if (finished) return;
            main?.Dispose(); work?.Dispose(); main = null; work = null;
            if (ReferenceEquals(owner.pending, this)) owner.pending = null;
            finished = true;
        }

        private void RequirePending()
        {
            ObjectDisposedException.ThrowIf(finished || owner.disposed, this);
            if (!ReferenceEquals(owner.pending, this))
                throw new InvalidOperationException("Atmosphere candidate does not belong to the installed owner.");
        }
    }
}
