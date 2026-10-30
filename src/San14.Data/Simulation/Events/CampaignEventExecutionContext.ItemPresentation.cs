namespace San14.Data;

/// <summary>A real item-layer request. A null index hides that layer;
/// index zero is an ordinary valid illustration. A null spec is a known
/// native-invalid Item no-op, distinct from either presentation request.</summary>
internal sealed record CampaignEventItemPresentationSpec(int? IllustrationIndex, int Setting);

internal sealed partial class CampaignEventExecutionContext
{
    /// <summary>89/3B1DA0 reads a literal Item-table selector. The current
    /// definition already owns byte51 plus the retained4F bit10 image bank.
    /// This does not evaluate the playback manager's live suppression gate.</summary>
    public CampaignEventItemPresentationSpec? ReadItemPresentation(int selector, int setting)
    {
        if (selector is < 1 or > 201) return new(null, setting);
        var item = Queries.ReadConditionItemTableSlot(selector)
            ?? throw Missing("Item presentation table pointer");
        if (!Known(Queries.ReadConditionItemValidity(item), "Item presentation native validity")) return null;
        int illustration = Known(Queries.ReadConditionItemIllustrationIndex(item), "Item presentation current illustration");
        return new(illustration, setting);
    }
}
