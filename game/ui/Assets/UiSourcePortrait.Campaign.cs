using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class UiSourcePortrait
{
    internal void ConfigureCampaign(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
    {
        var source = new UiSourceLayout(repository,0x14AA7945);
        Size = source.Rectangle(0).Size;
        var node = LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[3]);
        ConfigureSource(repository,assets,shader,portrait,source,3,node.Groups.Single().ComponentCount);
    }
}
