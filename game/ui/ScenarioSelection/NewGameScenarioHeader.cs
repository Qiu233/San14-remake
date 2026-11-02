using Godot;
using San14.Data;

/// <summary>The shared source banner for force selection and final confirmation.
/// Its independent completion, edition and scenario-kind slots follow the
/// selected scenario and application history, rather than static sample art.</summary>
internal static class NewGameScenarioHeader
{
    internal static void Configure(Control parent, Vector2 mount, GameResourceRepository repository,
        string locale, UiAssets assets, Shader shader, MapFont font, NewGameForcePageHeader content)
    {
        var source = new UiSourceLayout(repository, 0x6C522E54);
        var host = new Control { Name = "NewGameScenarioHeader", Position = mount,
            Size = source.Rectangle(0).Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(host);
        var sprites = new LocalizedUiSpriteSource(repository);
        string language = locale switch { "TC" => "tw", "SC" => "ch", _ => throw new ArgumentOutOfRangeException(nameof(locale)) };
        LoadedSprite Local(string name) => sprites.LoadTexturePackSprite("texture_lang_" + language, language + "_" + name);
        Bind(1, "CompletedScenario", content.CompletedMarkVisible == true ? Local("020_sho_seiha") : null);
        source.Art(host, 2, source.Rectangle(2), assets, shader);
        Bind(3, "EditionBadge", content.EditionBadge);
        Bind(4, "FirstScenarioKind", content.ScenarioTypeFlag ? Local("020_sho_mrk_scenario_1") : null);
        Bind(5, "SecondScenarioKind", !content.ScenarioTypeFlag ? Local("020_sho_mrk_scenario_2") : null);
        var preset = MapTextPresets.Create()[27];
        var unknown = source.Text(host, 6, default, "?", font, preset, rectangle: source.Rectangle(1));
        unknown.Name = "ScenarioCompletionUnknown"; unknown.Visible = content.CompletedMarkVisible is null;
        source.Text(host, 6, default, content.Year, font, preset);
        source.Text(host, 7, default, content.Month, font, preset);
        source.Text(host, 8, default, content.Title, font, preset);
        host.SetMeta("source_layout", source.Source.Source.ResourceUri);
        host.SetMeta("completion_known", content.CompletedMarkVisible.HasValue);

        void Bind(int node, string name, LoadedSprite? sprite)
        {
            Rect2 box = source.Rectangle(node);
            var image = new UiSourceSprite { Name = name, Position = box.Position, Size = box.Size,
                Visible = sprite is not null };
            host.AddChild(image);
            if (sprite is not null)
            {
                image.Configure(shader, assets.SrgbSprite(sprite.Definition.Name, sprite), UiSourceSprite.Blend.Ordinary);
                image.SetMeta("caption_sprite", sprite.Definition.Name);
            }
            image.SetMeta("source_node_index", node);
            image.SetMeta("source_layout", source.Source.Source.ResourceUri);
        }
    }
}
