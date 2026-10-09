namespace San14.Core;

internal static class NativePersonGrowthRuntimeValidation
{
    public static void Validate(GameState state)
    {
        foreach (OfficerState person in state.Officers.Values)
        {
            if (person.NativeGrowthRuntime is not { } growth) continue;
            if (person.NativeAbilityRuntime is null || string.IsNullOrWhiteSpace(growth.Revision) ||
                string.IsNullOrWhiteSpace(growth.SourceExperienceUri) ||
                growth.SourceExperienceSha256 is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) ||
                state.ContentHashes.GetValueOrDefault(growth.SourceExperienceUri) != hash ||
                growth.Experience13C is { Length: not 5 })
                throw new InvalidDataException("Current Person growth has inconsistent identity, source or XP window.");
            if (growth.AbilityGrowthNotices is not { } notices) continue;
            var indices = new HashSet<int>();
            if (notices.Length > 5 || notices.Any(notice => notice is null ||
                notice.AbilityIndex is < 0 or > 4 || !indices.Add(notice.AbilityIndex)))
                throw new InvalidDataException("Current Person growth notices have invalid ability identities.");
        }
    }
}
