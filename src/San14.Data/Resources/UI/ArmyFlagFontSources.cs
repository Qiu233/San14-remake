using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedArmyFlagFontSource(int ExplicitCommonArrayIndex,
    string CallerFontSource, uint ParameterKey, LoadedObjects CommonObjects,
    DokObject Parameter, uint MaxPrintCharNum, LoadedRdbAsset Source, G1FontCollection Collection)
{
    public bool FontCollectionFidelityVerified=>false;
    public bool CompleteFlagPaneDrawFidelityVerified=>false;
    public bool FreshHostDefaultFontFidelityVerified {get;init;}
    public string? FreshHostDefaultProvenance {get;init;}
}

/// <summary>Reads the actual common FontParameter/G1N dependencies. The caller
/// supplies its owned collection choice; neither locale selection nor the current
/// original TextManager collection is inferred from the array order.</summary>
public static class ArmyFlagFontSources
{
    public const uint CommonObjectsId=0x208BE13A;
    public static LoadedArmyFlagFontSource LoadFreshHostDefault(GameResourceRepository repository,
        bool explicitOwnedHostSelectorIsZero,string callerFontSource)
    {
        if(!explicitOwnedHostSelectorIsZero)throw Error("fresh Host default requires the explicit owned zero font selector");
        return Load(repository,0,callerFontSource) with{FreshHostDefaultFontFidelityVerified=true,
            FreshHostDefaultProvenance="SC Host BF6DC0/BF705E zeros selector+460; UI E7FE92/E7FE9A/E7FEA8 chooses97D72A63; EC7186..EC71E8->G1N->AE94E0; current overrides remain explicit"};
    }
    public static LoadedArmyFlagFontSource Load(GameResourceRepository repository,
        int explicitCommonArrayIndex,string callerFontSource)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(string.IsNullOrWhiteSpace(callerFontSource))throw Error("requires the explicit caller collection source");
        var common=repository.LoadObjects("KIDSSystemResource",CommonObjectsId);
        var settings=common.Document.Objects.Single(o=>o.ObjectId==0xE0D0BB40&&o.ClassId==0x8D034D41);
        uint[] keys=Values(settings,0x8985E35B),names=Values(settings,0xF504C347);
        if(keys.Length!=names.Length||(uint)explicitCommonArrayIndex>=names.Length)
            throw Error("requires matching FontParameter key/name arrays and a valid explicit index");
        var parameter=common.Document.Objects.Single(o=>o.ObjectId==names[explicitCommonArrayIndex]&&o.ClassId==0x9C1CED03);
        uint file=Values(parameter,0x5348D820).Single(),max=Values(parameter,0xC619F119).Single();
        var source=repository.LoadRdbAsset("RRPreview",file);
        return new(explicitCommonArrayIndex,callerFontSource,keys[explicitCommonArrayIndex],common,parameter,max,
            source,G1FontCollectionReader.Read(source.Bytes));
    }

    public static G1FontGlyphPixels ReadGlyph(LoadedArmyFlagFontSource font,ushort utf16CodeUnit,
        int explicitFaceIndex,int explicitPaletteIndex)
    {
        ArgumentNullException.ThrowIfNull(font);
        return G1FontCollectionReader.DecodeGlyph(font.Collection,font.Source.Bytes,utf16CodeUnit,explicitFaceIndex,explicitPaletteIndex);
    }

    private static uint[] Values(DokObject obj,uint hash)
    {
        var property=obj.Properties.Single(p=>p.NameHash==hash);
        if(property.RawValue.Length==0||property.RawValue.Length%4!=0||property.ElementSize!=4||
            (long)property.ElementCount*4!=property.RawValue.Length)
            throw Error("FontParameter field is not its complete UInt32 declaration");
        var values=new uint[property.RawValue.Length/4];for(int i=0;i<values.Length;i++)values[i]=BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(i*4));return values;
    }
    private static InvalidDataException Error(string message)=>new($"Army flag font {message}.");
}
