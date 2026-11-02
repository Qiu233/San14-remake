using San14.Core;

namespace San14.Data;

/// <summary>Loaded ordinary Main V468 direct packages. Calendar and actual
/// mode are current inputs; immutable resource descriptors survive mode changes.</summary>
public sealed class MapSeasonMainCameraModeSources
{
    private readonly SkySeasonMainCameraSource[] sources;
    public MapSeasonMainCameraModeSources(GameResourceRepository repository,SkySeasonMainCameraSource root)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(root);
        if(root.Selection.FirstIndex!=0||root.Selection.SecondPackage is not null||
            root.Selection.Provider.ExternalSelectorObjectId!=0)
            throw new NotSupportedException("Ordinary Main mode packages require the actual direct root graph without external selector.");
        sources=new SkySeasonMainCameraSource[5];sources[0]=root;
        for(int mode=1;mode<sources.Length;mode++)
        {
            var selection=MapSeasonCameraApplication.SelectDeclaredPackage(root.Selection.Provider,mode);
            sources[mode]=root.Season.Calendar is MapSeasonRuntimeFrame
                ?SkySeasonMainCameraProducer.ResolveOwnedSource(repository,root.Season,root.Transmittance.Atan.Locale,selection)
                :SkySeasonMainCameraProducer.ResolveScenarioPreviewSource(repository,root.Season,root.Transmittance.Atan.Locale,selection);
        }
        foreach(var source in sources)
        {
            // These immutable declarations join the load boundary too. A mode
            // switch must not become their first parser/validation consumer.
            _=source.CloudWeatherSettings;_=source.WeatherProfile;
        }
        RequireSharedPhysicalInputs(repository);
    }
    private void RequireSharedPhysicalInputs(GameResourceRepository repository)
    {
        var root=sources[0];
        var images=SkyAppliedCloudTextureSet.Import(root.CloudTextures);
        var sampling=SkySeasonPackageTextures.Read(root.Selection.FirstPackage);
        var stars=SkyStarResources.Load(repository,NamedViewRendererResources.Load(repository),root.Selection.FirstPackage);
        foreach(var selected in sources.Skip(1))
        {
            var candidate=SkyAppliedCloudTextureSet.Import(selected.CloudTextures);
            // These are the exact imported image/view/sampler identities used
            // by opening's Cloud and all selected atmosphere Plane bindings.
            if(!images.SamePhysical(candidate)||root.Controller.DeclaredTransmittanceTable!=selected.Controller.DeclaredTransmittanceTable||
                root.CloudTextures.DeclaredFallbackPlane!=selected.CloudTextures.DeclaredFallbackPlane||
                (root.CloudTextures.SkylightSta is null)!=(selected.CloudTextures.SkylightSta is null)||
                !SameAtmosphere(root.RenderingAtmosphere,selected.RenderingAtmosphere))
                throw new NotSupportedException("Main modes change physical Cloud/atmosphere inputs and require a preloaded resource bank with actual selection.");
            var next=SkySeasonPackageTextures.Read(selected.Selection.FirstPackage);
            if(!SameSampling(sampling.Earth,next.Earth)||!SameSampling(sampling.Moon,next.Moon)||
                !SameSampling(sampling.MilkyWay,next.MilkyWay)||!SameSampling(sampling.Star,next.Star))
                throw new NotSupportedException("Main modes change physical Sky sampling and require a preloaded resource bank with actual selection.");
            stars?.RequireCompatiblePackage(repository,selected.Selection.FirstPackage);
        }
        bool SameSampling(SkySamplingReference first,SkySamplingReference second)
        {
            if(first.IsSourceReferenceZero||second.IsSourceReferenceZero)
                return first.IsSourceReferenceZero&&second.IsSourceReferenceZero;
            var left=SkySeasonPackageTextures.LoadSampling(repository,first).Texture;
            var right=SkySeasonPackageTextures.LoadSampling(repository,second).Texture;
            return left.Identity==right.Identity&&left.SamplerDescriptorSha256==right.SamplerDescriptorSha256;
        }
        static bool SameAtmosphere(LoadedSkyAtmoTableSources? first,LoadedSkyAtmoTableSources? second)
            =>ReferenceEquals(first,second)||first is not null&&second is not null&&
                first.DefinitionResourceUri==second.DefinitionResourceUri&&first.DefinitionSha256==second.DefinitionSha256&&first.SameResources(second);
    }
    public SkySeasonMainCameraSource Prepare(MapSeasonCalendarFrame calendar,MainMapCameraModePacket mode)
    {
        ArgumentNullException.ThrowIfNull(calendar);ArgumentNullException.ThrowIfNull(mode);
        if((uint)mode.Mode288>=(uint)sources.Length)
            throw new NotSupportedException("Main mode requires another actual source package branch.");
        if(calendar.Selection!=sources[0].Season.Resources.Selection)
            throw new InvalidDataException("Main mode packages belong to another loaded season.");
        return sources[mode.Mode288].ApplySuccessfulMainMode(calendar,mode);
    }
    public void AdoptStartedCampaignCalendar(NativeMapSeasonState actual,MainMapCameraFrame main)
    {
        // Prepare all five transfers before publishing any new calendar role.
        var adopted=sources.Select(source=>source.AdoptStartedCampaignCalendar(actual,main)).ToArray();
        adopted.CopyTo(sources,0);
    }
}
