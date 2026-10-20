namespace San14.Data;

/// <summary>A resolved pointer is cached even when its reached update is unknown.
/// Null Update preserves the prior publication; known null hides an invalid speaker.</summary>
public sealed record AreaAdministrationOfficerSpeechRefresh(int PersonPhysicalSlot,
    AreaAdministrationQuery<AreaAdministrationOfficerSpeechText?>? Update);

/// <summary>One prepared UI message. It carries no world intent, pages or random state.</summary>
public sealed record AreaAdministrationOfficerSpeechText(int AdviceKey, int RootMessageId,
    int? MessageId, IReadOnlyList<NarrationMessageChoice> Choices, string Text);
