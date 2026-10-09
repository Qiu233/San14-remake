using San14.Data;
using San14.Client.Gameplay;

/// <summary>Binds an independent city publication to the resource-defined layout.</summary>
public partial class CityDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, CityDetailView view, PreviewText text, Action close)
    {
        DetailContent content = view.Content;
        ConfigureLayout(repository, "res://ui/city-details.json", "CityDetails", "detail_city_title",
            text, content.Values, content.PortraitAppearanceCode, content.Caption, close);
        SetMeta("current_city_capacity_unavailable", view.UnavailableCapacities.ToArray());
    }
}
