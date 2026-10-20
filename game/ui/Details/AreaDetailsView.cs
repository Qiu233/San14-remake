using San14.Data;
using San14.Client.Gameplay;

public partial class AreaDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, AreaDetailView view, PreviewText text, Action close)
    {
        int? ownedPreset = view.OwnershipCoverage switch
        {
            AreaOwnershipCoverage.BelowHalf => 16,
            AreaOwnershipCoverage.Complete => 17,
            AreaOwnershipCoverage.Partial => 18,
            null => null,
            _ => throw new InvalidDataException("Unknown area ownership presentation.")
        };
        IReadOnlyDictionary<string, int>? presets = ownedPreset is int preset
            ? new Dictionary<string, int> { ["owned"] = preset, ["ownedSlash"] = preset, ["total"] = preset }
            : null;
        string? placeIcon = view.PlaceKind switch
        {
            AreaPlaceKind.City => "000_cmn_icon_kyoten",
            AreaPlaceKind.Gate => "000_cmn_icon_gate",
            AreaPlaceKind.Port => "000_cmn_icon_port",
            null => null,
            _ => throw new InvalidDataException("Unknown area place presentation.")
        };
        ConfigureLayout(repository, "res://ui/area-details.json", "AreaDetails", "detail_area_title", text,
            view.Content.Values, view.Content.PortraitAppearanceCode, view.Content.Caption, close, mapBackdrop: true,
            valueTextPresets: presets, selectionIcon: placeIcon, forceAppearance: view.ForceAppearance,
            neighborFlags: view.NeighborFlags, miniMap: view.MiniMap);
        SetMeta("area_id", view.AreaId);
    }
}
