namespace San14.Core;

/// <summary>Current Person XP and the single-Person vector reached by the
/// native growth writer. Identity belongs to the accompanying ability runtime.
/// Missing saved fields remain unknown. Merit has its own independent owner.</summary>
public sealed record NativePersonGrowthRuntime(string Revision,
    string SourceExperienceUri, string SourceExperienceSha256,
    uint[]? Experience13C,
    NativePersonAbilityGrowthNotice[]? AbilityGrowthNotices);

/// <summary>One native0x14-byte growth notification's semantic fields. Array
/// position retains this Person's vector order. It does not establish the
/// separate global tree's order over native Person pointer keys.</summary>
public sealed record NativePersonAbilityGrowthNotice(int AbilityIndex,
    byte After4, byte Before5, int ReportContext8, int RawC, int Raw10);

public sealed partial record OfficerState
{
    public NativePersonGrowthRuntime? NativeGrowthRuntime { get; init; }
}
