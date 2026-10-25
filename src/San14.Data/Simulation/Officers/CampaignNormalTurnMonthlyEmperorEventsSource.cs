using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //428370: the three historical Han-emperor deaths and the two successor
    //placements. The surrounding NormalTurn owns the3098A0 monthly guard.
    internal GameState ApplyNormalTurnMonthlyEmperorEventsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnMonthlyEmperorEventsSource(this, state, current, pool).Run();

    private sealed class NormalTurnMonthlyEmperorEventsSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            int rawEmperor = Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654");
            var emperor = Lookup(rawEmperor);
            int mode = (state.CampaignEventWorld ?? throw Missing("world_mode")).ScenarioMode;
            if (mode == 2 || emperor.PhysicalSlot is null || rawEmperor == 0) return state;
            int person = emperor.PhysicalSlot.Value;
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
            int monthOrdinal = unchecked(date.Month + date.Year * 12);
            var city = EmperorCity(person);
            int roll = TacticEffectSourceSamplerMath.Sample(100, 1,
                Need(current.ReadBaseResource(city, BaseResourceKind.Troops), "EmperorCity3C"), date.Month, 0, 0,
                package.ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C"));

            //The source prepares2B44E0 before testing the date/age branches.
            //Keep its real temporary allocations in the shared pool order.
            int reporter = 5012;
            var district = PlayerDistrict();
            if (Need(current.ReadDistrictNativeValidity(district), "reporter_District_validity"))
            {
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language));
                var chosen = new PersonDeathHistorySelection(state, current, pool, abilities, package._governorInputs,
                    (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)).Select(district);
                if (chosen.PhysicalSlot is int selected && Need(current.ReadPersonNativeValidity(selected), "reporter_validity"))
                    reporter = Need(current.ReadPersonIdentity(selected), "reporter_ID");
            }

            int successor;
            int reportRoot, eventCode;
            uint entryKind;
            if (monthOrdinal >= 0x8E0 + 6 && rawEmperor == 5001 && unchecked(50 + (Age(person) - 34) * 5) > roll)
            {
                Sound(23); Scene(45); Narration(0x5E9E); Scene(null);
                successor = 5002; reportRoot = 0x527D; eventCode = 0x104; entryKind = 0x1D;
            }
            else if (monthOrdinal >= 0x8E5 + 6 && rawEmperor == 5002 && unchecked(50 + (Age(person) - 18) * 10) > roll)
            {
                Sound(23); Scene(45); Narration(0x5EA0); Scene(null);
                successor = 5003; reportRoot = 0x5283; eventCode = 0x105; entryKind = 0x1E;
            }
            else if (monthOrdinal >= 0xAFC && rawEmperor == 5003 && unchecked(50 + (Age(person) - 52) * 3) > roll)
            {
                Sound(22); Scene(6); Narration(0x5EA2); Scene(null);
                Sound(7); Scene(55); Narration(0x5EA3); Scene(null);
                successor = 0; reportRoot = 0x5289; eventCode = 0x106; entryKind = 0x1F;
            }
            else return state;

            district = PlayerDistrict();
            if (Need(current.ReadDistrictNativeValidity(district), "history_District_validity"))
            {
                int districtId = Need(current.ReadDistrictPhysicalSlot(district), "history_District60");
                int rawForce = Need(current.ReadDistrictForceId(Need(district.PhysicalSlot, "history_District_pointer")), "history_District10");
                var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw Missing("history_Force_pointer");
                var formatted = package.FormatNormalTurnReport(state, current, reportRoot,
                    new(Person0: reporter, Person1: package.TalentRulerCurrent(current, force)));
                state = formatted.State;
                int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("history_World450")).Day450;
                var notifications = new List<CampaignEffectRequest>();
                //ASM428666/428945/428C34 zero R9D; all five mark operands
                //are zero. This report uses map cell0 and an empty attachment.
                state = package.WriteHistory(state, current, new(1, districtId, 0, day, formatted.Text,
                    CampaignHistoryWriter.Supplemental(state), reporter), notifications);
                Append(notifications);

                var emperorForce = current.ReadPersonForce(person) ?? throw Missing("chronicle_Emperor_Force");
                string Name(OfficerState owner, bool full) => (owner.CurrentSurname ?? throw Missing("chronicle_surname")) +
                    (full ? owner.CurrentGivenName ?? throw Missing("chronicle_given_name") : string.Empty);
                state = new CampaignChronicleWriter(package.HistoryWriter).Write(state, current,
                    new(eventCode, 1, day, package.HistoryWriter.CustomFirstName(state, current, emperorForce, Name),
                        Need(current.ReadForceReportColor(emperorForce), "chronicle_Force49"), entryKind,
                        ImmutableArray.Create(string.Empty, string.Empty, string.Empty),
                        ImmutableArray.Create(48400, 48400, 48400, 48400)),
                    package.ReadApplicationGameplay()?.UserSelectionServicePresent);
            }

            //241D30 only stores status9. It does not run2A1720's ordinary
            //death/item/force-registration transaction for these special people.
            WriteEmperor(successor);
            current.WritePersonStatus(person, 9);
            var old = Owner(person);
            var status = OfficerServiceStatusSourceAdapter.Decode(9);
            state = state with { Officers = new(state.Officers) { [old.Id] = old with
                { ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, old.Role), Presence = OfficerPresence.Dead } } };
            if (successor == 0) return state;

            //The original retained City receiver supplies virtualC0 twice.
            //Neither successor's status nor its district is changed here.
            int next = Need(Lookup(successor).PhysicalSlot, "successor_pointer");
            int first = Need(city.PhysicalSlot, "successor_first_CityC0");
            first = first < 52 ? first : 0;
            ushort home = checked((ushort)Math.Clamp(first, 0, 61));
            current.WritePersonFirstLocation(next, home);
            state = OwnedArmyLifecycleDetachedInputs.PublishCurrentPersonFirstLocation(state, current, next);
            int second = Need(city.PhysicalSlot, "successor_second_CityC0");
            second = second < 52 ? second : 0;
            current.WritePersonSecondLocation(next, checked((ushort)Math.Clamp(second, 0, 1061)));
            var nextOwner = Owner(next);
            state = state with { Officers = new(state.Officers) { [nextOwner.Id] = nextOwner with
                { LocationId = home == 0 ? null : home } } };
            return OfficerLocationSourceAdapter.PublishPosition(state, nextOwner.Id, current);
        }

        private BaseReference EmperorCity(int person)
        {
            int at = 0;
            if (Need(current.ReadPersonNativeValidity(person), "Emperor_validity"))
            {
                var position = PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(person), "Emperor_E4"));
                if (position.Kind == 5) at = position.Index < 52 ? position.Index : 0;
            }
            return current.ReadCityTableReference(at) ?? throw Missing("Emperor_City_pointer");
        }
        private DistrictReference PlayerDistrict()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
            return ForcePrimaryDistrictSource.Resolve(current, player);
        }
        private int Age(int person)
        {
            int mode = (state.CampaignEventWorld ?? throw Missing("age_world")).ScenarioMode;
            bool special = mode == 2 || mode == 3 && DuelNumericParameters.Read(state, "mode3-age-control") != 0;
            if (special)
            {
                int id = Need(current.ReadPersonIdentity(person), "age_person_ID");
                for (int i = 0; i < 2; i++) if (id == DuelNumericParameters.Read(state, "special-age-person:" + i)) return 80;
                for (int i = 0; i < 9; i++)
                    if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, i), "age_trait") == 144) return 80;
                for (int i = 2; i < 5; i++) if (id == DuelNumericParameters.Read(state, "special-age-person:" + i)) return 80;
                return 20;
            }
            int year = state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("age_year");
            return Math.Max(0, unchecked(year - (Owner(person).BirthYear ?? throw Missing("age_birth_AC")) + 1));
        }
        private TransferPersonReference Lookup(int id) => (id <= 6000 ? current.ReadPersonLookupReference(id) :
            current.ReadTransferPersonTableReference(0)) ?? throw Missing("Person_lookup");
        private OfficerState Owner(int person)
        {
            int id = Need(current.ReadPersonIdentity(person), "Person_ID");
            if (current.ResolveOfficerSource(id) != person) throw Missing("Person_binding");
            return state.Officers.GetValueOrDefault(id) ?? throw Missing("Person_owner");
        }
        private void WriteEmperor(int id)
        {
            var simulation = state.NativeArmySimulation ?? throw Missing("Emperor_saved_owner");
            _ = Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "Emperor_saved_field");
            state = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [CityCaptureTerritoryInputs.Emperor1654Key] = id } } } };
            CityCaptureTerritoryInputs.BindCurrentEmperor(state, current);
        }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Scene(int? kind) => state = package.AppendNormalTurnEmperorNarrationCurrent(state, kind);
        private void Narration(int root)
        {
            var formatted = package.FormatNormalTurnReport(state, current, root, new());
            state = formatted.State;
            Append([new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.NarrationText, null, null, formatted.Text) }]);
        }
        private void Append(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner")).Append(
                CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("source_revision"), effects) };
        }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.monthly-emperor-events." + field);
    }
}
