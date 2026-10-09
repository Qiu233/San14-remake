using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Exact internal diaphragm/star pointer choices. This copies
/// native wrapper identities and makes no statement about their pixel contents.
/// External custom diaphragm/star branches need their actual controller owners.</summary>
public sealed class MapPostEffect3LensFlare2ControllerTextures
{
    private readonly MapPostEffect3ParameterState state;
    private readonly MapPostEffect3TextureParameterState table;
    private readonly byte[] diaphragm,externalStar;
    internal MapPostEffect3LensFlare2ControllerTextures(MapPostEffect3ParameterState state,
        MapPostEffect3TextureParameterState table,string evidence)
    {
        this.state=state;this.table=table;diaphragm=state.RequireValue("DiaphragmType").ToArray();
        externalStar=state.RequireValue("EnableExternalStarBurst").ToArray();SourceEvidence=evidence;
    }
    public long FrameNumber=>state.FrameNumber;
    public string ContextOwnerIdentity=>state.OwnerIdentity;
    public string SourceSha256=>state.SourceSha256;
    public string SourceEvidence{get;}
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(actualTable,table)||table.FrameNumber!=state.FrameNumber||table.ContextOwnerIdentity!=state.OwnerIdentity||
            !state.RequireValue("DiaphragmType").Span.SequenceEqual(diaphragm)||
            !state.RequireValue("EnableExternalStarBurst").Span.SequenceEqual(externalStar))
            throw new InvalidDataException("LensFlare2 controller texture choices belong to another current table/scalar invocation.");
        if(BinaryPrimitives.ReadInt32LittleEndian(diaphragm)==2||BinaryPrimitives.ReadInt32LittleEndian(externalStar)!=0)
            throw new NotSupportedException("LensFlare2 external controller textures need their actual 840/848 wrapper producers.");
    }
}

public sealed class LoadedMapPostEffect3LensFlare2ControllerTextureSource
{
    internal LoadedMapPostEffect3LensFlare2ControllerTextureSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public MapPostEffect3LensFlare2ControllerTextures PrepareInternal(
        MapPostEffect3ParameterState state,MapPostEffect3TextureParameterState table,
        MapPostEffect3ControllerScalarSnapshot actualController)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(actualController);
        MapPostEffect3ParameterState.ValidateSource(table.Allocation.Source);
        if(actualController.FrameNumber!=state.FrameNumber||table.FrameNumber!=state.FrameNumber||
            table.ContextOwnerIdentity!=state.OwnerIdentity||string.IsNullOrWhiteSpace(actualController.SourceEvidence)||
            string.IsNullOrWhiteSpace(actualController.ControllerOwnerIdentity)||
            actualController.Diaphragm9C!=BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue("DiaphragmType").Span)||
            (actualController.ExternalStarA0!=0)!=
                (BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue("EnableExternalStarBurst").Span)!=0))
            throw new InvalidDataException("LensFlare2 choices need the exact copied current controller/scalar fields.");
        foreach(var (name,index) in new[]{("LF2InternalDiaphragmImage",50),("InputDiaphragmImage",51),
            ("LF2InternalStarBurstImage",55),("InputStarBurstImage",56)})
            if(state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name)!=index)
                throw new InvalidDataException("LensFlare2 current controller texture name/index changed: "+name);
        var recipe=new MapPostEffect3LensFlare2ControllerTextures(state,table,SourceEvidence+";"+actualController.SourceEvidence);
        recipe.RequireCurrentTextureTable(table);return recipe;
    }
}

public static class MapPostEffect3LensFlare2ControllerTextureSource
{
    public static LoadedMapPostEffect3LensFlare2ControllerTextureSource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadLensFlare2ControllerTextures(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3LensFlare2ControllerTextureSource ReadLensFlare2ControllerTextures(
        LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("LensFlare2 controller texture source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";copy-owned50-to51-if-Diaphragm9C!=2"+
            "-copy-owned55-to56-if-ExternalStarA0=0;no-LF2OutputColor57-alias",native.Locale);
    }
}
