using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record AreaAdministrationHeaderValues(int? CurrentBooks,int? RemainingBooks,int? BookDifference,
    int? AppointmentUpkeep,string? TreasuryText);

/// <summary>The original Domestic command-book header. Source draft-book
/// changes are supplied separately from formal cost and system reservations.</summary>
internal partial class AreaAdministrationResourceHeader : Control
{
    private readonly Dictionary<int,UiSourceText> values=[];
    private string unknown="";
    private uint decreaseColour;
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text)
    {
        var source=new UiSourceLayout(repository,0xD15BF749);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        var presets=MapTextPresets.Create();var palettes=MapSpritePalettes.Create();unknown=text.Get("detail_unknown");decreaseColour=presets[40].LT.CpuPacked;
        source.Art(this,2,source.Rectangle(2),assets,shader);
        source.Art(this,3,source.Rectangle(3),assets,shader,palettes[10]);
        source.Art(this,6,source.Rectangle(6),assets,shader);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        foreach(var (node,message) in new[]{(4,0x0292),(9,0x0403),(13,0x020C)})
            source.Text(this,node,default,messages.Records[message].Text??throw new InvalidDataException("Missing Domestic resource caption."),font,presets[13]);
        source.Text(this,11,default,messages.Records[0x056B].Text??throw new InvalidDataException("Missing Domestic upkeep suffix."),font);
        foreach(int node in new[]{5,7,8,10,14})values[node]=source.Text(this,node,default,unknown,font);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void Apply(AreaAdministrationHeaderValues value)
    {
        values[5].Text=Number(value.CurrentBooks);values[7].Text=Number(value.RemainingBooks);
        if(value.BookDifference is not int difference)values[8].Text=unknown;
        else if(difference>=0)values[8].Text="(-0)";
        else values[8].SetRuns(new("("),new(difference.ToString(CultureInfo.InvariantCulture),decreaseColour),new(")"));
        values[10].Text=Number(value.AppointmentUpkeep);values[14].Text=value.TreasuryText??unknown;
    }
    private string Number(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??unknown;
}
