using Godot;
using San14.Data;

internal partial class EventEditorHelpView
{
    private void ConfigureAdviser(UiSourceLayout selector)
    {
        var adviser = repository.LoadEventEditorHelpAdviser(catalog.Locale);
        var pane = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "12_01_01_offset_00"));
        var mount = selector.Rectangle(1);
        var host = new Control { Name = "HelpAdviser", Position = mount.Position,
            Size = pane.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        directory.AddChild(host);

        var portraitBox = pane.Rectangle(1);
        var portrait = new UiSourceStandingPortrait { Name = "HelpAdviserStanding", Position = portraitBox.Position, AnimateFrames = false };
        host.AddChild(portrait);
        var sprites = new LocalizedUiSpriteSource(repository);
        portrait.Configure(repository, assets, shader,
            adviser.StandingPortrait.LoadSprites(sprites.LoadTexturePackSprite));
        portrait.Scale = new(pane.Nodes[1].HeaderValues[7], pane.Nodes[1].HeaderValues[8]);

        var captionSource = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "12_01_01_offset_01"));
        var captionBox = pane.Rectangle(2);
        var caption = new Control { Name = "HelpAdviserCaption", Position = captionBox.Position,
            Size = captionSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(caption);
        captionSource.Art(caption, 1, captionSource.Rectangle(1), assets, shader);
        captionSource.Text(caption, 2, default, adviser.Name, font).Name = "HelpAdviserName";
        AddParagraph(caption, captionSource, 3, adviser.Body, "HelpIntroduction");
        host.SetMeta("person_id", adviser.PersonId);
        host.SetMeta("source_layout", pane.Source.Source.ResourceUri);
        host.SetMeta("person_source", adviser.PersonSource.ResourceUri);
    }
}
