namespace San14.Core;

public sealed partial record OfficerState
{
    /// <summary>Current formation capability bits after fresh post-load and later
    /// edits. Independent of BaseFormationIds, which records static source skills.
    /// Null means this saved campaign has no current capability knowledge.</summary>
    public uint? CurrentFormationMask { get; init; }
}
