using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Shared illness setter used by normal Story and the existing editor result.
    //Callers retain their own admissions and source notification tails.
    private static GameState ApplyPersonIllnessCurrent(GameState state, SourceWorldContext current,
        int physical, int requested, bool skipPersonality = false)
    {
        if (!PersonIllnessNeed(current.ReadPersonNativeValidity(physical), "Person_validity") ||
            unchecked((uint)requested) >= 4) return state;
        int selector = PersonIllnessNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "environment_selector");
        if (PersonIllnessNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector != 0 ? 0xD0 : 0x2B4),
            "environment") != 0) return state;
        if (!skipPersonality)
        {
            var traits = new List<PersonalityReference>(9);
            int total = 0;
            for (int i = 0; i < 9; i++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, physical, i)
                    ?? throw PersonIllnessMissing("Person_personality");
                if (!PersonIllnessNeed(current.ReadSkillPersonalityValidity(trait), "personality_validity")) continue;
                traits.Add(trait);
                total = unchecked(total + PersonIllnessEffectCurrent(current, trait));
            }
            if (total != 0)
            {
                var pool = current.ReadOwnedCurrentListPool() ?? throw PersonIllnessMissing("shared_pool");
                var list = current.CreatePersonInjuryTemporary();
                bool immune = false;
                try
                {
                    current.ClearPersonInjuryTemporary(list, false);
                    foreach (var trait in traits) current.AppendPersonInjuryTemporary(list, trait);
                    var node = current.ReadSkillTemporaryHead(list) ?? throw PersonIllnessMissing("temporary_head");
                    while (node.Identity.HasValue)
                    {
                        var trait = current.ReadSkillTemporaryPayload(node) ?? throw PersonIllnessMissing("temporary_personality");
                        int effect = PersonIllnessEffectCurrent(current, trait);
                        if (effect > 0 && PersonIllnessNeed(current.ReadPersonalityByte(trait, 0xA4), "personality_condition") is not (9 or 21))
                        { immune = true; break; }
                        node = current.ReadHexListNodeNext(node) ?? throw PersonIllnessMissing("temporary_next");
                    }
                }
                finally
                {
                    current.ClearPersonInjuryTemporary(list, true);
                    state = state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw PersonIllnessMissing("pool_owner"))
                        with { Indexes = pool.ExportOwnedState() });
                }
                if (immune) return state;
            }
        }
        return WritePersonHealthCurrent(state, current, physical, requested);
    }

    private static int PersonIllnessEffectCurrent(SourceWorldContext current, PersonalityReference trait)
    {
        for (int pair = 0; pair < 2; pair++)
        {
            if (PersonIllnessNeed(current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "effect_code") != 53) continue;
            ushort value = PersonIllnessNeed(current.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC), "effect_value");
            return Math.Clamp((int)unchecked((short)value), -100, 2000);
        }
        return 0;
    }

    //Direct recovery deliberately bypasses the event illness environment and
    //immunity gates, while publishing the same actual Person health owner.
    private static GameState WritePersonHealthCurrent(GameState state, SourceWorldContext current, int physical, int value)
    {
        ushort id = PersonIllnessNeed(current.ReadPersonIdentity(physical), "Person_identity");
        var person = state.Officers.GetValueOrDefault(id) ?? throw PersonIllnessMissing("Person_owner");
        var health = new OfficerHealth(value);
        current.SetOfficerHealth(id, health);
        return state with { Officers = new(state.Officers) { [id] = person with { Health = health } } };
    }

    private static T PersonIllnessNeed<T>(T? value, string owner) where T : struct
        => value ?? throw PersonIllnessMissing(owner);
    private static InvalidDataException PersonIllnessMissing(string owner) => new("person.illness.current." + owner);
}
