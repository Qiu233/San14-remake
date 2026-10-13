using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapOwnedGraphicsMaskCurrent(long FrameNumber, string ViewIdentity,
    string CurrentProducerProvenance);

/// <summary>One source frontend's RT0 entry state, not a GPU execution receipt.
/// A later prefix carries the previous successful source output's state.</summary>
public sealed class MapOwnedGraphicsMaskPrefix
{
    internal MapOwnedGraphicsMaskPrefix(MapOwnedGraphicsColorMaskHistorySource source,
        MapOwnedGraphicsMaskCurrent current, long previousCompletedFrame)
    { Source=source; Current=current; PreviousCompletedFrameNumber=previousCompletedFrame; }
    public MapOwnedGraphicsColorMaskHistorySource Source { get; }
    public MapOwnedGraphicsMaskCurrent Current { get; }
    public long FrameNumber => Current.FrameNumber;
    public string ViewIdentity => Current.ViewIdentity;
    public long PreviousCompletedFrameNumber { get; }
    public bool UsesConstructedFrontendState => PreviousCompletedFrameNumber==0;
    public uint ColorWriteMask => 15;
    public uint SkyStarNativeCullMode => 1;
    /// <summary>Typed declaration gate: exact canonical source chains and the
    /// retained owned attempt, including source extra-hook count zero. It is
    /// not a caller-supplied Boolean absence assertion.</summary>
    public void RequireCanonicalSkyDeclaredHookScope() => Source.RequireCurrentSkyDeclaredHookScope(this);
    /// <summary>Candidate validation after the same complete final call may
    /// use the exact last prefix until another prefix is prepared. This does
    /// not admit a new draw or change the current-only preparation gate.</summary>
    public void RequireCurrentOrJustCompletedSkyDeclaredHookScope() => Source.RequireCurrentOrJustCompletedSkyDeclaredHookScope(this);
    public string SourceProvenance => Source.SourceProvenance+";entry-from="+
        (UsesConstructedFrontendState?"95DC10/966EA0-constructor":$"completed-source-Host-frame{PreviousCompletedFrameNumber}")+
        ";"+Current.CurrentProducerProvenance;
}

/// <summary>Finite owned source Host route: source Sky/WORK shaders, fresh
/// declared hooks, and source Tone2/DOF0 complete final output. This class
/// replays source semantics only. A Game owner must acknowledge actual final
/// draw completion; allocation, packet preparation and readback are not that
/// acknowledgement. Unknown hooks/writers and interrupted attempts invalidate
/// the scope. Only RT0 is claimed; other model MRT masks are not inferred.</summary>
public sealed partial class MapOwnedGraphicsColorMaskHistorySource
{
    private readonly LoadedRdbAsset[] assets;
    private MapOwnedGraphicsMaskPrefix? pending;
    private MapOwnedGraphicsMaskPrefix? lastCompletedPrefix;
    private bool valid=true,started;
    private MapOwnedGraphicsColorMaskHistorySource(string locale,string ruleVersion,
        string frontend,string view,LoadedMapFinalOutputResources final,
        IEnumerable<LoadedRdbAsset> actual)
    { Locale=locale; RuleVersion=ruleVersion; OwnedFrontendIdentity=frontend; OwnedViewIdentity=view;
      FinalSource=final; assets=actual.ToArray(); }
    public string Locale { get; }
    public string RuleVersion { get; }
    public string OwnedFrontendIdentity { get; }
    public string OwnedViewIdentity { get; }
    public LoadedMapFinalOutputResources FinalSource { get; }
    public long CompletedSourceFrameNumber { get; private set; }
    public bool WorkSubmissionStarted => started;
    public string SourceProvenance => $"owned-source-RT0-mask/{Locale}/{RuleVersion};"+
        "Ctor95DC10/966EA0-RGBA15;SkyD3A850-Cull1;"+
        "Stars7180A527/Cloud42ED52ED/WORK19DBE085-zero-config-no-extra-hooks;"+
        "Sky2340AC335-HeightFog-v28/v38/v48-mask-preserving;"+
        "ExposureEA9B2B-opcode17000007-RGBA15;source-Host-Tone2/DOF0-SPK-retains-mask;"+OwnedFrontendIdentity;

