using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The source CDispatchHeader mounted in the page's canonical canvas.
/// It displays the retained draft aggregate and never chooses a source City.</summary>
internal partial class GeneralUnitTargetHeaderView : Control
{
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapFont font=null!;
    private bool ownsResources;
    private readonly Dictionary<int,UiSourceText> values=[];

    internal void Configure(GameResourceRepository repository,PreviewText text,
        GeneralUnitTargetHeader publication, AdministrationPageChrome.Resources? resources = null)
    {
        Name="GeneralUnitTargetDispatchHeader";MouseFilter=MouseFilterEnum.Ignore;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        font=resources?.Font??new(repository,text.SourceLocale);
        var root=assets.SourceLayout(GeneralUnitTargetHeaderSources.LayoutName);
        var source=assets.SourceLayout(GeneralUnitTargetHeaderSources.ContentLayoutName);
        Size=root.Rectangle(0).Size;
        //7D3200 replaces root node1; its authored descendants are not drawn a
        //second time. Root node19 (advice) is explicitly hidden by that caller.
        Vector2 mount=root.Rectangle(GeneralUnitTargetHeaderSources.ContentMountNode).Position;
        // The header borrows the containing target page's static resources.
        foreach(var node in source.Nodes)
        {
            if(node.Type is 1 or 3)source.Art(this,node.Index,source.Rectangle(node.Index,mount),assets,shader);
            else if(node.Type==6)source.Blur(this,node.Index,source.Rectangle(node.Index,mount));
        }
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        var presets=MapTextPresets.Create();
        foreach(var (node,message) in GeneralUnitTargetHeaderSources.Captions)
            source.Text(this,node,mount,messages[message].Text??
                throw new InvalidDataException($"Missing dispatch header caption Data1/{message:X}."),font,presets[13]);
        foreach(int node in new[]{4,6,8,10,12,15,17})
            values[node]=source.Text(this,node,mount,"",font,presets[0]);
        SetMeta("source_layout",root.Source.Source.ResourceUri);
        SetMeta("source_content_layout",source.Source.Source.ResourceUri);
        SetMeta("source_mount_node",GeneralUnitTargetHeaderSources.ContentMountNode);
        Apply(publication);
    }

    internal void Apply(GeneralUnitTargetHeader publication)
    {
        values[4].Text=Number(publication.ArmyCount);
        values[6].Text=Number(publication.TotalTroops);
        values[8].Text=Number(publication.DistinctSourceCount);
        values[10].Text=publication.TargetName;
        values[12].Text=Number(publication.TargetTroops);
        values[15].Text=Number(publication.CommandBooks);
        values[17].SetKnownColourLine(publication.CommandBooksText);
    }

    private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
    public override void _ExitTree(){if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}}
}
