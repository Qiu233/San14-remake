using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached29CDD0 consumer. All mutations and ordered intentions
    /// remain in the caller's candidate; this does not play or acknowledge an event.</summary>
    internal sealed class PersonAppearanceFeedback(ArmyCampaignRuleAdapter package,
        SourceWorldContext current, Func<GameState> state, Action<GameState> replace,
        Func<int, int> drawApp32, List<CampaignEffectRequest> effects)
    {
        private NarrationResourceMessages? messages;

        internal bool Run(int physical)
        {
            if (!Known(current.ReadPersonNativeValidity(physical), "person_validity")) return false;
            var owner = Owner(physical);
            int related = owner.AppearanceNotificationPersonId ?? throw Missing("notification_person_knowledge");
            var q = Person(related); //The captured signed lookup precedes the clear.
            var latest = state();
            var officers = new Dictionary<int, OfficerState>(latest.Officers)
            { [owner.Id] = owner with { AppearanceNotificationPersonId = 0 } };
            replace(latest with { Officers = officers });

            var force = current.ReadPersonForce(physical) ?? throw Missing("force_pointer");
            if (!Known(current.ReadForceNativeValidity(force), "force_validity")) return false;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state(), current)
                ?? throw Missing("current_player_force_pointer");
            if (force != player) return false;

            Add(new(PersonAppearanceOperation.SoundCue, 8, null, null, null, null));
            //Each scope binds actual IDs in source order. Q has no extra native
            //validity gate here; its reached field consumers own empty/unknown.
            ushort qId = Identity(q);
            ushort pId = Identity(physical);
            string announcement = Format(0x5D27, qId, pId);
            Add(new(PersonAppearanceOperation.Announcement, null, null, announcement, null, null));

            //The supported saved Sex owner is the exact ordinary byteA9 domain.
            //Unknown or foreign enum values are not coerced to the nonzero branch.
            int kind = Owner(physical).Sex switch
            {
                OfficerSex.Male => 7,
                OfficerSex.Female => 8,
                _ => throw Missing("person_A9")
            };
            Add(new(PersonAppearanceOperation.EventEnter, null, kind, null, null, null));
            Add(new(PersonAppearanceOperation.SoundCue, 5, null, null, null, null));

            int forcePhysical = Known(force.PhysicalSlot, "saved_force_receiver");
            ushort rawRuler = Known(current.ReadForceRecord(forcePhysical)?.RawRuler10, "saved_force_current_ruler10");
            var ruler = Person(rawRuler);
            pId = Identity(physical);
            ushort rulerId = Identity(ruler);
            string speech = Format(0x5D28, pId, rulerId);
            ushort speaking = Identity(physical); //Fresh actual1F0EC0 PID after formatting.
            Add(new(PersonAppearanceOperation.SpeakerText, null, null, null, new(speaking, speech), null));
            Add(new(PersonAppearanceOperation.EventExit, null, null, null, null, null));

            pId = Identity(physical);
            int forceArgument = Known(current.ReadForcePhysicalSlot(force), "saved_force_argument_ordinal");
            string history = Format(0x5D4F, pId, 0, forceArgument);
            //No name or resource query may move these fresh operands ahead of
            //5D4F. Position uses the same current task/list pool as the manager.
            byte district = Known(current.ReadPersonDistrictSlot(physical), "history_district_E0");
            latest = state();
            var season = latest.NativeMapSeason ?? throw Missing("history_current_calendar");
            var hex = PersonReturnCommands.ResolvePersonPosition(latest, current, physical, season.ThirdDate);
            int ordinal = Known(hex.PhysicalSlot, "history_null_hex_ordinal");
            if ((uint)ordinal >= 48400) throw Missing("history_hex_ordinal_profile");
            ushort personId = Identity(physical);
            latest = state();
            season = latest.NativeMapSeason ?? throw Missing("history_current_calendar");
            int day = unchecked((latest.NativeArmySimulation?.World.SamplerEpoch
                ?? throw Missing("history_day450_epoch")).Day450 + (int)season.AdvancedDays);
            replace(package.WriteHistory(latest, current, new(0, district, ordinal, day, history,
                CampaignHistoryWriter.Supplemental(latest), personId, MarkBit4: 1), effects));
            return true;
        }

        private string Format(int root, ushort first, ushort second, int? force = null)
        {
            try
            {
                if (package.Seed.Language is not ("TC" or "SC")) throw Missing("text_locale");
                var latest = state();
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                    package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                var people = new DeploymentNarrationCurrentPeople(latest, current, abilities, first, second,
                    appearanceRepository: package._repository, appearanceScenario: package._scenario,
                    appearanceLanguage: package.Seed.Language, forceArgument: force);
                messages ??= new(package._repository.LoadMessages(package.Seed.Language, MessageKind.Game).Messages);
                return messages.ResolvePersonAppearance(people, drawApp32, root);
            }
            catch (IOException error)
            { throw new InvalidDataException("person-appearance.feedback.text_resource", error); }
        }

        private OfficerState Owner(int physical)
        {
            int id = Identity(physical);
            if (current.ResolveOfficerSource(id) != physical || !state().Officers.TryGetValue(id, out var owner))
                throw Missing("current_person_owner");
            return owner;
        }
        private TransferPersonReference Person(int raw)
        {
            if (raw < 0) throw Missing("negative_notification_person_lookup");
            return (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("person_lookup_pointer");
        }
        private ushort Identity(TransferPersonReference person) => Identity(Known(person.PhysicalSlot, "null_person_ID10"));
        private ushort Identity(int physical) => Known(current.ReadPersonIdentity(physical), "person_ID10");
        private void Add(PersonAppearanceRequest request) => effects.Add(new(null, null) { PersonAppearance = request });
        private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("person-appearance.feedback." + field);
    }
}
