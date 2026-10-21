using San14.Data;
using San14.Client.Gameplay;

/// <summary>Binds an independent force publication to the resource-defined layout.</summary>
public partial class ForceDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, ForceDetailView view, PreviewText text, Action close)
    {
        DetailContent content = view.Content;
        ConfigureLayout(repository, "res://ui/force-details.json", "ForceDetails", "detail_force_title",
            text, content.Values, content.PortraitAppearanceCode, content.Caption, close,
            miniMap: view.MiniMap, rulerNameplate: view.Nameplate);
        SetMeta("force_id", view.ForceId);
        SetMeta("doctrine_reference_known", view.DoctrineReferenceKnown);
        SetMeta("doctrine_level_verified", false);
    }
}
