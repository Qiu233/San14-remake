using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int? ReadStoryGovernmentRepresentativeCurrent(GameState state, SourceWorldContext current, int code)
    {
        if (code is < 127 or > 131) throw StoryAppointmentMissing("representative_opcode");
        int group = code - 127;
        _baseCapacityInputs.BindCurrent(state, current);
        var force = current.CurrentPlayerForce ?? throw StoryAppointmentMissing("global_player_Force");
        int forceId = StoryAppointmentNeed(current.ReadForcePhysicalSlot(force), "player_Force_identity");
        int doctrine = ReadStoryForceDoctrineCurrent(state, current, force);
        if (doctrine == 0) return null;
        var definition = _scenario.Snapshot.DoctrineParameters?.Doctrines.FirstOrDefault(row => row.Slot == doctrine)
            ?? throw StoryAppointmentMissing("representative_Doctrine_definition");
        if (definition.RawBytes.Length != 102) throw StoryAppointmentMissing("representative_Doctrine_format");
        var values = state.NativeArmySimulation?.Parameters.Int32Values
            ?? throw StoryAppointmentMissing("representative_policy_owner");
        int expansion = Byte(BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(forceId, group));
        int capacity = definition.RawBytes[91 + group] + expansion;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        int dimension = group switch { 0 => 1, 1 => 0, _ => group };
        int best = 0;
        int? winner = null;
        for (int ordinal = 0; ordinal < capacity; ordinal++)
        {
            //The native department accessor selects Person0 above slot6.
            int id = ordinal < 7 ? Word(BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group, ordinal)) : 0;
            int person = StoryAppointmentNeed((id <= 6000 ? current.ReadPersonLookupReference(id)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "representative_Person_pointer");
            if (!StoryAppointmentNeed(current.ReadPersonNativeValidity(person), "representative_Person_validity")) continue;
            int identity = StoryAppointmentNeed(current.ReadPersonIdentity(person), "representative_Person_identity");
            int ability = abilities.ResolveDimension(state, identity, dimension)
                ?? throw StoryAppointmentMissing("representative_current_ability");
            if (ability <= best) continue; //Strict comparison preserves the first equal candidate.
            best = ability; winner = person;
        }
        return winner;

        int Word(string key) => values.TryGetValue(key, out int value) && value is >= 0 and <= ushort.MaxValue
            ? value : throw StoryAppointmentMissing("representative_word:" + key);
        int Byte(string key) => values.TryGetValue(key, out int value) && value is >= 0 and <= byte.MaxValue
            ? value : throw StoryAppointmentMissing("representative_byte:" + key);
    }

    private static T StoryAppointmentNeed<T>(T? value, string field) where T : struct =>
        value ?? throw StoryAppointmentMissing(field);
    private static InvalidDataException StoryAppointmentMissing(string field) => new("story.person_appointment." + field);
}
