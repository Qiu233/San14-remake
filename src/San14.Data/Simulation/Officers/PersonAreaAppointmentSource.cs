using San14.Core;

namespace San14.Data;

internal static class PersonAreaAppointmentSource
{
    internal static int AdditionalCount(GameState state, SourceWorldContext current, int physical)
    {
        if (!K(current.ReadPersonNativeValidity(physical), "person_validity")) return 0;
        var force = current.ReadPersonForce(physical) ?? throw Missing("person_force");
        if (K(force.PhysicalSlot, "person_force_pointer") is >= 46 and <= 50) return 0;
        int id = K(current.ReadPersonIdentity(physical), "person_identity");
        bool special = unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
            unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
        if (special && K(current.ReadPersonFirstLocation(physical), "special_first_location") is >= 1 and <= 61) return 0;
        if ((K(current.ReadArmyReturnPersonFlags196(physical), "person_flags") & 0x4000) != 0) return 0;
        byte status = K(current.ReadPersonStatus(physical), "person_status");
        if (!state.Officers.TryGetValue(id, out var officer) || current.ResolveOfficerSource(id) != physical)
            throw Missing("person_domain_owner");
        // Ruler kind2 is two, unlike the kind1 capacity bonus. It still needs
        // the wrapper's current-user and mode gates before taking that path.
        if (status != 1 && (officer.MeritState?.RankStageId == 0 || OfficerMeritRules.HasNoSelections(officer.MeritState))) return 0;
        if (state.CampaignEventWorld?.CurrentUserIndex != -1) throw Missing("credit_current_user_gate");
        if (state.CampaignEventWorld.ScenarioMode == 3) throw Missing("credit_person_selection_mode");
        if (status == 1) return 2;
        byte stage = K(OfficerMeritSourceAdapter.EncodeStage(officer.MeritState), "credit_stage");
        if (stage == 0) return 0;
        int first = K(OfficerMeritSourceAdapter.EncodeFirstSelectionCount(officer.MeritState), "credit_first_count");
        int second = K(OfficerMeritSourceAdapter.EncodeSecondSelectionCount(officer.MeritState), "credit_second_count");
        int sum = 0;
        for (int i = 0; i < first; i++) Add(i + 1 < 7 ? i + 1 : 0);
        for (int i = 0; i < second; i++) Add(i + 4 < 7 ? i + 4 : 0);
        return sum;
        void Add(int slot)
        {
            if (K(OwnedArmyMoraleWaveInputs.ReadCreditEffectKind(state, slot), "credit_effect_kind") == 2)
                sum = unchecked(sum + K(OwnedArmyMoraleWaveInputs.ReadCreditEffectAddition(state, slot), "credit_effect_addition"));
        }
    }

    internal static int Add(GameState state, SourceWorldContext current,
        PersonAssignmentPrecallInputs.AreaAppointmentDraft draft, int person, int area)
    {
        if (unchecked(AdditionalCount(state, current, person) + 1) == 1)
        { draft.SetFirst(person, area); return 1; }
        int capacity = unchecked(AdditionalCount(state, current, person) + 1);
        int count = draft.Count(person);
        if (count >= capacity) return 0;
        for (int index = 0; index < count; index++) if (draft.Entry(person, index) == area) return 0;
        if (count < 0 || count >= draft.Length(person)) throw Missing("person_append_storage");
        var vector = draft.Materialize(person);
        vector.Values[count] = area;
        PersonAssignmentPrecallSource.SortAreaVector(vector.Values);
        draft.Write(person, vector with { ActiveCount1E8 = unchecked(count + 1) });
        return 1;
    }

    internal static int Change(GameState state, SourceWorldContext current,
        PersonAssignmentPrecallInputs.AreaAppointmentDraft draft, int area, ushort newId, ushort oldId)
    {
        int result = 1;
        if (oldId != 0)
        {
            int old = Resolve(current, oldId);
            if (K(current.ReadPersonNativeValidity(old), "old_person_validity"))
            {
                int count = draft.Count(old), at = 0;
                while (at < count && draft.Entry(old, at) != area) at++;
                if (at == count) result = 0;
                else
                {
                    current.SetAssignmentPersonAreaVector(old, draft.Materialize(old));
                    var removal = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, oldId);
                    if (!removal.NativeReturn.HasValue) throw new InvalidDataException(removal.UnresolvedOwner);
                    result = removal.NativeReturn.Value & 1;
                    if (removal.NativeReturn != 0)
                        draft.Write(old, current.ReadAssignmentPersonAreaVector(old) ?? throw Missing("removed_vector"));
                }
            }
        }
        if (newId != 0)
        {
            int added = Resolve(current, newId);
            if (K(current.ReadPersonNativeValidity(added), "new_person_validity"))
                result &= Add(state, current, draft, added, area);
        }
        return result;
    }
    internal static int Resolve(SourceWorldContext current, ushort id) => K((id <= 6000
        ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "person_lookup");
    private static T K<T>(T? value, string input) where T : struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("area-administration." + input);
}
