using San14.Data;

/// <summary>The Ending retains its own request and receipt; only the list view
/// is shared with ordinary rank promotions.</summary>
internal partial class EndingKingdomSelectionView : KingdomSelectionView
{
    internal new void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<KingdomNameSelectionRow> candidates, Func<bool> admitted,
        Action<int?> closed, Func<int> scrollSpeed)
    {
        base.Configure(repository, text,
            candidates, admitted, closed, scrollSpeed);
        Name = "EndingKingdomSelection";
    }
}
