using System.Collections.Immutable;
using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private sealed record ChronologyCityFlagStyle(string Name,Color Fill,Color Letter);
    private Dictionary<int,ChronologyCityFlagStyle>? _chronologyCityFlags;
    private Dictionary<int,Color>? _chronologyGateFills;
    private bool _chronologySavedBaseInformation,_chronologySavedArmyInformation;
    private int _chronologySavedInformationMode;

    /// <summary>Publish the selected history row over existing city/gate ink.
    /// The current source supplies only Base/Object/Hex/Area/City identities;
    /// historical colours and names always come from the saved snapshot.</summary>
    internal void ApplyChronologyShortNames(ChronologyInformationMapSnapshot snapshot,SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(current);
        var saved=snapshot.CityForceShortNames;
        if(saved.IsDefault||saved.Length!=52||snapshot.AreaColors.IsDefault||snapshot.AreaColors.Length!=501)
            throw new InvalidDataException("Chronology city flags require 52 saved short-name slots and 501 saved Area colours.");
        foreach(var pair in saved)
            if(pair.IsDefault||pair.Length!=2)
                throw new InvalidDataException("Chronology city short names require two UTF16 storage units per physical slot.");
        if(_font is null||_cityGlyphFont is null)
            throw new InvalidOperationException("Chronology requires the actual configured city marker font owner.");
        var styles=new Dictionary<int,ChronologyCityFlagStyle>();
        foreach(var marker in _markers.Values.Where(marker=>marker.Kind==Kind.City))
        {
            if(marker.Id is <1 or >51)
                throw new InvalidDataException("Chronology city marker has no physical City1..51 source identity.");
            var sourceBase=new BaseReference(BaseKind.City,marker.Id);
            int colour=ChronologySavedBaseColour(snapshot,current,sourceBase);
            // 22A940 first resolves Area.City35 with its native City0 fallback.
            // 22DC80 then exposes the actual city pointer's physical ordinal.
            int savedCity=Math.Clamp(CurrentGateMapQueries.FindCity(sourceBase,current).PhysicalSlot??0,0,51);
            string name=ChronologyShortName(saved[savedCity],savedCity);
            styles.Add(marker.Id,new(name,CanvasColor(MapSpritePalettes.ForceFill(colour)),
                CanvasColor(_spritePalettes!.ForceLetter(colour))));
        }
        var gateFills=new Dictionary<int,Color>();
        foreach(var marker in _markers.Values.Where(marker=>marker.Kind==Kind.Gate))
        {
            // Marker IDs use the accepted primary's world location52..61.
            // The actual owner carries its independently bound native Gate1..10.
            var owner=_cityPublication?.Source.Primary.Owners.SingleOrDefault(value=>
                value.CitySelection is null&&value.PhysicalSlot==marker.Id);
            if(marker.Id is <52 or >61||owner is null||owner.Gate is not {} gate||
                gate.Definition.WorldSlot!=marker.Id||gate.Definition.NativeSlot is <1 or >10||
                owner.NativeSlot!=gate.Definition.NativeSlot)
                throw new InvalidDataException("Chronology Gate marker lacks its actual accepted world/native-slot binding.");
            var sourceBase=new BaseReference(BaseKind.Gate,gate.Definition.NativeSlot);
            gateFills.Add(marker.Id,CanvasColor(MapSpritePalettes.ForceFill(
                ChronologySavedBaseColour(snapshot,current,sourceBase))));
        }
        _font.Prepare(styles.Values.Select(style=>style.Name),_cityGlyphFont);
        if(_chronologyCityFlags is null)
        {
            _chronologySavedBaseInformation=_baseInformationVisible;
            _chronologySavedArmyInformation=_armyInformationVisible;
            _chronologySavedInformationMode=foodWarningInformationMode;
        }
        _chronologyCityFlags=styles;
        _chronologyGateFills=gateFills;
        SetInformationVisibility(true,false,_chronologySavedInformationMode);
        QueueMarkerRedraw();
    }

    internal void ClearChronologyShortNames()
    {
        if(_chronologyCityFlags is null)return;
        _chronologyCityFlags=null;
        _chronologyGateFills=null;
        SetInformationVisibility(_chronologySavedBaseInformation,_chronologySavedArmyInformation,
            _chronologySavedInformationMode);
        QueueMarkerRedraw();
    }

    private ChronologyCityFlagStyle? ChronologyCityFlag(Marker marker)
    {
        if(_chronologyCityFlags is not {} styles)return null;
        return styles.TryGetValue(marker.Id,out var style)?style:
            throw new InvalidDataException("Chronology city marker was not part of the selected source publication.");
    }

    private Color? ChronologyGateFill(Marker marker)
    {
        if(_chronologyCityFlags is null)return null;
        if(_chronologyGateFills is {} fills&&fills.TryGetValue(marker.Id,out var fill))return fill;
        throw new InvalidDataException("Chronology Gate marker was not part of the selected source publication.");
    }

    private static int ChronologySavedBaseColour(ChronologyInformationMapSnapshot snapshot,
        SourceWorldContext current,BaseReference sourceBase)
    {
        var hex=CurrentGateMapQueries.FindHex(sourceBase,current);
        // 22A0E0's raw Hex.Area12 is clamped to500 here; FindArea has a
        // different >500-to-Area0 lookup and must not supply this colour.
        int rawArea=hex.PhysicalSlot.HasValue
            ? current.ReadHexAreaSlot(hex)??throw new InvalidDataException("Chronology Base Hex.Area12 is unresolved.")
            : 0;
        return snapshot.AreaColors[Math.Clamp(rawArea,0,500)];
    }

    private static string ChronologyShortName(ImmutableArray<ushort> pair,int physicalCity)
    {
        if(pair[0]==0)return "";
        // 931D10 ->931E20 reads a NUL-terminated wide string, not a bounded
        // first-glyph value. Without a NUL it continues into adjacent storage.
        if(pair[1]!=0)
            throw new NotSupportedException("Chronology saved city name has no NUL within its two UTF16 units; adjacent-string storage is unresolved at city "+physicalCity+".");
        return ((char)pair[0]).ToString();
    }
}
