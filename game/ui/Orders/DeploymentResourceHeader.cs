using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The source resource header shared by setup and target selection.
/// The containing page owns its font, artwork and current draft query.</summary>
internal partial class DeploymentResourceHeader : Control
{
    private readonly Dictionary<int,UiSourceText> values=[];
    private string unknown="";
    private uint negativeColour;
    internal IReadOnlyDictionary<int,UiSourceText> Values=>values;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        PreviewText text,CityState city,long availableTroops,long? books)
    {
        var source=new UiSourceLayout(repository,0xCFF3CC08);
        Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        var presets=MapTextPresets.Create();negativeColour=presets[40].LT.CpuPacked;
        unknown=text.Get("detail_unknown");
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        source.Art(this,1,source.Rectangle(1),assets,shader);
        source.Art(this,2,source.Rectangle(2),assets,shader);
        int[] captions=[0x032A,0x032B,0x032C,0x020D,0x0225,0x0292];
        string[] initial=[city.Name,Number(availableTroops),Number(city.Gold),Number(city.Supplies),
            city.Morale is int morale?Number(morale):unknown,books is long count?Number(count):unknown];
        for(int index=0;index<captions.Length;index++)
        {
            string caption=messages.Records[captions[index]].Text??throw new InvalidDataException("Missing deployment header caption.");
            source.Text(this,3+index*2,default,caption,font,presets[13]);
            values[4+index*2]=source.Text(this,4+index*2,default,initial[index],font);
        }
        values[15]=source.Text(this,15,default,books is long available?$"{available}(-0)":unknown,font);
        source.Art(this,16,source.Rectangle(16),assets,shader);
        var tint=presets[13];
        source.Art(this,17,source.Rectangle(17),assets,shader,new(tint.LT,tint.RT,tint.LB,tint.RB));
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void Apply(DeploymentDraftResources resources)
    {
        values[6].Text=resources.Troops is { } troops?Number(troops.RemainingTroops):unknown;
        if(resources.Gold is not { } gold)values[8].Text=unknown;
        else if(gold.RemainingGold<0)values[8].SetRuns(new UiSourceTextRun(Number(gold.RemainingGold),negativeColour));
        else values[8].Text=Number(gold.RemainingGold);
        if(resources.CommandBooks is not { } books)
        {values[14].Text=unknown;values[15].Text=unknown;}
        else if(books.IsPublished)
        {
            values[14].Text=Number(books.CurrentBooks!.Value);
            string remaining=Number(books.RemainingBooks!.Value),spent=Number(books.SpentBooks);
            if(books.SpentBooks>0)values[15].SetRuns(new(remaining+"("),new("-"+spent,negativeColour),new(")"));
            else values[15].Text=remaining+"(-"+spent+")";
        }
        // A known invalid District has no writer. Preserve its prior labels.
    }

    private static string Number(long value)=>value.ToString(CultureInfo.InvariantCulture);
}
