using San14.Data;
using San14.Client.Gameplay;

/// <summary>Binds an independent officer publication; final ability modifiers remain unresolved.</summary>
public partial class OfficerDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, OfficerDetailView view, PreviewText text, Action close)
    {
        DetailContent content = view.Content;
        ConfigureLayout(repository, "res://ui/officer-details.json", "OfficerDetails", "detail_officer_title",
            text, content.Values, content.PortraitAppearanceCode, content.Caption, close);
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
