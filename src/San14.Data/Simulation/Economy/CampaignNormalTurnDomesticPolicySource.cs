using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //27AAB0 changes a currently assigned Area's completed development focus.
    //It neither appoints a new person nor samples App or the World random.
    internal GameState ApplyNormalTurnDomesticPolicyCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference city,
        List<CampaignEffectRequest> effects)
    {
        if (K(current.ReadBaseObjectLink(city), "City4E") >= 52 ||
            K(current.ReadBaseForceSlot(city), "City_Force") == 0) return state;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("World3A_Force");
        var primary = ForcePrimaryDistrictSource.Resolve(current, player);
        var district = current.ReadBaseDistrictReference(city) ?? throw Missing("City_District");
        if (district != primary) return state;
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");

        //The native local Area list is never populated, but its descriptor
        //allocation/release still belongs in the enclosing pool's free order.
        var temporary = Temporary();
        var changed = new List<int>();
        try
        {
            int cityPhysical = K(city.PhysicalSlot, "City_pointer");
            if (cityPhysical is < 1 or > 51) return state;
            foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("AreaB0"))
            {
                var area = entry ?? throw Missing("Area_pointer");
                if (K(current.ReadAreaCitySlot(area), "Area35") != cityPhysical) continue;
                int physical = K(area.PhysicalSlot, "Area_physical");
                int person = PersonAreaAppointmentSource.Resolve(current, K(current.ReadAssignmentArea36(physical), "Area36"));
                if (K(current.ReadAreaCurrentForceSlot(area), "Area_Force") != K(current.ReadBaseForceSlot(city), "Area_City_Force")) continue;
                if (K(current.ReadPersonIdentity(person), "assigned_Person10") == 0) continue;
                byte before = K(current.ReadAssignmentArea72(physical), "Area72");
                if (before > 3) throw Missing("Area72_index");
                var owner = state.Areas.GetValueOrDefault(physical) ?? throw Missing("Area_owner");
                var development = owner.Development ?? throw Missing("Area_development");
                int[] progress = [1000, Level(development.Commerce), Level(development.Agriculture), Level(development.Barracks)];
                if (progress[before] != 1000) continue;
                byte after = 0;
                bool available = progress[1] != 1000 || progress[2] != 1000 || progress[3] != 1000;
                if (available)
                {
                    //266F90's two explicit modifier operands are both0 at
                    //27ADBA/27ADBF. No UI city's trait/policy is substituted.
                    if (!K(current.ReadPersonNativeValidity(person), "assigned_Person_validity"))
                        throw Missing("266F90_nonvalid_assigned_Person");
                    bool away = Away(person);
                    var counts = new PersonAssignmentPrecallInputs.AreaAppointmentDraft(state);
                    var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
                    int best = 0;
                    for (int focus = 0; focus < 4; focus++)
                    {
                        if (progress[focus] == 1000) continue;
                        var value = preview.AutoDevelopment(physical, person, focus, counts.Count,
                            residentTrait: 0, forcePolicy: 0, candidateCity: false, awayAtSource: away);
                        int score = value.Available && value.Value.HasValue ? value.Value.Value :
                            throw Missing(value.UnresolvedInput ?? "266F90_current_development");
                        if (score > best) { best = score; after = (byte)focus; }
                    }
                }
                if (before != 0)
                {
                    var operands = available
                        ? new HistoryMessageOperands(Area0: physical, String0: FocusName(before), String1: FocusName(after))
                        : new HistoryMessageOperands(Area0: physical);
                    var report = FormatNormalTurnReport(state, current, available ? 0x5F83 : 0x5F84, operands);
                    state = report.State;
                    int speaker = K(current.ReadPersonIdentity(person), "history_Person10");
                    int day = (ReadArmyDestructionSampler(state, current) ?? throw Missing("history_World450")).Day450;
                    int hex = K(current.ReadAreaHexIndex(area), "history_Area3A");
                    //City virtual78 is its raw District30. R9 is the Area's
                    //representative Hex, not the City's physical identity.
                    state = WriteHistory(state, current, new(0, K(current.ReadBaseDistrictSlot(city), "history_City30"),
                        hex, day, report.Text, CampaignHistoryWriter.Supplemental(state), speaker, MarkBit4: 1), effects);
                }
                current.SetAssignmentArea72(physical, after);
                changed.Add(physical);
            }
            return changed.Count == 0 ? state : PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, [], changed, []);
        }
        finally
        {
            Lock(true);
            try { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
            finally { Lock(false); }
        }

        bool Away(int person)
        {
            if ((K(current.ReadArmyReturnPersonFlags196(person), "Person15E") & 1) != 0) return true;
            if (K(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "22A200_Army_validity")) return true;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("22AC00_associated");
            return K(current.ReadLinkedEntityNativeValidity(linked), "associated_validity");
        }
        int Level(int? value) => value is >= 0 and <= ushort.MaxValue ? value.Value : throw Missing("development_word");
        string FocusName(byte focus) => ReadHistoryDataText(0, focus switch { 1 => 0x2B3, 2 => 0x2B4, 3 => 0x2B5, _ => 0x53F });
        HexListHandle Temporary()
        {
            Lock(true); try { return current.CreateTemporaryHexList() ?? throw Missing("Area_temporary_owner"); } finally { Lock(false); }
        }
        void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.domestic-policy." + field);
    }
}
