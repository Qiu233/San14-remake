using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed class OriginalDirectionalLightBackendTemplate
{
    private readonly byte[] bytes;
    internal OriginalDirectionalLightBackendTemplate(byte[] value,string provenance,uint objectId)
        =>(bytes,SourceProvenance,SourceObjectId)=(value.ToArray(),provenance,objectId);
    public ReadOnlyMemory<byte> CompleteTemplate=>bytes.ToArray();
    public string SourceProvenance {get;}
    public uint SourceObjectId {get;}
    public bool NativeByteIdentityPreserved=>false;
}

/// <summary>Consumer-scoped representation of constructor-retained bytes.
/// This is neither an observation of native allocation values nor permission
/// for arbitrary LightData/Generic/shader consumers.</summary>
public sealed class OriginalDirectionalLightRetainedConsumerReceipt
{
    internal OriginalDirectionalLightRetainedConsumerReceipt(OriginalDirectionalLightRetainedNormalization owner,
        OriginalMapSourceCreatedLightingPacket lighting,bool earlyRequired)
        =>(Owner,Lighting,EarlyRequired)=(owner,lighting.CopyAcceptedSnapshot(),earlyRequired);
    internal OriginalDirectionalLightRetainedNormalization Owner {get;}
    internal OriginalMapSourceCreatedLightingPacket Lighting {get;}
    internal bool EarlyRequired {get;}
    public long FrameNumber=>Lighting.FrameNumber;
    public bool Late510Validated {get;internal set;}
    public bool Early126Validated {get;internal set;}
    internal int SkyProgramMask {get;set;}
    public int? ActualSkyAtmospherePixelProgram {get;internal set;}
    public int? ActualSkyContributionPixelProgram {get;internal set;}
    public bool SkyDeclaredProgramsValidated=>SkyProgramMask==3;
    public bool Sky10And4Validated=>SkyDeclaredProgramsValidated&&ActualSkyAtmospherePixelProgram==10;
    public bool AllDeclaredConsumersValidated=>Late510Validated&&(!EarlyRequired||Early126Validated)&&SkyDeclaredProgramsValidated;
    public bool NativeByteIdentityPreserved=>false;
    public bool BackendNamedOnly=>true;
    public string SourceProvenance=>Owner.SourceProvenance+";frame="+FrameNumber+";actual-Camera-replaces-Fog96";
}

