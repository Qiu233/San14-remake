namespace San14.Data;

public sealed record PersonCategoryTemporaryResult(int? NativeReturn, int AppendedNodes,
    string? UnresolvedOwner);

/// <summary>Actual2C56D0 appends category-matching current selected
/// personalities to the caller's existing primary pointer chain. It neither
/// clears the output nor applies the separate effect/activation predicates.</summary>
public static class PersonCategoryTemporarySource
{
    public static PersonCategoryTemporaryResult Append(SourceWorldContext current,
        int personPhysical, uint category, HexListHandle output)
    {
        int appended = 0;
        try
        {
            if (!Known(current.ReadPersonNativeValidity(personPhysical), "Person_validity")) return new(0, 0, null);
            int nativeReturn = 0;
            for (int index = 0; index < 9; index++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, personPhysical, index)
                    ?? throw Missing("Person_230730_selected_pointer");
                if (!Known(current.ReadSkillPersonalityValidity(trait), "Personality_validity")) continue;
                if (Known(current.ReadPersonalityByte(trait, 0xA4), "Personality_categoryA4") != category) continue;
                bool found = false;
                var node = current.ReadSkillTemporaryHead(output) ?? throw Missing("output_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadSkillTemporaryPayload(node) ?? throw Missing("output_Personality_pointer");
                    if (payload.PhysicalSlot == trait.PhysicalSlot) { found = true; break; }
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("output_next");
                }
                if (found) continue;
                if (current.AppendPersonCategoryTemporary(output, trait)) appended++;
                //Source sets return1 after attempted018CC0, including node
                //allocation failure; this is not the number of actual nodes.
                nativeReturn = 1;
            }
            return new(nativeReturn, appended, null);
        }
        catch (InvalidDataException error) { return new(null, appended, error.Message); }
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("Current2C56D0 owner is unknown: " + field);
}

public sealed partial class SourceWorldContext
{
    internal bool AppendPersonCategoryTemporary(HexListHandle output, PersonalityReference trait)
    {
        ConditionalRegistryEnter();
        try { return AppendSkillTemporaryPersonality(output, trait); }
        finally { ConditionalRegistryExit(); }
    }
}
