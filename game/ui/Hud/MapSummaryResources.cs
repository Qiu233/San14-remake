using Godot;
using San14.Data;

/// <summary>A map-local summary binding. The application owns its immutable UI
/// resources; playback, panel instances and current receivers stay with the map.</summary>
internal sealed class MapSummaryResources
{
    internal GameResourceRepository Repository { get; }
    internal UiAssets Assets { get; }
    internal MapFont Font { get; }
    internal Shader Shader { get; }
    internal MapTextPresets Presets { get; } = MapTextPresets.Create();
    internal MapSpritePalettes Palettes { get; } = MapSpritePalettes.Create();
    internal MapSummaryIndicatorPlayback IndicatorPlayback { get; set; } = new();
    private MapMarkerLayout? bar;
    // Bar's typed geometry metadata is a local read-only wrapper. Its art is
    // already in the same application Assets; this wrapper owns no GPU object.
    internal MapMarkerLayout Bar => bar ??= CityGateMapMarkerSources.LoadBar(Repository);
    internal MapSummaryResources(GameResourceRepository repository, AdministrationPageChrome.Resources resources)
    {
        Repository = repository; Assets = resources.Assets; Font = resources.Font; Shader = resources.SpriteShader;
    }
    internal UiSourceLayout Layout(uint file) => Assets.SourceLayout(file);
    internal UiSourceLayout Layout(string name) => Assets.SourceLayout(name);
    internal LoadedSprite Sprite(string pack, string sprite) => Assets.DeclaredSprites.LoadTexturePackSprite(pack, sprite);
}
