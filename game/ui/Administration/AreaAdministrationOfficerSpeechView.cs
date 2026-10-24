using Godot;
using San14.Data;

/// <summary>The region page's source node97, with its own ordinary paragraph
/// profile. The presenter prepares the message once per resolved person.</summary>
internal partial class AreaAdministrationOfficerSpeechView : Control
{
    private UiSourceParagraph body=null!;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        MouseFilter=MouseFilterEnum.Ignore;
        var source=new UiSourceLayout(repository,0xC7A85B99);
        source.Art(this,1,source.Rectangle(1),assets,shader);
        var rectangle=source.Rectangle(2);
        var profile=MapFontProfile.FromNode(source.Nodes[2],source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[0]) with {RawAlign=0,RawLineArrange=1,WidthFitMode=3,RawCharacterMode=2};
        body=new UiSourceParagraph{Name="SpeechBody",Position=rectangle.Position,Size=rectangle.Size};
        AddChild(body);body.Configure(font,profile);
        SetMeta("source_layout",source.Source.Source.ResourceUri);Hide();
    }

    internal void ApplyText(string? expanded)
    {
        SetMeta("speech_available",true);
        if(expanded is null){body.Apply("");Hide();return;}
        body.Apply(expanded);Show();
    }

    internal void ApplyUnknown(string label)
    {
        body.Apply(label);SetMeta("speech_available",false);Show();
    }
}
