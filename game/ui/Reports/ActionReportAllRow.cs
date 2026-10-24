using Godot;
using San14.Core;
using San14.Data;

/// <summary>The original all-records row. Fit2 removes source line breaks
/// before parsing body colours and fitting one visible line horizontally.</summary>
internal partial class ActionReportAllRow : Control
{
    private UiSourceText body=null!;
    internal CampaignHistoryRecord? Record {get;private set;}
    internal string SourceText {get;private set;}="";

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        var source=new UiSourceLayout(repository,0xE0AA945F);
        Size=new(source.Nodes[0].Value44,source.Nodes[0].Value46);
        MouseFilter=MouseFilterEnum.Ignore;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        body=source.Text(this,2,default,"",font);
        // The initializer hides cursor group3; the list owns row focus.
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void Apply(CampaignHistoryRecord? record)
    {
        Record=record;
        if(record is null)return;
        var window=record.Text;
        int length=window.IndexOf(0);
        if(length<0)length=window.Length;
        string text=new(window.Take(length).Select(code=>(char)code).ToArray());
        ApplyText(text);
    }

    internal void ApplyPlainText(string text)
    {
        body.SetRuns([new UiSourceTextRun(text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal))]);
        SourceText = text;
    }

    private void ApplyText(string text)
    {
        // Other format families, escaped carets and isolated CR require their
        // own source consumers. Do not silently discard an unknown command.
        body.SetKnownColourLine(text);
        SourceText=text;
    }
}
