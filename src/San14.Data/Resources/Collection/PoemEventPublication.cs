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
    /// <summary>Both callers initially hide the information group. The source
    /// descriptor flag controls its footer hint, not this visibility.</summary>
    public bool InformationInitiallyVisible => false;
    /// <summary>The actual second-footer hint. The descriptor's flag selects
    /// this caption only; it does not initially display information or change
    /// the reading, information and exit phases.</summary>
    public int InformationFooterHintMessageId { get; init; } = 0x2986;
    public int InitialAnimationIndex => 2;
    public string InitialAnimationName => "09_ev_p_01_all";
    public IReadOnlyList<LcskAnimationClip> Animations { get; init; } = [];
}
