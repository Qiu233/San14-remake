using Godot;
using San14.Data;

internal partial class EventEditorHelpView
{
    private void ConfigureAdviser(UiSourceLayout selector)
    {
        var fixedAdviser = catalog.Mode == 1 ? repository.LoadEventEditorHelpAdviser(catalog.Locale) : null;
        if (fixedAdviser is null && campaignAdviser is null) return;
        int personId = fixedAdviser?.PersonId ?? campaignAdviser!.PersonId;
        string personName = fixedAdviser?.Name ?? campaignAdviser!.Name;
        string body = fixedAdviser?.Body ?? campaignAdviser!.Body;
        var portraitRoute = fixedAdviser?.StandingPortrait ?? campaignAdviser!.StandingPortrait;
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
            portraitRoute.LoadSprites(sprites.LoadTexturePackSprite));
        portrait.Scale = new(pane.Nodes[1].HeaderValues[7], pane.Nodes[1].HeaderValues[8]);

        var captionSource = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "12_01_01_offset_01"));
        var captionBox = pane.Rectangle(2);
        var caption = new Control { Name = "HelpAdviserCaption", Position = captionBox.Position,
            Size = captionSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(caption);
        captionSource.Art(caption, 1, captionSource.Rectangle(1), assets, shader);
        captionSource.Text(caption, 2, default, personName, font).Name = "HelpAdviserName";
        AddParagraph(caption, captionSource, 3, body, "HelpIntroduction");
        host.SetMeta("person_id", personId);
        host.SetMeta("source_layout", pane.Source.Source.ResourceUri);
        if (fixedAdviser is not null) host.SetMeta("person_source", fixedAdviser.PersonSource.ResourceUri);
    }
}