/// <summary>Exact Directional561-property constructor holes, admitted only
/// by current group80 mode6 Camera, late510/383, ordinary early126/95 and
/// Sky VS0/currentPS3-or10/4 or finite-Moon PS5-or11/6. Fog96 is read, but its actual current Camera replacement
/// removes constructor-template dependency. Hemi W transport is not native
/// byte-identical; current admitted named programs have no hemi consumers.
/// Actual admitted Group1 uses the same pinned510/383 and ordinary126/95
/// writers as2/8; its complete modifier remains independently owned.</summary>
public sealed partial class OriginalDirectionalLightRetainedNormalization
{
    private static readonly (int Offset,int Length)[] Holes=[(0x10C,4),(0x11C,4),(0x150,96),(0x1F4,8),(0xF21,3),(0xF2C,4),(0x10B8,8)];
    private readonly LoadedShaderContainer late,sky;
    private readonly Dictionary<uint,OriginalDirectionalLightBackendTemplate> templates=new();
    private readonly Dictionary<uint,OriginalDirectionalLightConstructionPacket> constructed=new();
    private readonly bool earlyRequired;
    private OriginalDirectionalLightRetainedNormalization(LoadedShaderContainer lateShader,LoadedShaderContainer skyShader,string identity,bool includeEarly)
        =>(late,sky,BackendOwnerIdentity,earlyRequired)=(lateShader,skyShader,identity,includeEarly);
    public string BackendOwnerIdentity {get;}
    public bool BackendNamedOnly=>true;
    public bool NativeByteIdentityPreserved=>false;
    public int ConstructorRetainedBytes=>127;
    public int PreparedDirectRetainedBytes=>119;
    public string SourceProvenance=>"backend-named-Directional-retained127;owner="+BackendOwnerIdentity+
        ";native-retained-values-unresolved/native-byte-identity=false;consumers=mode6-Camera/510-PS383/Sky-VS0-currentPS3-or10-PS4-or-PS5-or11-PS6-or-VS2-PS14-PS15"+
        (earlyRequired?"/ordinary78-126-PS95":"");
    public static OriginalDirectionalLightRetainedNormalization LoadKnownConsumers(OriginalGameRepository repository,string identity,bool includeOrdinaryEarly126=true)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var l=repository.LoadShaderContainer("system",0x9D8E811F);RequireShader(l,0x9D8E811F,OriginalMapSourceCreatedShaderSelection.ShaderSha256);
        RequireProgram(l,383,OriginalDeferredSelected383Constants.PixelProgramSha256);
        RequireLate383NamedLayout(l);
        var s=repository.LoadShaderContainer("system",0x340AC335);RequireShader(s,0x340AC335,"CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424");
        RequireProgram(s,0,"E0E2FEB809484A59D79E28A58BBE384BAAE01B67914296C37CF1BBC26ED2AB36");
        RequireProgram(s,3,OriginalSky2SourceZeroMoon.AtmosphereProgramSha256);
        RequireProgram(s,10,OriginalSky2SourceZeroMoon.DirectionalAtmosphereProgramSha256);RequireProgram(s,4,OriginalSky2SourceZeroMoon.ContributionProgramSha256);
        RequireProgram(s,5,OriginalSky2NullTextureAdapter.FiniteMoonAtmosphere5SourceSha);
        RequireProgram(s,11,OriginalSky2NullTextureAdapter.FiniteMoonAtmosphere11SourceSha);
        RequireProgram(s,6,OriginalSky2NullTextureAdapter.FiniteMoonContribution6SourceSha);
        RequireProgram(s,2,"F446F2C09B2FB7C2D409EAF7DE1373E875A6AFFB3C7DC29B9B1029529A4DB8A6");
        RequireProgram(s,14,"4A875CA4C6C2398BE10A7365C2FA1703E621BE3736E3DF3E22735642C9CD2EE2");
        RequireProgram(s,15,"9956328CDE2DCEF6C2D4F0E56059010C332442B070BC328481DA2035C2C9D8BF");
        if(includeOrdinaryEarly126)
        {
            var e=repository.LoadShaderContainer("system",0xB82C3BBF);RequireShader(e,0xB82C3BBF,OriginalDeferredEarlyCausticsOwner.ShaderSourceSha256);
            RequireProgram(e,95,OriginalDeferredEarlyCausticsOwner.PixelProgramSha256);
        }
        return new(l,s,identity,includeOrdinaryEarly126);
    }

    /// <summary>Chosen backend bytes only. A construction/frame cannot acquire
    /// a consumer receipt merely by obtaining this template.</summary>
    public OriginalDirectionalLightBackendTemplate CreateTemplate(OriginalMapSeasonDirectionalLightSourceSet source,uint id,byte backendFill=0)
    {
        ArgumentNullException.ThrowIfNull(source);var canonical=OriginalMapSeasonDirectionalLightSources.Read(source.Database);
        uint word=backendFill*0x01010101u;
        if(!float.IsFinite(BitConverter.UInt32BitsToSingle(word)))
            throw new NotSupportedException("Directional backend fill must preserve the finite CE4A00 hemisphere-W arithmetic scope.");
        if(!canonical.Roots.SelectMany(r=>r.Children.Prepend(r.Root)).Any(r=>r.ObjectId==id))
            throw new InvalidDataException("Directional representation requires an actual selected-season declaration.");
        if(templates.ContainsKey(id))throw new InvalidOperationException("Directional representation already has this construction generation.");
        byte[] bytes=new byte[0x10C0];foreach(var h in Holes)bytes.AsSpan(h.Offset,h.Length).Fill(backendFill);
        var result=new OriginalDirectionalLightBackendTemplate(bytes,SourceProvenance+$";source={canonical.Source.ResourceUri}#{id:X8}@{canonical.Source.DecodedSha256};fill={backendFill:X2}",id);
        templates.Add(id,result);return result;
    }
    public OriginalDirectionalLightConstructionPacket ApplyConstructor(OriginalMapSeasonDirectionalLightSourceSet source,uint id,uint flags,
        IReadOnlyList<OriginalDirectionalLightResourceBinding> bindings,byte backendFill=0)
    {
        var t=CreateTemplate(source,id,backendFill);var c=OriginalDirectionalLightConstruction.Apply(source,id,t.CompleteTemplate.Span,new(flags,t.SourceProvenance,bindings));
        AdoptConstruction(c);return c;
    }
    /// <summary>Join the independently GPU-bound device owner's actual
    /// constructor. No resources, readiness or source-null receipts are created.</summary>
    public void AdoptConstruction(OriginalDirectionalLightConstructionPacket construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        if(!templates.TryGetValue(construction.SourceObjectId,out var t)||construction.RetainedTemplateProvenance!=t.SourceProvenance||constructed.ContainsKey(construction.SourceObjectId)||
            construction.ConstructorRetainedRanges.Count!=Holes.Length||construction.ConstructorRetainedRanges.Where((h,i)=>h.Offset!=Holes[i].Offset||h.Length!=Holes[i].Length).Any())
            throw new InvalidDataException("Directional constructor does not belong to this exact127B representation generation.");
        foreach(var h in Holes)
            if(!construction.ConstructorBytes.Span.Slice(h.Offset,h.Length).SequenceEqual(t.CompleteTemplate.Span.Slice(h.Offset,h.Length)))
                throw new InvalidDataException("Directional constructor changed an owned retained representation byte.");
        constructed.Add(construction.SourceObjectId,construction);
    }
    private void ValidateLighting(OriginalMapSourceCreatedLightingPacket lighting)
    {
        lighting.ValidateCurrentSourceFrame();
        if(lighting.SourceLightClassId!=OriginalDirectionalLightSource.SourceClass||!constructed.TryGetValue(lighting.SourceIdentity.MainLightObjectId,out var c)||
            lighting.SourceIdentity.MainLightResourceUri!=c.SourceUri||lighting.SourceIdentity.MainLightSha256!=c.SourceSha256||
            lighting.SourceIdentity.CreatedLightDataSha256!=c.PreparedSha256||lighting.SourceIdentity.DeclaredGroupMask!=c.Metadata.SourceGroupMask||
            lighting.ContextFlags3E4!=c.ContextFlags3E4||lighting.SourceIdentity.CameraObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||
            lighting.SourceIdentity.ConstructedOwnerSha256!=Hash(Encoding.UTF8.GetBytes(OriginalMapSourceCreatedDirectionalLightingSource.DescribeConstruction(c))))
            throw new InvalidDataException("Current Directional differs from this actual source/backend construction owner.");
    }
    public OriginalDirectionalLightRetainedConsumerReceipt CreateCurrentReceipt(OriginalMapSourceCreatedLightingPacket outer)
    {
        ArgumentNullException.ThrowIfNull(outer);ValidateLighting(outer);var b=outer.LocalLightData.Span;
        if(outer.SourceIdentity.DeclaredGroupMask!=0x80||!outer.CameraLightingApplied||b[0x10B0]!=6||b[0x10B2]!=8||
            !b.Slice(0x150,96).SequenceEqual(outer.PreparedCameraPrefix.Span.Slice(0x340,96)))
            throw new NotSupportedException("Directional retained representation requires the actual group80 mode6/B2=8 whole-Fog96 Camera replacement.");
        foreach(int offset in new[]{0x100,0x110,0x130,0x140})
            if(!b.Slice(offset,12).SequenceEqual(new byte[12]))throw new NotSupportedException("An active hemisphere requires its own retained-W consumer proof.");
        return new(this,outer,earlyRequired);
    }
    private void ValidateReceipt(OriginalDirectionalLightRetainedConsumerReceipt receipt)
    {ArgumentNullException.ThrowIfNull(receipt);if(!ReferenceEquals(receipt.Owner,this))throw new InvalidDataException("Directional receipt belongs to another owner.");CreateCurrentReceipt(receipt.Lighting);}
    public void ValidateLate510(OriginalDirectionalLightRetainedConsumerReceipt receipt,OriginalDeferredF103SelectedSource selected,
        OriginalMapSourceCreatedLightingPacket actualInner)
    {
        ValidateReceipt(receipt);ArgumentNullException.ThrowIfNull(selected);ValidateLighting(actualInner);selected.Validate();
        if(selected.Combination.Index!=510||selected.PixelProgramIndex!=383||selected.PixelProgramSha256!=OriginalDeferredSelected383Constants.PixelProgramSha256||
            selected.OwnedFeatureSource is not { } feature||feature.Scratch.HemisphereCount!=0||feature.Scratch.Mode50!=7||
            !IsSupportedLateModifier(selected,actualInner)||actualInner.FrameNumber!=receipt.FrameNumber||
            !feature.SelectedModifierLightData.Span.SequenceEqual(actualInner.LocalLightData.Span)||
            receipt.Lighting.AcceptedInputs.SelectedSharedFogOwnerEnabled is not bool)
            throw new NotSupportedException("Directional retained late scope requires actual510/383 outer80 plus admitted independent1/2/8 H and known Fog owner; "+
                $"actual combo={selected.Combination.Index}/PS{selected.PixelProgramIndex}@{selected.PixelProgramSha256},"+
                $" inner={actualInner.SourceIdentity.DeclaredGroupMask:X8}, frame={actualInner.FrameNumber}/{receipt.FrameNumber},"+
                $" hemi={selected.OwnedFeatureSource?.Scratch.HemisphereCount}, mode={selected.OwnedFeatureSource?.Scratch.Mode50},"+
                $" View19AC={selected.OwnedFeatureSource?.CurrentView.ViewFlags19AC:X8},"+
                $" exactH={selected.OwnedFeatureSource is { } actualFeature && actualFeature.SelectedModifierLightData.Span.SequenceEqual(actualInner.LocalLightData.Span)},"+
                $" knownFog={receipt.Lighting.AcceptedInputs.SelectedSharedFogOwnerEnabled is bool}.");
        feature.ValidateFrame(selected.Camera,receipt.Lighting);
        var expected=OriginalDeferredF103Kind1Modifier.ApplyWithModifier(receipt.Lighting.LocalLightData.Span,actualInner.LocalLightData.Span,feature.NativeArg18);
        if(!feature.EffectiveLocalLightData.Span.SequenceEqual(expected.Bytes))throw new InvalidDataException("Current late source has an additional unclosed retained-byte writer.");
        receipt.Late510Validated=true;
    }
    public void ValidateEarly126(OriginalDirectionalLightRetainedConsumerReceipt receipt,OriginalDeferredEarlyF103SelectedSource selected,
        OriginalDeferredEarlyCausticsPacket caustics,OriginalDeferredEarlyConstantPacket actualNamedConstants)
    {
        ValidateReceipt(receipt);ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(caustics);selected.Validate();
        if(!earlyRequired||selected.SourcePlan.Node.Index!=78||selected.Combination.Index!=126||selected.PixelProgramIndex!=95||
            selected.PixelProgramSha256!=OriginalDeferredEarlyCausticsOwner.PixelProgramSha256||selected.OwnedFeatureSource is not { } f||f.Scratch.HemisphereCount!=0)
            throw new NotSupportedException("Directional retained early scope is only actual ordinary78/126/PS95 with zero hemisphere.");
        f.ValidateFrame(selected.Camera,receipt.Lighting);caustics.SourceNull.Validate(selected);
        if(actualNamedConstants.Bytes.Length!=480||actualNamedConstants.RetainedVariables.Count!=0||actualNamedConstants.CommonWrites.Count!=13||actualNamedConstants.AdditionalWrites.Count!=3)
            throw new InvalidDataException("Directional ordinary early scope requires all16 current named writers.");
        foreach(var write in caustics.NativeWrites)
        {
            var actual=actualNamedConstants.AdditionalWrites.SingleOrDefault(w=>w.Name==write.Name);
            var target=selected.PixelDefinitions.Buffers.Single(b=>b.Name.Text=="$Globals").Variables.Single(v=>v.Name.Text==write.Name);
            if(actual is null||actual.CurrentProducerProvenance!=write.CurrentProducerProvenance||!actual.Bytes.Span.SequenceEqual(write.Bytes.Span)||
                !actualNamedConstants.Bytes.Span.Slice(target.StartOffset,target.ByteSize).SequenceEqual(write.Bytes.Span))
                throw new InvalidDataException("Directional early packet differs from its current source Caustics writer.");
        }
        var globals=selected.PixelDefinitions.Buffers.Single(b=>b.Name.Text=="$Globals");
        int occ=globals.Variables.Single(v=>v.Name.Text=="vOccRate").StartOffset,fade=globals.Variables.Single(v=>v.Name.Text=="fSdmFade").StartOffset;
        var bytes=actualNamedConstants.Bytes.Span;
        var reprojection=OriginalDeferredEarlySceneConstants.CreateOrdinary126(selected,late,receipt.Lighting,
            new(new(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(occ,4)),BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(occ+4,4)))),
            new(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(fade,4))),caustics.NativeWrites,actualNamedConstants.CurrentProducerProvenance);
        if(!reprojection.Bytes.Span.SequenceEqual(bytes))throw new InvalidDataException("Directional early cb0 differs from its exact source projection.");
        receipt.Early126Validated=true;
    }
    public void ValidateSkyNamed(OriginalDirectionalLightRetainedConsumerReceipt receipt,OriginalSkyDirectionalSelectedLightSource source,
        LoadedOriginalSkyDirectionalAtmosphereNativeSource native,OriginalSky2DrawInputs draw,int actualPixelProgram,
        ReadOnlyMemory<byte> actualVertexCb0,ReadOnlyMemory<byte> actualPixelCb0)
    {
        int atmosphereProgram=(source.SelectedPacket.CompleteLightData.Span[0xFDC]&2)!=0?10:3;
        ValidateSkyNamedCore(receipt,source,native,draw,atmosphereProgram,4,256,actualPixelProgram,actualVertexCb0,actualPixelCb0);
    }

    /// <summary>Current nonnull Sun/Moon setter projection. The complete owned
    /// feature, setter lifetime and selected original programs come from this
    /// same source receipt; a desired atmosphere packet is not a GPU draw.</summary>
    public void ValidateSkyNamed(OriginalDirectionalLightRetainedConsumerReceipt receipt,OriginalSkyDirectionalSelectedLightSource source,
        LoadedOriginalSkyDirectionalAtmosphereNativeSource native,OriginalSky2DrawInputs draw,
        OriginalSkyDirectionalDesiredSelectionReceipt desiredSelection,int actualPixelProgram,
        ReadOnlyMemory<byte> actualVertexCb0,ReadOnlyMemory<byte> actualPixelCb0)
    {
        desiredSelection.ValidateCurrent(source);desiredSelection.ValidateSelectedMoonProgramScope();
        var atmosphere=desiredSelection.GetSelection(true);var contribution=desiredSelection.GetSelection(false);
        static int Program(OriginalSky2SelectionResult selected,uint stage)=>selected.Combination.Stages
            .Single(s=>s.StageCode==stage&&s.Entry is not null).Entry!.ProgramIndex;
        int atmosphereProgram=Program(atmosphere,0),contributionProgram=Program(contribution,0);
        if(Program(atmosphere,1)!=0||Program(contribution,1)!=0||atmosphereProgram is not (5 or 11)||contributionProgram!=6)
            throw new NotSupportedException("Current Directional named consumer requires the selected original VS0/PS5-or11/PS6 source family.");
        if(actualPixelProgram!=atmosphereProgram&&actualPixelProgram!=contributionProgram)
            throw new NotSupportedException("Current Directional named consumer is outside its actual desired source pair.");
        (draw.Pixel.SourceMode4Fog??throw new InvalidDataException("Directional named Sky needs its actual current mode4 Fog writer."))
            .ValidateOriginalPixelCpuPacking(sky,actualPixelProgram);
        ValidateSkyNamedCore(receipt,source,native,draw,atmosphereProgram,contributionProgram,336,
            actualPixelProgram,actualVertexCb0,actualPixelCb0);
    }

    private void ValidateSkyNamedCore(OriginalDirectionalLightRetainedConsumerReceipt receipt,OriginalSkyDirectionalSelectedLightSource source,
        LoadedOriginalSkyDirectionalAtmosphereNativeSource native,OriginalSky2DrawInputs draw,
        int atmosphereProgram,int contributionProgram,int pixelBytes,int actualPixelProgram,
        ReadOnlyMemory<byte> actualVertexCb0,ReadOnlyMemory<byte> actualPixelCb0,int vertexProgram=0,
        OriginalSkySceneSharedAtmosphere? actualSourceAtmosphere=null)
    {
        ValidateReceipt(receipt);ArgumentNullException.ThrowIfNull(source);source.ValidateCurrent();
        if(source.AcceptedLighting.FrameNumber!=receipt.FrameNumber||source.AcceptedLighting.Binding!=receipt.Lighting.Binding||
            !source.AcceptedLighting.LocalLightData.Span.SequenceEqual(receipt.Lighting.LocalLightData.Span)||
            actualPixelProgram!=contributionProgram&&actualPixelProgram!=atmosphereProgram||
            actualVertexCb0.Length!=sky.ReadResourceDefinitions(vertexProgram).Buffers.Single().ByteSize||actualPixelCb0.Length!=pixelBytes||
            receipt.ActualSkyAtmospherePixelProgram is int previousAtmosphere&&previousAtmosphere!=atmosphereProgram||
            receipt.ActualSkyContributionPixelProgram is int previousContribution&&previousContribution!=contributionProgram)
            throw new NotSupportedException("Directional retained Sky scope requires the same actual mode6 source, selected source pair and complete named buffers.");
        var atmosphere=actualSourceAtmosphere??source.PrepareAtmosphere(native);
        if(atmosphere.Fog.SourceMode4Proof is { } proof)
        {proof.ValidateValue(draw.Vertex.Fog);proof.ValidateValue(draw.Pixel.Fog);}
        else if(!SameVectorBits(atmosphere.Fog.Value,draw.Vertex.Fog)||!SameVectorBits(atmosphere.Fog.Value,draw.Pixel.Fog))
            throw new InvalidDataException("Current Sky Fog differs from its exact source-selected package writer.");
        if(!draw.Pixel.Scattering.SequenceEqual(atmosphere.Scattering))throw new InvalidDataException("Current Sky Scat is not from the actual replaced CameraFog96.");
        var v=OriginalSky2Constants.Vertex(sky.ReadResourceDefinitions(vertexProgram),actualVertexCb0.Span,draw.Vertex);
        var p=OriginalSky2Constants.Pixel(sky.ReadResourceDefinitions(actualPixelProgram),actualPixelCb0.Span,draw.Pixel);
        if(!v.Bytes.AsSpan().SequenceEqual(actualVertexCb0.Span)||!p.Bytes.AsSpan().SequenceEqual(actualPixelCb0.Span))
            throw new InvalidDataException("Current Sky named buffers differ from their unchanged source writers.");
        receipt.ActualSkyAtmospherePixelProgram=atmosphereProgram;
        receipt.ActualSkyContributionPixelProgram=contributionProgram;
        receipt.SkyProgramMask|=actualPixelProgram==atmosphereProgram?1:2;
    }
    public void ValidateSkyNamed(OriginalDirectionalLightRetainedConsumerReceipt receipt,OriginalSkyDirectionalSelectedLightSource source,
        LoadedOriginalSkyDirectionalAtmosphereNativeSource native,OriginalSky2DrawInputs draw,
        OriginalSkySceneSourceFramePacket actualFrame,OriginalSkyDirectionalDesiredSelectionReceipt desiredSelection,int actualPixelProgram,
        ReadOnlyMemory<byte> actualVertexCb0,ReadOnlyMemory<byte> actualPixelCb0)
    {
        desiredSelection.ValidateCurrent(source);desiredSelection.ValidateAppliedFrame(actualFrame);
        desiredSelection.ValidateSelectedMoonProgramScope();
        var selectedAtmosphere=actualFrame.SourceFrame.SourceSelectedAtmosphere??
            throw new InvalidDataException("Current Directional named consumer requires its actual source-selected renderer/package receipt.");
        selectedAtmosphere.ValidateFrame(actualFrame,source);
        var atmosphere=desiredSelection.GetSelection(true);var contribution=desiredSelection.GetSelection(false);
        static int Program(OriginalSky2SelectionResult s,uint stage)=>s.Combination.Stages
            .Single(x=>x.StageCode==stage&&x.Entry is not null).Entry!.ProgramIndex;
        int vertexProgram=Program(contribution,1),atmosphereProgram=Program(atmosphere,0),contributionProgram=Program(contribution,0);
        var words=contribution.RendererFeature.Feature.Words;
        bool mode4=words[6]==4&&words[7]==1;
        bool mode7=words[6]==7&&words[7]==2;
        if(Program(atmosphere,1)!=vertexProgram||
            !(mode4&&vertexProgram==0&&(atmosphereProgram is 5 or 11)&&contributionProgram==6||
              mode7&&vertexProgram==2&&atmosphereProgram==14&&contributionProgram==15)||
            !desiredSelection.ExplicitOwnedRendererFeature48.Span.SequenceEqual(selectedAtmosphere.RendererFeature.RequestFeature48.Span)||
            !contribution.RendererFeature.Feature.RawBytes.AsSpan().SequenceEqual(selectedAtmosphere.RendererFeature.MatchedFeature48.Span))
            throw new NotSupportedException("Current Directional consumer differs from its full source renderer request and selected original pair.");
        if(mode4)
            (draw.Pixel.SourceMode4Fog??throw new InvalidDataException("Mode4 Sky needs its actual Fog writer."))
                .ValidateOriginalPixelCpuPacking(sky,actualPixelProgram);
        ValidateSkyNamedCore(receipt,source,native,draw,atmosphereProgram,contributionProgram,336,
            actualPixelProgram,actualVertexCb0,actualPixelCb0,vertexProgram,actualFrame.SourceFrame.Atmosphere);
    }
    private static bool SameVectorBits(System.Numerics.Vector4 a,System.Numerics.Vector4 b)=>
        new[]{a.X,a.Y,a.Z,a.W}.Select(BitConverter.SingleToUInt32Bits)
            .SequenceEqual(new[]{b.X,b.Y,b.Z,b.W}.Select(BitConverter.SingleToUInt32Bits));
    private static void RequireShader(LoadedShaderContainer s,uint id,string hash)
    {if(s.Source.OverrideSource is not null||s.Source.FileId!=id||s.Source.DecodedSha256!=hash||Hash(s.Source.Bytes)!=hash)throw new InvalidDataException("Directional retained shader source differs.");}
    private static void RequireProgram(LoadedShaderContainer s,int index,string hash)
    {if(Hash(s.Container.GetProgramBytes(s.Source.Bytes,index))!=hash)throw new InvalidDataException("Directional retained original program differs.");}
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}
