using Godot;
using San14.Data;

/// <summary>Normal UI resources owned by one resident summary manager.
/// Every summary shares its font, textures and parsed layouts for this lifetime.</summary>
internal sealed class MapSummaryResources : IDisposable
{
    internal GameResourceRepository Repository { get; }
    internal UiAssets Assets { get; }
    internal MapFont Font { get; }
    internal Shader Shader { get; }
    internal MapTextPresets Presets { get; } = MapTextPresets.Create();
    internal MapSpritePalettes Palettes { get; } = MapSpritePalettes.Create();
    internal MapSummaryIndicatorPlayback IndicatorPlayback { get; set; } = new();
    private readonly Dictionary<string,UiSourceLayout> layouts=[];
    private readonly Dictionary<uint,UiSourceLayout> indexedLayouts=[];
    private MapMarkerLayout? bar;
    internal MapMarkerLayout Bar => bar??=CityGateMapMarkerSources.LoadBar(Repository);
    internal UiSourceLayout Layout(uint file)
    {
        if(!indexedLayouts.TryGetValue(file,out var layout))
        {layout=new(Repository,file);indexedLayouts.Add(file,layout);}
        return layout;
    }
    private readonly Dictionary<(string Pack,string Sprite),LoadedSprite> sprites=[];
    private readonly LocalizedUiSpriteSource source;
    internal MapSummaryResources(GameResourceRepository repository,string locale)
    {
        Repository=repository;Assets=new(repository);Font=new(repository,locale);
        Shader=UiSourceSprite.CreateShader();source=new(repository);
    }
    internal UiSourceLayout Layout(string name)
    {
        if(!layouts.TryGetValue(name,out var layout))
        {layout=new(MapMarkerLayout.LoadNamed(Repository,name));layouts.Add(name,layout);}
        return layout;
    }
    internal LoadedSprite Sprite(string pack,string sprite)
    {
        if(!sprites.TryGetValue((pack,sprite),out var image))
        {image=source.LoadTexturePackSprite(pack,sprite);sprites.Add((pack,sprite),image);}
        return image;
    }
    public void Dispose(){Font.Dispose();Assets.Dispose();Shader.Dispose();layouts.Clear();indexedLayouts.Clear();sprites.Clear();}
}
