namespace San14.Core;

public sealed partial record OfficerState
{
    /// <summary>Current resource dialogue vocabulary, independent of voice.</summary>
    public byte? DialogueStyle { get; init; }
    /// <summary>Current stock voice type. Null is an unknown historical value.</summary>
    public byte? VoiceType { get; init; }
    public ushort? BirthYear { get; init; }
    /// <summary>Current generation within the person's bloodline.</summary>
    public byte? FamilyGeneration { get; init; }
    /// <summary>Null is unknown; empty preserves a known empty surname.</summary>
    public string? CurrentSurname { get; init; }
    public string? CurrentGivenName { get; init; }
    /// <summary>Null is unknown; empty means that no courtesy name is set.</summary>
    public string? CurrentCourtesyName { get; init; }
}
