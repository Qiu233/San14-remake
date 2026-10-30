using San14.Data;
using San14.Client.Gameplay;

/// <summary>Hosts the current three-page inspector and the separate pre-game preview.</summary>
public partial class OfficerDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, OfficerDetailView view, PreviewText text, Action close,
        bool showReturnButton = true, bool showHeader = true)
    {
        DetailContent content = view.Content;
        if (showReturnButton || showHeader) ConfigureCommonBackdrop(repository);
        ConfigureLayout(repository, "res://ui/officer-details.json", "OfficerDetails", "detail_officer_title",
            text, content.Values, content.PortraitAppearanceCode, content.Caption, close,
            showReturnButton: showReturnButton, showHeader: showHeader);
        SetMeta("officer_id", view.OfficerId);
        if (view.BaseAbilities is { } abilities) SetMeta("base_abilities", abilities.ToArray());
        // Rank definitions stay separate from an unresolved final ability sum.
        if (view.RulerRankId is int rankId && view.RulerRankBonus is { } bonus)
        {
            SetMeta("ruler_rank_id", rankId);
            SetMeta("ruler_rank_bonus", bonus.ToArray());
        }
    }
}
