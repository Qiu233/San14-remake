namespace San14.Core;

internal static class NativePersonCreditRuntimeValidation
{
    public static void Validate(GameState state)
    {
        foreach (OfficerState person in state.Officers.Values)
            if (person.NativeCreditRuntime is { } credit &&
                (string.IsNullOrWhiteSpace(credit.Revision) || person.NativeAbilityRuntime is null ||
                 person.NativeGrowthRuntime is null))
                throw new InvalidDataException("Current Person credit has no complete identity or pending-merit owner.");
    }
}
