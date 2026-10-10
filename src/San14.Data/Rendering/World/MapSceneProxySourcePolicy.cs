using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual node1 cold Scene proxy selection. EA9300 reads the selected
/// prepared View at Q110[n], and the independent Common Aux at Q240. The
/// terminal is a named original texture, not a numerically overridden Scene.
/// This finite owner has no retained proxy or intervening View writer.</summary>
public sealed class MapSceneProxySourcePolicy
{
    private readonly uint flags8, flags0C;
    private readonly string cameraUri, cameraSha;
    private readonly LoadedMapSceneProxyNativeSource native;
    private MapSceneProxySourcePolicy(uint f8,uint f0C,string uri,string sha,
        uint? selected,RenderTextureResolutionFrame resolutions,string evidence,
        LoadedMapSceneProxyNativeSource native)
    { flags8=f8;flags0C=f0C;cameraUri=uri;cameraSha=sha;
      this.native=native;SelectedTerminalObjectId=selected;Resolutions=resolutions;SourceEvidence=evidence; }
    public string Locale=>native.Locale;
    public string RuleVersion=>native.RuleVersion;
    public uint? SelectedTerminalObjectId { get; }
    public RenderTextureResolutionFrame Resolutions { get; }
    public string SourceEvidence { get; }

    /// <summary>Called before World allocations with the already constructed
    /// bootstrap World. Its prepared Camera supplies View1CC through the exact
    /// D13F21/D12830/D06FA0/D02C60 copy. This creates remake-owned resources;
    /// it makes no assertion about an attachment in the original process.</summary>
    public static MapSceneProxySourcePolicy LoadFreshWorld(
        GameResourceRepository repository,MapSourceCreatedF103CurrentFrame actualWorld,
        string actualAllocationGenerationIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualWorld);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualAllocationGenerationIdentity);
        var defaults=DefaultView0Source.LoadDeclarationDefaults(repository);
        var camera=actualWorld.Lighting.CapturePreparedCameraFromSameAcceptedMain(actualWorld.Camera);
        var native=RenderSamplerSourceAdapters.LoadMapSceneProxySource(repository.SourceDirectory,camera.ExecutableLocale);
        var template=CommonRenderResources.ImportSettings(repository.LoadObjects("KIDSSystemResource",0x6D011726));
        RequireFresh(camera,actualWorld.Common,defaults,template.Flags8,template.Flags0C,native.Locale);
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        const string graphSha="B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
        if(graph.Source.DecodedSha256!=graphSha||Convert.ToHexString(SHA256.HashData(graph.Source.Bytes))!=graphSha||
            graph.Document.Nodes.Count!=211||graph.Document.Nodes[1].Call.FactoryClassId!=0x5F0A8446||
            !graph.Document.Nodes[0].Call.ConnectedNodeIndices.SequenceEqual(new uint[]{1}))
            throw new InvalidDataException("Scene proxy requires the actual entry-to-node1 render graph.");
        var node=graph.Document.Nodes[1];
        byte[] A(uint hash,uint descriptor,int size)
        {
            var a=node.Call.Arguments.Single(x=>x.NameHash==hash);
            if(a.DescriptorWord!=descriptor||a.RawValue.Length!=size)
                throw new InvalidDataException("Scene proxy node1 argument differs from its exact typed loader.");
            return a.RawValue;
        }
        uint W(uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(A(hash,0x05000001,4));
        if(A(0x9075EDA0,0x01000001,1)[0]!=0||W(0x72DB7EBD)!=defaults.ViewObjectId||W(0x51332A47)!=0x064B746B)
            throw new InvalidDataException("Scene proxy caller is not the declared Main0 Scene input.");
        uint high=W(0x514B2AC3),normal=W(0x0EBBA230),disabled=W(0xD776D144);
        if(high!=0x087260E1||normal!=0x90B366D4||disabled!=0x636525C6)
            throw new InvalidDataException("Scene proxy named target inputs changed.");
        var textures=repository.LoadObjects("system",0x93FB3280);
        const string textureSha="4082FCB18D13F992407B2168F0105B9C86F53D095E2BA630802AD0BCDF19FF12";
        if(textures.Source.DecodedSha256!=textureSha||Convert.ToHexString(SHA256.HashData(textures.Source.Bytes))!=textureSha)
            throw new InvalidDataException("Scene named proxy descriptors differ from their original source bytes.");
        // EA9472..EA949A: Common8.bit23 and negative View1CC clears C8.
        // Fresh C8 is null, so this branch retains the declared Scene.
        int actualSelector=BinaryPrimitives.ReadInt32LittleEndian(camera.Prefix.Span.Slice(0x1CC,4));
        uint? selected=ReadSourceCreatedFreshTerminal(actualSelector,template.Flags8,template.Flags0C,high,normal,disabled);
        // This generation is allocated by NativeMapScene's actual node53/73
        // attachment owners. Unselected candidates (including native03 and
        // the independent FloatRGBA128 Exposure target) are not allocations
        // in that generation and must not demand a device-format recipe.
        var attachments=new[]{RenderGraphTargetResources.Read(graph,53,textures),
            RenderGraphTargetResources.Read(graph,73,textures)};
        var required=attachments.SelectMany(t=>t.ColorTargets.Select(c=>c.Texture)
            .Concat(t.DepthTarget is null?Array.Empty<RenderTextureDefinition>():new[]{t.DepthTarget!}));
        var declarations=required.Concat(selected is uint terminalKey?
            new[]{RenderTextureResources.Read(textures,terminalKey)}:Array.Empty<RenderTextureDefinition>())
            .DistinctBy(t=>t.Object.ObjectId).ToArray();
        var scene=declarations.Single(t=>t.Object.ObjectId==0x064B746B);
        if(selected==high)
            throw new NotSupportedException("Selected original native03 Scene terminal requires its own verified device recipe.");
        string evidence=native.SourceEvidence+
            ";owned-fresh-null-C8;allocation-scope=actual-node53/73-plus-selected-named-terminal;Common8="+
            template.Flags8.ToString("X8")+";Common0C="+template.Flags0C.ToString("X8")+";"+
            template.SourceIdentity+";"+defaults.SourceIdentity;
        var proxies=new Dictionary<uint,RenderTextureRuntimeDescriptor>();
        if(selected is uint key)
        {
            var terminal=declarations.Single(t=>t.Object.ObjectId==key);
            byte expected=key==normal?(byte)0x6B:key==high?(byte)3:(byte)1;
            if(scene.Kind!=RenderTextureKind.Texture2D||terminal.Kind!=scene.Kind||terminal.PixelFormat!=expected)
                throw new InvalidDataException("Named Scene terminal does not have its original source kind/native format.");
            var owned=RenderTextureRuntimeDescriptor.ConstructDeclared(terminal,
                actualAllocationGenerationIdentity+"/node1/named-terminal/"+key.ToString("X8"));
            // Both names reference the same concrete Data/CPU terminal.
            proxies.Add(scene.Object.ObjectId,owned);proxies.Add(key,owned);
        }
        var resolutions=RenderTextureResolutionFrame.CreateExplicitOwned(declarations,proxies,actualAllocationGenerationIdentity);
        return new(template.Flags8,template.Flags0C,camera.Source.Resource.Source.ResourceUri,
            camera.Source.Resource.Source.DecodedSha256,selected,resolutions,evidence,native);
    }

    /// <summary>Actual fresh C8-null selection with the source-created Main
    /// View1CC=-1. Positive or retained selectors require their current owner.
    /// Callers supply the actual typed node1 names, not numeric HDR overrides.</summary>
    public static uint? ReadSourceCreatedFreshTerminal(int currentPreparedView1CC,uint actualAuxFlags8,uint actualAuxFlags0C,
        uint actualHighName,uint actualNormalName,uint actualDisabledName)
    {
        if(currentPreparedView1CC!=-1)
            throw new NotSupportedException("Fresh Scene proxy source requires its actual Main prepared selector-1.");
        return(actualAuxFlags8&(1u<<23))!=0?null:
            (actualAuxFlags0C&(1u<<31))==0?actualDisabledName:
            (actualAuxFlags0C&(1u<<29))!=0?actualHighName:actualNormalName;
    }

    /// <summary>Do not reuse the allocation policy after a callback, overlay,
    /// retargeted Camera or View-mode change. Such changes require a new owner.</summary>
    public void ValidateCurrent(MapSourceCreatedF103CurrentFrame actualWorld)
    {
        ArgumentNullException.ThrowIfNull(actualWorld);
        var camera=actualWorld.Lighting.CapturePreparedCameraFromSameAcceptedMain(actualWorld.Camera);
        var common=actualWorld.Common;
        if(camera.ExecutableLocale!=native.Locale||camera.Source.Camera.ObjectId!=DefaultView0Source.MainCameraObjectId||
            camera.Source.Resource.Source.ResourceUri!=cameraUri||camera.Source.Resource.Source.DecodedSha256!=cameraSha||
            BinaryPrimitives.ReadInt32LittleEndian(camera.Prefix.Span.Slice(0x1CC,4))!=-1||
            common.FrameNumber!=camera.FrameNumber||!common.AuxiliaryPresent||
            common.Scope!=CommonRenderPublicationKind.InitialSettings||
            common.CommonFlags!=flags8||common.AuxiliaryLightingFlags!=flags0C)
            throw new InvalidDataException("Scene proxy allocation policy differs from the actual selected View/Common frame.");
    }
    private static void RequireFresh(MapCameraConstructionPacket camera,
        MapCommonRenderState common,LoadedDefaultView0Source defaults,uint f8,uint f0C,string locale)
    {
        if(camera.ExecutableLocale!=locale||camera.FrameNumber<=0||camera.Source.Camera.ObjectId!=defaults.MainCameraObjectId||
            BinaryPrimitives.ReadInt32LittleEndian(camera.Prefix.Span.Slice(0x1CC,4))!=-1||
            common.FrameNumber!=camera.FrameNumber||!common.AuxiliaryPresent||
            common.Scope!=CommonRenderPublicationKind.InitialSettings||
            common.CommonFlags!=f8||common.AuxiliaryLightingFlags!=f0C)
            throw new InvalidDataException("Fresh Scene proxy needs the actual Main0 prepared View and source Common Aux owners.");
    }
}
