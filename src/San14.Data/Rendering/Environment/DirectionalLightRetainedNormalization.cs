using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed class DirectionalLightBackendTemplate
{
    private readonly byte[] bytes;
    internal DirectionalLightBackendTemplate(byte[] value,string provenance,uint objectId,
        DirectionalLightSettings settings)
        =>(bytes,SourceProvenance,SourceObjectId,Settings)=(value.ToArray(),provenance,objectId,settings);
    public ReadOnlyMemory<byte> CompleteTemplate=>bytes.ToArray();
    public string SourceProvenance {get;}
    public uint SourceObjectId {get;}
    public DirectionalLightSettings Settings {get;}
    public bool NativeByteIdentityPreserved=>false;
}

/// <summary>Consumer-scoped representation of constructor-retained bytes.
/// This is neither an observation of native allocation values nor permission
/// for arbitrary LightData/Generic/shader consumers.</summary>
public sealed class DirectionalLightRetainedConsumerReceipt
{
    internal DirectionalLightRetainedConsumerReceipt(DirectionalLightRetainedNormalization owner,
        MapSourceCreatedLightingPacket lighting,bool earlyRequired)
        =>(Owner,Lighting,EarlyRequired)=(owner,lighting,earlyRequired);
    internal DirectionalLightRetainedNormalization Owner {get;}
    internal MapSourceCreatedLightingPacket Lighting {get;}
    internal bool EarlyRequired {get;}
    public long FrameNumber=>Lighting.FrameNumber;
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
public sealed partial class DirectionalLightRetainedNormalization
{
    private static readonly (int Offset,int Length)[] Holes=[(0x10C,4),(0x11C,4),(0x150,96),(0x1F4,8),(0xF21,3),(0xF2C,4),(0x10B8,8)];
    private readonly LoadedShaderContainer late,sky;
    private readonly Dictionary<uint,DirectionalLightBackendTemplate> templates=new();
    private readonly Dictionary<uint,DirectionalLightConstructionPacket> constructed=new();
    private readonly bool earlyRequired;
    private DirectionalLightRetainedNormalization(LoadedShaderContainer lateShader,LoadedShaderContainer skyShader,string identity,bool includeEarly)
        =>(late,sky,BackendOwnerIdentity,earlyRequired)=(lateShader,skyShader,identity,includeEarly);
    public string BackendOwnerIdentity {get;}
    public bool BackendNamedOnly=>true;
    public bool NativeByteIdentityPreserved=>false;
    public int ConstructorRetainedBytes=>127;
    public int PreparedDirectRetainedBytes=>119;
    public string SourceProvenance=>"backend-named-Directional-retained127;owner="+BackendOwnerIdentity+
        ";native-retained-values-unresolved/native-byte-identity=false;consumers=mode6-Camera/510-PS383/Sky-VS0-currentPS3-or10-PS4-or-PS5-or11-PS6-or-VS2-PS14-PS15"+
        (earlyRequired?"/ordinary78-126-PS95":"");
    public static DirectionalLightRetainedNormalization LoadKnownConsumers(GameResourceRepository repository,string identity,bool includeOrdinaryEarly126=true)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var l=repository.LoadShaderContainer("system",0x9D8E811F);RequireShader(l,0x9D8E811F,MapSourceCreatedShaderSelection.ShaderSha256);
        RequireProgram(l,383,DeferredSelected383Constants.PixelProgramSha256);
        RequireLate383NamedLayout(l);
        var s=repository.LoadShaderContainer("system",0x340AC335);RequireShader(s,0x340AC335,"CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424");
        RequireProgram(s,0,"E0E2FEB809484A59D79E28A58BBE384BAAE01B67914296C37CF1BBC26ED2AB36");
        RequireProgram(s,3,Sky2SourceZeroMoon.AtmosphereProgramSha256);
        RequireProgram(s,10,Sky2SourceZeroMoon.DirectionalAtmosphereProgramSha256);RequireProgram(s,4,Sky2SourceZeroMoon.ContributionProgramSha256);
        RequireProgram(s,5,Sky2NullTextureAdapter.FiniteMoonAtmosphere5SourceSha);
        RequireProgram(s,11,Sky2NullTextureAdapter.FiniteMoonAtmosphere11SourceSha);
        RequireProgram(s,6,Sky2NullTextureAdapter.FiniteMoonContribution6SourceSha);
        RequireProgram(s,2,"F446F2C09B2FB7C2D409EAF7DE1373E875A6AFFB3C7DC29B9B1029529A4DB8A6");
        RequireProgram(s,14,"4A875CA4C6C2398BE10A7365C2FA1703E621BE3736E3DF3E22735642C9CD2EE2");
        RequireProgram(s,15,"9956328CDE2DCEF6C2D4F0E56059010C332442B070BC328481DA2035C2C9D8BF");
        if(includeOrdinaryEarly126)
        {
            var e=repository.LoadShaderContainer("system",0xB82C3BBF);RequireShader(e,0xB82C3BBF,DeferredEarlyCausticsOwner.ShaderSourceSha256);
            RequireProgram(e,95,DeferredEarlyCausticsOwner.PixelProgramSha256);
        }
        return new(l,s,identity,includeOrdinaryEarly126);
    }

