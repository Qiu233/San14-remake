using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

internal sealed record AreaAdministrationListColumn(int Id,int Width,int CaptionMessage,
    byte Align,byte LineArrange,bool InitialAscending);
internal sealed record AreaAdministrationPlaceCell(bool Available,AreaPlaceKind? Kind);
internal sealed record AreaAdministrationListEntry(int AreaId,AreaAdministrationPlaceCell Place,
    IReadOnlyList<string> Cells);
internal sealed record AreaAdministrationListOrder(IReadOnlyList<int>? AreaIds,string? UnresolvedInput);

/// <summary>Original Domestic columns, independent of display sorting and the
/// draft's held order. Known source blanks and unresolved inputs stay distinct.</summary>
internal static class AreaAdministrationListContent
{
    internal static IReadOnlyList<AreaAdministrationListColumn> Columns { get; }=Array.AsReadOnly<AreaAdministrationListColumn>(
    [
        new(0,132,0x0202,0x01,1,true),new(5,132,0x03D4,0x11,2,true),
        new(6,104,0x047C,0x21,3,false),new(7,132,0x047D,0x21,3,false),
        new(8,104,0x047E,0x21,3,false),new(9,132,0x047F,0x21,3,false),
        new(10,104,0x0480,0x21,3,false),new(11,132,0x0481,0x21,3,false),
        new(14,76,0x0253,0x21,3,false),new(4,118,0x03D3,0x11,2,false),new(12,132,0x02B6,0x11,2,false)
    ]);

    internal static IReadOnlyList<AreaAdministrationListEntry> Create(GameResourceRepository repository,PreviewText text,
        IReadOnlyList<AreaAdministrationAreaRow> rows,Func<int,AreaAdministrationPlaceCell> place)
    {
        string unknown=text.Get("detail_unknown");
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing administration list caption.");
        string Name(AreaAdministrationQuery<string> query)=>query.Available?query.Value??"":unknown;
        string Number(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??unknown;
        string Development(AreaAdministrationParentDevelopment value,bool level)=>value.UsesSourceUnknownText switch
        {true=>Message(0x0353),false=>Number(level?value.Level:value.Experience),_=>unknown};
        string Land(AreaAdministrationAreaRow row)
        {
            if(row.LandMaximum.Available&&row.LandMaximum.Value is null||row.LandCurrent.Available&&row.LandCurrent.Value is null)return "";
            string current=row.LandCurrent.Available?Number(row.LandCurrent.Value):unknown;
            string maximum=row.LandMaximum.Available?Number(row.LandMaximum.Value):unknown;
            return current.PadLeft(3)+"/"+maximum.PadLeft(3);
        }
        string Focus(AreaAdministrationQuery<AreaDevelopmentFocus?> value)=>!value.Available?unknown:value.Value switch
        {AreaDevelopmentFocus.None=>"",AreaDevelopmentFocus.Commerce=>Message(0x02B3),
            AreaDevelopmentFocus.Agriculture=>Message(0x02B4),AreaDevelopmentFocus.Barracks=>Message(0x02B5),_=>unknown};
        return rows.Select(row=>new AreaAdministrationListEntry(row.AreaId,place(row.AreaId),Array.AsReadOnly(new[]
        {
            Name(row.AreaName),Name(row.OfficerName),Development(row.CommerceDisplay,true),Development(row.CommerceDisplay,false),
            Development(row.AgricultureDisplay,true),Development(row.AgricultureDisplay,false),
            Development(row.BarracksDisplay,true),Development(row.BarracksDisplay,false),Number(row.Security),Land(row),Focus(row.Focus)
        }))).ToArray();
    }
}
