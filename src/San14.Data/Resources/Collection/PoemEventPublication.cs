namespace San14.Data;

/// <summary>A precomposed poem scroll image placed in its original layout.
/// These are kind29 images, not message-manager text or replacement fonts.</summary>
public sealed record PoemEventImage(int NodeId, CollectionImageRoute Image);

public sealed record PoemEventInfoPublication(LoadedLayout Layout, CollectionPoemDetail Poem,
    string DescriptionCaption);

public sealed record PoemEventPublication(int ItemId, int LayoutIndex, string LayoutName,
    LoadedLayout Layout, int MaxTextScrollOffsetX, IReadOnlyList<PoemEventImage> Images,
    PoemEventInfoPublication Info)
{
    /// <summary>The title appreciation caller clears this input even though
    /// the source catalog records have it set.</summary>
    public bool InformationInitiallyVisible => false;
    public int InitialAnimationIndex => 2;
    public string InitialAnimationName => "09_ev_p_01_all";
    public IReadOnlyList<LcskAnimationClip> Animations { get; init; } = [];
}