    /// <summary>Chosen backend bytes only. A construction/frame cannot acquire
    /// a consumer receipt merely by obtaining this template.</summary>
    public DirectionalLightBackendTemplate CreateTemplate(MapSeasonDirectionalLightSourceSet source,uint id,byte backendFill=0)
    {
        ArgumentNullException.ThrowIfNull(source);
        uint word=backendFill*0x01010101u;
        if(!float.IsFinite(BitConverter.UInt32BitsToSingle(word)))
            throw new NotSupportedException("Directional backend fill must preserve the finite CE4A00 hemisphere-W arithmetic scope.");
        if(!source.Roots.SelectMany(r=>r.Children.Prepend(r.Root)).Any(r=>r.ObjectId==id))
            throw new InvalidDataException("Directional representation requires an actual selected-season declaration.");
        if(templates.ContainsKey(id))throw new InvalidOperationException("Directional representation already has this construction generation.");
        byte[] bytes=new byte[0x10C0];foreach(var h in Holes)bytes.AsSpan(h.Offset,h.Length).Fill(backendFill);
        var result=new DirectionalLightBackendTemplate(bytes,SourceProvenance+$";source={source.Source.ResourceUri}#{id:X8}@{source.Source.DecodedSha256};fill={backendFill:X2}",id,source.GetSettings(id));
        templates.Add(id,result);return result;
    }
    public DirectionalLightConstructionPacket ApplyConstructor(MapSeasonDirectionalLightSourceSet source,uint id,uint flags,
        IReadOnlyList<DirectionalLightResourceBinding> bindings,byte backendFill=0)
    {
        var t=CreateTemplate(source,id,backendFill);var c=DirectionalLightConstruction.Apply(source,id,t.CompleteTemplate.Span,new(flags,t.SourceProvenance,bindings));
        AdoptConstruction(c);return c;
    }
    /// <summary>Join the independently GPU-bound device owner's actual
    /// constructor. No resources, readiness or source-null receipts are created.</summary>
    public void AdoptConstruction(DirectionalLightConstructionPacket construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        if(!templates.TryGetValue(construction.SourceObjectId,out var t)||
            !ReferenceEquals(construction.Settings,t.Settings)||
            construction.RetainedTemplateProvenance!=t.SourceProvenance||constructed.ContainsKey(construction.SourceObjectId))
            throw new InvalidDataException("Directional constructor does not belong to this exact127B representation generation.");
        constructed.Add(construction.SourceObjectId,construction);
    }
    private void ValidateLighting(MapSourceCreatedLightingPacket lighting)
    {
        if(lighting.SourceLightClassId!=DirectionalLightSource.SourceClass||!constructed.TryGetValue(lighting.SourceIdentity.MainLightObjectId,out var c)||
            lighting.SourceIdentity.MainLightResourceUri!=c.SourceUri||lighting.SourceIdentity.MainLightSha256!=c.SourceSha256||
            lighting.SourceIdentity.CreatedLightDataSha256!=c.PreparedSha256||lighting.SourceIdentity.DeclaredGroupMask!=c.Metadata.SourceGroupMask||
            lighting.ContextFlags3E4!=c.ContextFlags3E4||lighting.SourceIdentity.CameraObjectId!=MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Current Directional differs from this actual source/backend construction owner.");
    }
    public DirectionalLightRetainedConsumerReceipt CreateCurrentReceipt(MapSourceCreatedLightingPacket outer)
    {
        ArgumentNullException.ThrowIfNull(outer);ValidateLighting(outer);var b=outer.LocalLightData.Span;
        if(outer.SourceIdentity.DeclaredGroupMask!=0x80||!outer.CameraLightingApplied||b[0x10B0]!=6||b[0x10B2]!=8)
            throw new NotSupportedException("Directional retained representation requires the actual group80 mode6/B2=8 whole-Fog96 Camera replacement.");
        foreach(int offset in new[]{0x100,0x110,0x130,0x140})
            if(!b.Slice(offset,12).SequenceEqual(new byte[12]))throw new NotSupportedException("An active hemisphere requires its own retained-W consumer proof.");
        return new(this,outer,earlyRequired);
    }
    private void ValidateReceipt(DirectionalLightRetainedConsumerReceipt receipt)
    {ArgumentNullException.ThrowIfNull(receipt);if(!ReferenceEquals(receipt.Owner,this))throw new InvalidDataException("Directional receipt belongs to another owner.");CreateCurrentReceipt(receipt.Lighting);}
    /// <summary>Reject unsupported retained-byte consumers at the selected
    /// program boundary. Numerical source replays belong to offline checks.</summary>
    public void RequireLateConsumer(DirectionalLightRetainedConsumerReceipt receipt,
        DeferredF103SelectedSource selected, MapSourceCreatedLightingPacket actualInner)
    {
        ValidateReceipt(receipt); ArgumentNullException.ThrowIfNull(selected); ValidateLighting(actualInner);
        if(selected.Combination.Index!=510 || selected.PixelProgramIndex!=383 ||
            selected.OwnedFeatureSource is not { } feature || feature.Scratch.HemisphereCount!=0 ||
            feature.Scratch.Mode50!=7 || !IsSupportedLateModifier(selected,actualInner) ||
            actualInner.FrameNumber!=receipt.FrameNumber ||
            receipt.Lighting.AcceptedInputs.SelectedSharedFogOwnerEnabled is not bool)
            throw new NotSupportedException("Directional retained late scope requires 510/383, mode7, no hemisphere, admitted Group1/2/8 and a resolved Fog owner.");
    }

    public void RequireEarlyConsumer(DirectionalLightRetainedConsumerReceipt receipt,
        DeferredEarlyF103SelectedSource selected)
    {
        ValidateReceipt(receipt); ArgumentNullException.ThrowIfNull(selected);
        if(!earlyRequired || selected.SourcePlan.Node.Index!=78 || selected.Combination.Index!=126 ||
            selected.PixelProgramIndex!=95 || selected.OwnedFeatureSource is not { } feature ||
            feature.Scratch.HemisphereCount!=0)
            throw new NotSupportedException("Directional retained early scope requires ordinary78/126/PS95 with no hemisphere.");
    }
    private static void RequireShader(LoadedShaderContainer s,uint id,string hash)
    {if(s.Source.OverrideSource is not null||s.Source.FileId!=id||s.Source.DecodedSha256!=hash||Hash(s.Source.Bytes)!=hash)throw new InvalidDataException("Directional retained shader source differs.");}
    private static void RequireProgram(LoadedShaderContainer s,int index,string hash)
    {if(Hash(s.Container.GetProgramBytes(s.Source.Bytes,index))!=hash)throw new InvalidDataException("Directional retained original program differs.");}
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}
