namespace San14.Data.AI.Original;

/// <summary>A reference to an existing world-owned military group. Group zero
/// is the source placeholder object, not a null reference or a District.</summary>
public readonly record struct OriginalAiTroopsReference(int GroupId);
