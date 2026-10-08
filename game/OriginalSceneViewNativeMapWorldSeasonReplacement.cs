using San14.Data;

/// <summary>Loaded replacement assets for one live World session. This owns
/// no renderer, clock, pixel history or presentation. The World package gate
/// drains the preceding request before installation.</summary>
public sealed class OriginalSceneViewNativeMapWorldSeasonReplacement
{
    private readonly OriginalSceneViewNativeMapWorldDependencies dependencies;
    private readonly OriginalSceneViewNativeMapSeasonResourceBundle previous;
    private readonly OriginalMapSourceCreatedDirectionalLightingDeviceOwner.PreparedSeasonSources lighting;
    private bool installed;
    private OriginalSceneViewNativeMapWorldSeasonReplacement(OriginalSceneViewNativeMapWorldDependencies dependencies,
        OriginalSceneViewNativeMapSeasonResourceBundle previous,OriginalSceneViewNativeMapSeasonResourceBundle next,
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner.PreparedSeasonSources lighting)
    {this.dependencies=dependencies;this.previous=previous;Resources=next;this.lighting=lighting;}
    public OriginalSceneViewNativeMapSeasonResourceBundle Resources{get;}
    public OriginalMapSeasonSelection Selection=>Resources.Selection;
    public bool IsInstalled=>installed;
    public bool ProvesMainCameraRefreshCompleted=>false;
    public bool ProvesGpuFrameCompleted=>false;

    public static async Task<OriginalSceneViewNativeMapWorldSeasonReplacement> PrepareAsync(
        OriginalSceneViewNativeMapWorldDependencies actualDependencies,OriginalSceneViewNativeMapFrameCapture actualCapture,
        OriginalMainMapContextCurrentPacket actualPreviousContext)
    {
        ArgumentNullException.ThrowIfNull(actualDependencies);ArgumentNullException.ThrowIfNull(actualCapture);
        ArgumentNullException.ThrowIfNull(actualPreviousContext);
        actualPreviousContext.Source.Validate(actualPreviousContext);
        if(!ReferenceEquals(actualPreviousContext.Source,actualDependencies.WorldSource.Bootstrap.First.Context.Source))
            throw new InvalidDataException("Season loading requires the same actual retained Host context owner.");
        var season=actualCapture.World.NativeMapSeason??throw new InvalidDataException("Season replacement requires the actual owned World third date.");
        var previous=actualDependencies.WorldSource.SeasonResources;
        string locale=actualCapture.MainCamera.Camera.ExecutableLocale;
        if(locale!=previous.Locale)throw new InvalidDataException("Changing executable locale requires its actual session source generation.");
        if(OriginalMapSeasonMath.SelectMonth(season.ThirdDate.Month)==previous.Selection)
            throw new InvalidOperationException("Unchanged resource selection uses the normal current Camera/calendar path.");
        var next=OriginalSceneViewNativeMapSeasonResourceBundle.Load(actualDependencies.Repository,season,locale,
            actualCapture.MainCamera.Camera.FrameNumber);
        actualDependencies.Sky.RequireCompatibleSeasonResources(next);
        var lighting=await actualDependencies.Lighting.PrepareSeasonSourcesAsync(actualDependencies.Repository,
            next.DirectionalDeclarations,actualCapture.MainCamera.Camera.Source,
            actualPreviousContext.Source.Host.ContextFlags3E4);
        return new(actualDependencies,previous,next,lighting);
    }

    /// <summary>Install only these prepared declarations/resources in their
    /// original owners. Main's loaded-Camera refresh is a separate actual
    /// caller writer; the next request still computes V468/Main/V418, all
    /// current CBs, and real HDR selection using this new source set.</summary>
    public void Install(OriginalSceneViewNativeMapWorldDependencies actualDependencies)
    {
        if(installed||!ReferenceEquals(dependencies,actualDependencies)||
            !ReferenceEquals(previous,dependencies.WorldSource.SeasonResources))
            throw new InvalidDataException("Season installation requires its same live session and unchanged preceding loaded bundle.");
        dependencies.Sky.RequireCompatibleSeasonResources(Resources);
        dependencies.Lighting.InstallSeasonSources(lighting);
        dependencies.Sky.InstallCompatibleSeasonResources(Resources);
        dependencies.WorldSource.InstallSeasonResources(Resources);
        installed=true;
    }
}