    public static MapOwnedGraphicsColorMaskHistorySource ReadFresh(GameResourceRepository repository,
        string locale,string ownedFrontendIdentity,string ownedViewIdentity,
        LoadedMapFinalOutputResources actualFinalSource)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualFinalSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedFrontendIdentity);ArgumentException.ThrowIfNullOrWhiteSpace(ownedViewIdentity);
        if(locale is not("SC" or "TC"))throw new InvalidDataException("Owned graphics history requires SC or TC source.");
        var actual=new List<LoadedRdbAsset>();
        foreach(var (id,count) in new[]{
            (0x7180A527u,0), (0x340AC335u,1), (0x42ED52EDu,0), (0x19DBE085u,0)})
        {
            var shader=repository.LoadShaderContainer("system",id);Exact(shader.Source,$"san14://rdb/system/{id:X8}");
            var records=shader.ReadConfigurations().Records;
            if(records.Count!=count || (count==1 && (records[0].Name!="HeightFog" || records[0].RawSecondWord!=2)))
                throw new InvalidDataException("Owned Sky mask history requires the exact empty/HeightFog source configuration chains.");
            actual.Add(shader.Source);
        }
        Exact(actualFinalSource.Graph.Source,"san14://rdb/system/024496EE");
        Exact(actualFinalSource.PostEffect.Source,"san14://rdb/system/A1C83E34");
        foreach(var source in new[]{actualFinalSource.TextureObjects.Source,actualFinalSource.Parameters.Source,
            actualFinalSource.AuxiliaryShader.Source,actualFinalSource.StockScreen.Graph.Source})
        { actual.Add(source); }
        actual.Add(actualFinalSource.Graph.Source);actual.Add(actualFinalSource.PostEffect.Source);
        var graph=MapFinalOutputPassGraph.Read(actualFinalSource);
        if(graph.Passes.Count!=29 || graph.Passes[28].Program.VertexProgramIndex!=7 || graph.Passes[28].Program.PixelProgramIndex!=28)
            throw new InvalidDataException("Owned mask history requires the exact source Host SPK final pass.");
        return new(locale,RenderRuleIdentity.Version,
            ownedFrontendIdentity,ownedViewIdentity,actualFinalSource,actual);
    }

    public MapOwnedGraphicsMaskPrefix PrepareCurrentSkyPrefix(MapOwnedGraphicsMaskCurrent current)
    {
        ArgumentNullException.ThrowIfNull(current);RequireMutableSourceBoundary();
        if(started || current.FrameNumber<=CompletedSourceFrameNumber || current.FrameNumber<1 ||
            current.ViewIdentity!=OwnedViewIdentity || string.IsNullOrWhiteSpace(current.CurrentProducerProvenance))
            throw new InvalidDataException("Owned Sky prefix requires a new frame of this View and a completed preceding source attempt.");
        if(pending is not null)
        { if(pending.Current!=current)throw new InvalidDataException("Mask prefix is already prepared for another attempt.");return pending; }
        return pending=new(this,current,CompletedSourceFrameNumber);
    }
    public void BeginWorkSubmission(MapOwnedGraphicsMaskPrefix prefix)
    { RequireMutableSourceBoundary();RequirePrefix(prefix,false);started=true; }
    public void RequireCurrentSkyDeclaredHookScope(MapOwnedGraphicsMaskPrefix prefix)
    { RequirePrefix(prefix,started); }
    public void RequireCurrentOrJustCompletedSkyDeclaredHookScope(MapOwnedGraphicsMaskPrefix prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);RequireSource();
        if(ReferenceEquals(pending,prefix)){RequirePrefix(prefix,started);return;}
        if(pending is not null || started || !ReferenceEquals(lastCompletedPrefix,prefix) ||
            !ReferenceEquals(prefix.Source,this) || prefix.FrameNumber!=CompletedSourceFrameNumber)
            throw new InvalidDataException("Sky candidate prefix is neither current nor this exact just-completed source attempt.");
    }

    /// <summary>Replay only after the actual complete source Host output has
    /// succeeded. The production packet identifies Tone2/DOF0 full-viewport
    /// Exposure and the exact source final family. This is not a GPU receipt.</summary>
    public void ReplayCompletedSourceHostOutput(MapOwnedGraphicsMaskPrefix prefix,
        MapFinalOutputProductionPrepared actualCompletedFinal)
    {
        ValidateCompletedSourceHostOutput(prefix,actualCompletedFinal);
        // Actual Exposure writes RGB7/Alpha1. All admitted subsequent source
        // SPK/stock effect functions retain RT0. 11429D0 does not restore mask.
        CompletedSourceFrameNumber=prefix.FrameNumber;lastCompletedPrefix=prefix;pending=null;started=false;
        ReleaseAcceptedSourceSnapshot();
    }

    /// <summary>Validate without committing, so the same actual final receipt
    /// can acknowledge the finite mask and stock histories together.</summary>
    public void ValidateCompletedSourceHostOutput(MapOwnedGraphicsMaskPrefix prefix,
        MapFinalOutputProductionPrepared actualCompletedFinal)
    {
        ArgumentNullException.ThrowIfNull(actualCompletedFinal);RequireMutableSourceBoundary();RequirePrefix(prefix,true);
        var exposure=actualCompletedFinal.Exposure;
        if(actualCompletedFinal.FrameNumber!=prefix.FrameNumber || exposure.FrameNumber!=prefix.FrameNumber ||
            actualCompletedFinal.Camera.FrameNumber!=prefix.FrameNumber || exposure.ToneType3A4!=2 || exposure.HalfRectangle ||
            (exposure.ViewFlags8&((1u<<22)|(1u<<23)))!=0 ||
            actualCompletedFinal.Camera.Source.Declaration.DepthOfFieldMode!=0 ||
            !actualCompletedFinal.CurrentParameters.TryGetValue(25,out var dof) || dof.Length!=4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(dof.Span)!=0 ||
            string.IsNullOrWhiteSpace(actualCompletedFinal.SourceEvidence))
            throw new InvalidDataException("Mask history cannot acknowledge another frame or an unsupported final effect/Exposure route.");
    }
    /// <summary>Independent mode1 route: no A1 parameter25 or SPK execution is
    /// inferred. Unknown post-fallback callbacks prevent history advancement.</summary>
    public void ValidateCompletedSourceHostOutput(MapOwnedGraphicsMaskPrefix prefix,
        MapPostEffect3ApplyMode1Completed actualCompletedFinal)
    {
        ArgumentNullException.ThrowIfNull(actualCompletedFinal);RequireMutableSourceBoundary();RequirePrefix(prefix,true);
        actualCompletedFinal.Validate();
        if(actualCompletedFinal.FrameNumber!=prefix.FrameNumber||actualCompletedFinal.Camera.ExecutableLocale!=Locale||
            actualCompletedFinal.FallbackRt0ColorWriteMask!=prefix.ColorWriteMask)
            throw new InvalidDataException("Mode1 mask acknowledgement belongs to another frame/source frontend.");
        actualCompletedFinal.RequireCompletedSourceHostWriterScope();
    }
    public void ReplayCompletedSourceHostOutput(MapOwnedGraphicsMaskPrefix prefix,
        MapPostEffect3ApplyMode1Completed actualCompletedFinal)
    {
        ValidateCompletedSourceHostOutput(prefix,actualCompletedFinal);
        CompletedSourceFrameNumber=prefix.FrameNumber;lastCompletedPrefix=prefix;pending=null;started=false;
        ReleaseAcceptedSourceSnapshot();
    }
    public void MarkFailedOrUnknownWriter(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);valid=false;ReleaseAcceptedSourceSnapshot(); }
    private void RequirePrefix(MapOwnedGraphicsMaskPrefix prefix,bool expectedStarted)
    {
        ArgumentNullException.ThrowIfNull(prefix);RequireSource();
        if(started!=expectedStarted || !ReferenceEquals(pending,prefix) || !ReferenceEquals(prefix.Source,this))
            throw new InvalidDataException("Mask prefix is foreign, consumed or belongs to another current attempt.");
    }
    private void RequireSource()
    {
        if(acceptedSources is{} accepted){accepted.RequireCurrent();return;}
        RequireMutableSourceBoundary();
    }
    private static void Exact(LoadedRdbAsset source,string uri)
    { if(source.ResourceUri!=uri)throw new InvalidDataException("Mask source resource reference differs."); }
}
