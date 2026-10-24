namespace San14.Core;

/// <summary>Acquired tactic-book counts by physical force and Gem slot.
/// All source words, including slot zero, survive saving unchanged until a
/// reached acquisition updates one count. A missing state remains unknown.</summary>
public sealed record GemForceInventoryState(Dictionary<int, ushort[]> Forces);
