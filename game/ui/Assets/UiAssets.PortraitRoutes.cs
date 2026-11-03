using San14.Data;

internal sealed partial class UiAssets
{
    // The card publication supplies a route, while the rendering page borrows
    // this application's already prepared encoded image and native texture.
    internal LoadedUiSpriteSource? RequireStockPortraitSource(OfficerPortraitRoute? route)
    {
        if (route is not { Kind: OfficerPortraitRouteKind.StockNamed }) return null;
        return Portraits.RequireSource(
            route.ResourceName ?? throw new InvalidDataException("Officer portrait pack is missing."),
            route.SpriteName ?? throw new InvalidDataException("Officer portrait sprite is missing."));
    }
}
