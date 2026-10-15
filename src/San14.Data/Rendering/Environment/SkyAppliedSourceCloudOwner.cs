using System.Buffers.Binary;
using System.Numerics;
namespace San14.Data;

/// <summary>Actual current1193A90 color162/PS47 and finite same-frame Main
/// weather input. Animation is a pure once-per-frame1191B40 candidate until
/// the complete Host output commits. The explicit owned clock is not Clock254.</summary>
public sealed class SkyAppliedSourceCloudOwner
{
    private readonly GameResourceRepository repository;
    private readonly SkyAppliedSourceCloudSelectionOwner selectionOwner;
    private readonly SkyCloudSourceClipPlane clip;
    private readonly string native,locale;
    private SkyCloudAnimationState acceptedAnimation=SkyCloudAnimationState.Fresh;
    private SkyAppliedSourceCloudFrame? candidate;
    private long acceptedFrame;
    private MapOwnedGraphicsMaskPrefix? currentClipScope;
    public void BindCurrentClipScope(MapOwnedGraphicsMaskPrefix scope)
    { ArgumentNullException.ThrowIfNull(scope);scope.RequireCanonicalSkyDeclaredHookScope();currentClipScope=scope; }
    private SkyAppliedSourceCloudOwner(GameResourceRepository repo,string locale,string owner,
        SkyAppliedSourceCloudSelectionOwner selection,SkyCloudSourceClipPlane clip,string native)
    {repository=repo;this.locale=locale;OwnerIdentity=owner;selectionOwner=selection;this.clip=clip;this.native=native;}
    public string OwnerIdentity {get;}
    /// <summary>The privately created special-cache owner, borrowed by the
    /// actual complete final wrapper. It cannot be replaced by a caller.</summary>
    public SkyCloudSourceClipPlane ClipSource => clip;
    public SkyCloudSourceClipPlanePacket RequireCurrentClipPacket()
    {
        var frame=candidate??throw new InvalidDataException("Cloud has no current clip candidate.");
        frame.ValidateCandidate(frame.SourceFrame,frame.AcceptedPresent);
        if(frame.Clip.IsConstructorPlan)throw new InvalidDataException("Cloud constructor plan cannot identify an actual current clip submission.");
        return frame.Clip;
    }
    public static SkyAppliedSourceCloudOwner CreateFresh(GameResourceRepository repository,string locale,string ownedCloudIdentity,
        SkyCloudProgramResources programs)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedCloudIdentity);
        return new(repository,locale,ownedCloudIdentity,SkyAppliedSourceCloudSelectionOwner.CreateFresh(repository,locale,programs),
            SkyCloudSourceClipPlane.CreateFresh(repository,locale),RenderSamplerSourceAdapters.ReadAppliedCloudCurrentWriterNativeRecipe(repository.SourceDirectory,locale));
    }
    public SkyAppliedSourceCloudFrame PrepareBootstrap(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
        => Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,true);
    public SkyAppliedSourceCloudFrame PrepareCurrent(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
    {
        return Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,false);
    }
    /// <summary>A separate actual mode6 selected-source entry. The old
    /// mode2 zero-Moon/Fog0 gate remains on the original entry points.</summary>
    public SkyAppliedSourceCloudFrame PrepareDirectionalBootstrap(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,
        SkyDirectionalSelectedLightSource directional)
        =>Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,true,directional);
    public SkyAppliedSourceCloudFrame PrepareDirectionalCurrent(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,
        SkyDirectionalSelectedLightSource directional)
        =>Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,false,directional);
    private SkyAppliedSourceCloudFrame Prepare(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,bool constructorPlan,
        SkyDirectionalSelectedLightSource? directional=null)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);ArgumentNullException.ThrowIfNull(present);ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(selectedView);ArgumentException.ThrowIfNullOrWhiteSpace(ownedClockProvenance);
        selectedView.RequireSelectedView(present.FrameNumber,sourceFrame.Binding);
        var lighting=stages.AcceptedLighting;var selected=present.AcceptedSeasonCamera.Inputs.Source.Selection;
        stages.ValidateCurrent(lighting,selected,present.CurrentSkyCamera);
        if(!ReferenceEquals(stages.AcceptedPresent,present)||sourceFrame.FrameNumber!=present.FrameNumber||
            sourceFrame.Binding!=lighting.Binding||present.FrameNumber<=acceptedFrame||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=locale||
            !float.IsFinite(explicitOwnedClockDelta)||explicitOwnedClockDelta<0)
            throw new InvalidDataException("Cloud current writer requires the exact accepted frame, View and finite explicit owned clock.");
        var actualSource=present.AcceptedSeasonCamera.Inputs.Source;
        var textures=actualSource.CloudTextures;
        var selection=selectionOwner.PrepareCurrent(present,stages,textures);
        var bindings=SkyAppliedCloudTextureSet.Import(textures);
        SkyAppliedCloudTextureBindings.ValidateProgram(selection,bindings);
        var clipPacket=constructorPlan?clip.PrepareConstructorPlan(sourceFrame,present,stages,selectedView):
            clip.PrepareCurrent(currentClipScope??throw new InvalidDataException("Cloud current clip requires a typed owned frontend prefix."),sourceFrame,present,stages,selectedView);
        var weather=present.CloudWeather;
        var source=sourceFrame.SourceFrame;
        if(source.SixCameras.Count!=6||source.Controller is null||!source.Lights.SunPresent||!source.Lights.MoonPresent)
            throw new NotSupportedException("Cloud actual branch requires six source cameras and the complete current controller/light path.");
        if(directional is null&&(source.Lights.SourceZeroMoon is null||source.Atmosphere.Fog.SourceFogMode!=0||
            source.Atmosphere.Fog.RendererFogMode!=4||source.Atmosphere.Fog.Profile!=1||source.Atmosphere.Fog.Value!=Vector4.Zero))
            throw new NotSupportedException("Cloud actual branch requires six source cameras, complete current controller/Sun/zero-Moon and source Fog0.");
        if(directional is not null)
        {
            directional.ValidateCurrent();directional.ValidatePreparedLights(source.Lights);
            if(!ReferenceEquals(directional.AcceptedLighting,lighting))
                throw new InvalidDataException("Directional Cloud must use its actual same selected group80 lighting owner.");
            if(source.SourceSelectedAtmosphere is { } selectedAtmosphere)
            {
                selectedAtmosphere.ValidateFrame(sourceFrame,directional);selectedAtmosphere.ValidateStage(stages);
                selectedAtmosphere.ValidateAtmosphere(source.Atmosphere);
                (directional.CurrentColorSetterReceipt??throw new InvalidDataException("Current Directional Cloud lacks its actual D3 global color setter receipt."))
                    .ValidateFrame(sourceFrame,directional);
            }
            else
            {
            if(source.Atmosphere.Fog.SourceMode4Proof is null)
                throw new InvalidDataException("Directional Cloud requires its current Sky mode4 Fog/Scat owner before the renderer0/profile1 stage.");
            }
        }
        var sourceCamera=selected.FirstPackage.Camera;
        var geometryField=sourceCamera.Properties.Single(p=>p.Index==614);
        if(geometryField.NameHash!=0x59633E4E||geometryField.TypeCode!=8||geometryField.ElementCount!=1||geometryField.ElementSize!=4||geometryField.RawValue.Length!=4)
            throw new InvalidDataException("Cloud controller40 source614 geometry field changed.");
        float geometry40=selectedView.SelectedViewIndex==0?BinaryPrimitives.ReadSingleLittleEndian(geometryField.RawValue):-1;
        if(!float.IsFinite(geometry40)||geometry40==0)throw new InvalidDataException("Cloud controller current geometry40 is outside the finite source branch.");
        var lightBytes=lighting.LocalLightData.Span;
        Vector3 rawSun=new(BinaryPrimitives.ReadSingleLittleEndian(lightBytes.Slice(0xFA0,4)),BinaryPrimitives.ReadSingleLittleEndian(lightBytes.Slice(0xFA4,4)),BinaryPrimitives.ReadSingleLittleEndian(lightBytes.Slice(0xFA8,4)));
        //1195B14/44..65 reads controller50's attached Sun record+10 RGB;
        //1195BB5/BE3..BEF does the same for controller58's Moon. The current
        //D3 frame shares these exact global records after its color setters.
        if(directional?.CurrentColorSetterReceipt is { } colorReceipt)
        {colorReceipt.ValidateFrame(sourceFrame,directional);rawSun=colorReceipt.SunRgb;}
        if(directional is null&&source.Lights.RawMoonRgb!=Vector3.Zero)throw new NotSupportedException("Cloud current Moon branch requires its actual nonnull zero-RGB source setter.");
        var material=present.CloudMaterial;
        var animation=acceptedAnimation.Advance(material,explicitOwnedClockDelta);
        uint maximum=weather.Settings.MinimumRaySamples;
        if(maximum is <1 or >4096)throw new NotSupportedException("Current source ray samples exceed the finite GPU work contract.");
        var volume=material.PrepareVolume(animation);
        float planetRadius=actualSource.RequireRenderingAtmosphere().PlanetRadius;
        var packets=new SkyCloudConstantPacket[6];
        for(int face=0;face<6;face++)packets[face]=SkyCloudConstants.PrepareCurrentFace(selection.ProgramSelection,source,face,material,volume,planetRadius,maximum,clipPacket.Value,rawSun,geometry40);
        return candidate=new(this,sourceFrame,present,stages,selectedView,selection,bindings,clipPacket,packets,material,animation,volume,maximum,
            native+";"+weather.SourceProvenance+";"+clipPacket.Provenance+";"+ownedClockProvenance+
            ";same-frame1193A90-full-named-VS160/PS512;once1191B40-pure-phase-candidate;owner="+OwnerIdentity);
    }
    internal void RequireCandidate(SkyAppliedSourceCloudFrame frame)
    {if(!ReferenceEquals(candidate,frame))throw new InvalidDataException("Cloud candidate is stale, superseded or already committed.");}
    public void CommitCompleteFrame(SkyAppliedSourceCloudFrame frame)
    {RequireCandidate(frame);if(frame.Clip.IsConstructorPlan)throw new InvalidDataException("Cloud constructor plan cannot acknowledge an actual completed Host frame.");frame.ValidateCandidate(frame.SourceFrame,frame.AcceptedPresent);clip.RequireCompletedPacket(frame.Clip);acceptedAnimation=frame.Animation;acceptedFrame=frame.FrameNumber;candidate=null;}


}
public sealed class SkyAppliedSourceCloudFrame
{
    internal SkyAppliedSourceCloudOwner SourceCreatedInputOwner => owner;
    private readonly SkyAppliedSourceCloudOwner owner;private readonly SkyCloudConstantPacket[] packets;
    internal SkyAppliedSourceCloudFrame(SkyAppliedSourceCloudOwner owner,SkySceneSourceFramePacket source,MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,MainSceneNode51Execution view,SkyAppliedSourceCloudSelection selection,SkyAppliedCloudTextureSet bindings,SkyCloudSourceClipPlanePacket clip,SkyCloudConstantPacket[] packets,SkyCloudMaterial material,SkyCloudAnimationState animation,SkyCloudVolumeMaterial volume,uint maximumRaySamples,string provenance)
    {this.owner=owner;SourceFrame=source;AcceptedPresent=present;Stages=stages;SelectedView=view;Selection=selection;Bindings=bindings;Clip=clip;this.packets=packets;Material=material;Animation=animation;Volume=volume;MaximumRaySamples=maximumRaySamples;SourceProvenance=provenance;}
    public SkySceneSourceFramePacket SourceFrame {get;}
    public MapSourceCreatedSeasonLightingFrame AcceptedPresent {get;}
    public SkyAppliedSourceStageFrame Stages {get;}
    public MainSceneNode51Execution SelectedView {get;}
    public SkyAppliedSourceCloudSelection Selection {get;}
    public SkyAppliedCloudTextureSet Bindings {get;}
    public SkyCloudSourceClipPlanePacket Clip{get;}
    public SkyCloudMaterial Material {get;}
    public SkyCloudAnimationState Animation {get;}
    public SkyCloudVolumeMaterial Volume {get;}
    public uint MaximumRaySamples {get;}
    /// <summary>Borrow the current volume without advancing its clock or decoding a color buffer.
    /// Pattern uses its own camera and the actual atmosphere CPU's planet radius.</summary>
    public SkyCloudPatternMaterial PreparePatternMaterial(SkyAtmoTableCpuOwner atmosphere,
        SkyPatternPreparedCameras cameras,Vector4 actualClipPlane)
    {
        ArgumentNullException.ThrowIfNull(atmosphere);
        ValidateCandidate(SourceFrame,AcceptedPresent);
        atmosphere.RequireCurrent();
        if(!atmosphere.Source.Tables.SameResources(AcceptedPresent.AcceptedSeasonCamera.Inputs.Source.Transmittance.RequireTables()))
            throw new InvalidDataException("Pattern atmosphere belongs to another selected resource generation.");
        return new(cameras.PrepareVertexMaterial(actualClipPlane),
            Volume.PreparePixel(cameras.Temporary.Eye,cameras.Temporary.Lens.VerticalFovRadians,atmosphere.PlanetRadius58));
    }
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public string SourceProvenance {get;}
    public IReadOnlyList<SkyCloudConstantPacket> SixPackets=>Array.AsReadOnly(packets.Select(p=>p with{VertexBytes=p.VertexBytes.ToArray(),PixelBytes=p.PixelBytes.ToArray()}).ToArray());
    public void ValidateCandidate(SkySceneSourceFramePacket source,MapSourceCreatedSeasonLightingFrame present)
    {if(!ReferenceEquals(SourceFrame,source)||!ReferenceEquals(AcceptedPresent,present))throw new InvalidDataException("Cloud candidate belongs to another exact accepted frame.");SourceFrame.SourceFrame.SourceSelectedAtmosphere?.ValidateFrame(SourceFrame);SourceFrame.SourceFrame.SourceSelectedAtmosphere?.ValidateStage(Stages);Selection.ValidateCurrent(present,Stages);Clip.ValidateCurrent(source,present);owner.RequireCandidate(this);}
}



