namespace San14.Core;

/// <summary>Current CE Person credit fields with independent unknown values.
/// Identity/provenance, stage and tail belong to NativeAbilityRuntime; pending
/// merit belongs to NativeGrowthRuntime. Missing legacy modules are not seeded.</summary>
public sealed record NativePersonCreditRuntime(string Revision, short? Merit1F6,
    byte? FirstRewardCount1FA, byte? SecondRewardCount1FB, byte? Reset1FC);

public sealed partial record OfficerState
{
    public NativePersonCreditRuntime? NativeCreditRuntime { get; init; }
}
