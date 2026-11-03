using Godot;
using San14.Data;

internal partial class UiSourcePortrait
{
    // The caller has already qualified the current-person resource route.
    // Pages borrow the application's original encoded image; no CPU pixels
    // or per-page portrait upload is constructed by these common faces.
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedUiSpriteSource portrait)
        => ConfigureLayoutPane(repository, assets, shader, portrait, assets.SourceLayout(0xACFB207E), 1, 3);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        LoadedUiSpriteSource portrait, UiSourceLayout source)
        => ConfigureLayoutPane(repository, assets, shader, portrait, source, 1, 3);
    internal void ConfigureFace2(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedUiSpriteSource portrait)
        => ConfigureLayoutPane(repository, assets, shader, portrait, assets.SourceLayout(0xA9487A40), 1, 3);
    internal void ConfigureFace3(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedUiSpriteSource portrait)
        => ConfigureLayoutPane(repository, assets, shader, portrait, assets.SourceLayout(0xB20B608C), 3, 2);
    internal void ConfigureFace5(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedUiSpriteSource portrait)
        => ConfigureLayoutPane(repository, assets, shader, portrait, assets.SourceLayout(0xAED4739D), 1, 3);
    internal void ConfigureMediation(GameResourceRepository repository, UiAssets assets, Shader shader,
        LoadedUiSpriteSource portrait, int slot)
        => ConfigureLayoutPane(repository, assets, shader, portrait, assets.SourceLayout(0x1DE0C63E), 14 + slot * 11, 2);
    internal void ConfigureOfficerCard(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedUiSpriteSource portrait)
        => ConfigureLayoutPane(repository, assets, shader, portrait, assets.SourceLayout(0xCB7DEBFC), 9, 3);
    internal void ConfigureMapHeader(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedUiSpriteSource portrait)
    {
        var source=assets.SourceLayout(0xB639C019);var root=source.Nodes[0];
        Size=new(root.Value44*root.HeaderValues[7],root.Value46*root.HeaderValues[8]);
        ConfigureLayoutPane(repository,assets,shader,portrait,source,1,3);
    }
}
