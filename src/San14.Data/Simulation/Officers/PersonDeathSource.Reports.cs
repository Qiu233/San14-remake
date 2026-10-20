using San14.Core;
using San14.Data.Formats;
using System.Text;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal static class PersonDeathReportInputs
    {
        internal const string RankThresholdKey = "simulation.person-death.office-report-rank-threshold";
        internal const string PopularityScaleKey = "simulation.person-death.general-popularity-loss-scale";

        internal static int? ReadCurrentPopularityScale(GameState state) =>
            state.NativeArmySimulation?.Parameters is { } p && p.Language is "TC" or "SC" &&
            p.RuleVersion == SimulationRuleIdentity.ForLocale(p.Language) && !p.Float32Values.ContainsKey(PopularityScaleKey) &&
            p.Int32Values.TryGetValue(PopularityScaleKey, out int value) ? value : null;

        //Project reference CE catalog; each locale has independent file-initialization evidence.
        internal static GameState InstallFreshPopularityScale(GameState state, LoadedScenario source, string locale)
        {
            if (locale is not ("TC" or "SC") || source.Snapshot.Locale != locale ||
                state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != locale ||
                simulation.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(locale))
                throw new InvalidDataException("person-death.fresh_popularity_profile");
            if (source.Metadata.FormatMarker != 0x66) return state;
            if (simulation.Parameters.Int32Values.ContainsKey(PopularityScaleKey) ||
                simulation.Parameters.Float32Values.ContainsKey(PopularityScaleKey))
                throw new InvalidDataException("person-death.fresh_popularity_already_owned");
            const int initial = 2;
            return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [PopularityScaleKey] = initial } } } };
        }


        //The explicitly owned CE fresh catalog. Restore/Bind never calls this.
        //Both locales have independent file-initialization evidence for2.
        internal static GameState InstallFresh(GameState state, string locale)
        {
            if (locale is not ("TC" or "SC"))
                throw new InvalidDataException("person-death.fresh_report_locale");
            var simulation = state.NativeArmySimulation ??
                throw new InvalidDataException("person-death.fresh_report_parameters");
            if (simulation.Parameters.Int32Values.ContainsKey(RankThresholdKey))
                throw new InvalidDataException("person-death.fresh_report_parameters_already_owned");
            return state with { NativeArmySimulation = simulation with
            {
                Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [RankThresholdKey] = 2 } }
            } };
        }
    }

    internal sealed partial class PersonDeathSource
    {
        private void ApplyInitialReports(int person, byte capturedStatus, int capturedOffice,
            ForceReference capturedForce, HexReference capturedPosition)
        {
            var effects = new List<CampaignEffectRequest>();
            bool entered = false;
            if (capturedStatus != 6 && (capturedStatus == 1 ||
                Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest, capturedOffice), "initial_report_office_rank37") <
                    Parameter("office-report-rank-threshold") || IsPlayer(Force(person))))
            {
                //A9/SC E1 is reached only after the controller subgate.
                int kind = Owner(person).Sex switch
                {
                    OfficerSex.Male => 4,
                    OfficerSex.Female => 5,
                    _ => throw Missing("initial_report_person_sex")
                };
                effects.Add(Request(new(PersonDeathReportOperation.EventEnter, kind, null, null)));
                entered = true;
            }

            //Reset and actual argument binding precede root selection. The
            //ordinary annual profile selects2A14; ruler/successor arguments
            //for29A9/29B1 are not supplied by this finite consumer.
            ushort argument0 = Identity(person);
            if (capturedStatus is 1 or 2) throw Missing("initial_special_report_arguments_29A9_29B1");
            string text = FormatInitialDeathText(argument0);
            ushort speaker = Identity(person); //Fresh1F0EC0 operand after formatting.
            effects.Add(Request(new(PersonDeathReportOperation.SpeakerText, null, speaker, text)));
            if (entered) effects.Add(Request(new(PersonDeathReportOperation.EventExit, null, null, null)));
            AppendInitialDeathEffects(effects);
            Observe("death.initial-report");

            ApplySecondReports(person, capturedStatus, capturedOffice);

            int chronicleCode = ApplyDeathHistory(person);
            ApplyDeathKinJournal(person, capturedForce, capturedPosition, chronicleCode);
        }

        private int ApplyDeathHistory(int person)
        {
            //Annual argument8 is1. Retain this exact fresh player receiver for
            //the later attachment Arg1, even when it is native-invalid.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest, current)
                ?? throw Missing("death_history_current_player");
            ushort reporter = 5012;
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            var selection = new PersonDeathHistorySelection(latest, current, pool, abilities,
                package._governorInputs, (force, effect) => package._baseCapacityInputs.ReadPanelModifier(latest, force, effect));
            if (Known(current.ReadForceNativeValidity(player), "death_history_player_validity"))
            {
                var selected = selection.Select(selection.PrimaryDistrict(player));
                if (Valid(selected) && Identity(person) != Identity(Physical(selected, "death_history_selected_pointer")))
                    reporter = Identity(Physical(selected, "death_history_reporter_pointer"));
            }

            //232460 is status1..4; it does not perform another Person-validity test.
            byte firstStatus = Status(person);
            if (firstStatus is not (>= 1 and <= 4) && Status(person) != 6)
            {
                string text = FormatDeathHistoryText(0x2A32, Identity(person), 0, 0, 0);
                int day = DeathHistoryDay();
                string? supplemental = CampaignHistoryWriter.Supplemental(latest);
                CommitDeathHistory(new(0, 0, 0, day, text, supplemental, 5012));
                return 0;
            }

            var related = Force(person);
            if (Status(person) == 6)
            {
                int prior = current.ReadOfficerAftermath(person)?.SourceKnowledge?.PreviousForceCode
                    ?? throw Missing("death_history_current_165");
                if ((uint)prior > byte.MaxValue) throw Missing("death_history_current_165_byte");
                related = current.ReadForceTableReference(prior <= 51 ? prior : 0)
                    ?? throw Missing("death_history_165_Force_lookup");
            }
            int root = Known(current.ReadForceNativeValidity(related), "death_history_main_Force_validity") ? 0x2A2E : 0x2A32;
            string main = FormatDeathHistoryText(root, Identity(person), 0, 0, 0);

            //A separate reset/context for528F. The annual second operand is
            //the real lookup0 binding, not a fabricated known-null Person.
            ushort argument1 = Known(current.ReadMoraleWaveRuler22EAC0(player), "death_history_attachment_ruler22EAC0");
            var secondary = current.ReadPersonLookupReference(0) ?? throw Missing("death_history_param2_lookup0");
            ushort argument2 = Valid(secondary) ? Identity(Physical(secondary, "death_history_param2_pointer")) : (ushort)0;
            ushort argument3 = Identity(person);
            string attachment = FormatDeathHistoryText(0x528F, reporter, argument1, argument2, argument3);

            int district = 0;
            if (Known(current.ReadForceNativeValidity(related), "death_history_metadata_Force_validity"))
                district = Known(selection.PrimaryDistrict(related).PhysicalSlot, "death_history_District_ordinal");
            if (district < 0) throw Missing("death_history_negative_District_ordinal");
            int freshDay = DeathHistoryDay();
            //16F050's manager-only lazy initialization owns no record/cursor
            //reset. Position and its actual same-current task effects follow it.
            int hex = Known(Position(person).PhysicalSlot, "death_history_Hex_ordinal");
            if (hex < 0) throw Missing("death_history_negative_Hex_ordinal");
            CommitDeathHistory(new(1, district, hex, freshDay, main, attachment, reporter, MarkBit3: 1, MarkBit4: 1));
            return root == 0x2A2E ? 12 : 0; //Retain the actual annual branch result, not a later status.
        }

        private int DeathHistoryDay()
        {
            var season = latest.NativeMapSeason ?? throw Missing("death_history_current_calendar");
            var epoch = latest.NativeArmySimulation?.World.SamplerEpoch ?? throw Missing("death_history_Day450_epoch");
            return unchecked(epoch.Day450 + (int)season.AdvancedDays);
        }

        private string FormatDeathHistoryText(int root, ushort argument0, ushort argument1,
            ushort argument2, ushort argument3)
        {
            if (package.Seed.Language is not ("TC" or "SC")) throw Missing("death_history_text_locale");
            try
            {
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                    package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                var people = new DeploymentNarrationCurrentPeople(latest, current, abilities, argument0, argument1,
                    panelReader: (force, effect) => package._baseCapacityInputs.ReadPanelModifier(latest, force, effect),
                    appearanceRepository: package._repository, appearanceScenario: package._scenario,
                    appearanceLanguage: package.Seed.Language, personArgument2: argument2, personArgument3: argument3);
                var messages = new NarrationResourceMessages(package._repository.LoadMessages(
                    package.Seed.Language, MessageKind.Game).Messages);
                return messages.ResolvePersonDeathHistory(people, DrawDeathReportApp32, root);
            }
            catch (IOException error)
            { throw new InvalidDataException("person-death.history_text_resource", error); }
        }

        private void CommitDeathHistory(ReportCommand command)
        {
            var effects = new List<CampaignEffectRequest>();
            latest = package.WriteHistory(latest, current, command, effects);
            AppendInitialDeathEffects(effects);
            Observe("death.history-consumed");
        }

        private void ApplySecondReports(int person, byte capturedStatus, int capturedOffice)
        {
            //This is independent of the first controller's already-closed flag.
            bool entered6 = false;
            if (capturedStatus == 1 ||
                Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest, capturedOffice), "second_report_office_rank37") <
                    Parameter("office-report-rank-threshold"))
            {
                AppendSecondDeathEffect(new(PersonDeathReportOperation.EventEnter, 6, null, null), "death.second-report-enter");
                entered6 = true;
            }
            //The text condition repeats the rank/parameter read, independently
            //of controller entry, and reaches the current-player gate last.
            if (capturedStatus == 1 ||
                Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest, capturedOffice), "second_text_office_rank37") <
                    Parameter("office-report-rank-threshold") || IsPlayer(Force(person)))
            {
                //Actual405E40(...,22,0) precedes caption/argument formatting.
                //Keep this prefix on the same candidate even if a later read fails.
                AppendSecondDeathEffect(new(PersonDeathReportOperation.ControllerQueueEvent, 22, null, null), "death.second-report-queue");
                int forceArgument0 = Known(current.ReadPersonOldForceSlot(person), "second_report_ForceArg0");
                string caption = ReadSecondDeathCaption(person);
                ushort argument0 = Identity(person);
                var date = latest.NativeMapSeason?.ThirdDate ?? throw Missing("second_report_ThirdDate");
                int year = date.Year;
                int month = date.Month;
                string text = FormatSecondDeathText(argument0, forceArgument0, caption, year, month);
                //The same conditional(text,0) work as the existing narration
                //owner. No speaker, App admission/display, callback or ACK claim.
                AppendSecondDeathEffect(new(PersonDeathReportOperation.NarrationText, null, null, text), "death.second-report-narration");
            }
            if (entered6)
                AppendSecondDeathEffect(new(PersonDeathReportOperation.EventExit, null, null, null), "death.second-report-exit");
        }

        private string ReadSecondDeathCaption(int person)
        {
            //Actual233780(Person,1) is status equality. Special-ruler reports
            //are rejected by the existing initial-report profile before here.
            if (Status(person) == 1) throw Missing("second_report_ruler_status_label");
            int post = Known(current.ReadMoraleWavePersonPostE7(person), "second_report_office_validity_E7");
            string caption = string.Empty;
            if (Known(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(latest, post), "second_report_office_validity") &&
                Known(current.ReadMoraleWavePersonPostE7(person), "second_report_office73_E7") != 73)
            {
                post = Known(current.ReadMoraleWavePersonPostE7(person), "second_report_office_name_E7");
                if (post is >= 1 and <= 255)
                    caption = latest.OfficerOffices.TryGetValue(post, out var office)
                        ? office.Name : throw Missing("second_report_office_name");
            }
            //The existing String0 complete finite-copy domain, not truncation
            //or the already-coloured UI OfficeText representation.
            if (caption.Length >= 126 || Encoding.UTF8.GetByteCount(caption) >= 126 ||
                caption.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Missing("second_report_caption_copy_profile");
            return caption;
        }

        private string FormatSecondDeathText(ushort argument0, int forceArgument0,
            string caption, int year, int month)
        {
            if (package.Seed.Language is not ("TC" or "SC")) throw Missing("second_report_text_locale");
            try
            {
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                    package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                var people = new DeploymentNarrationCurrentPeople(latest, current, abilities, argument0, 0,
                    appearanceRepository: package._repository, appearanceScenario: package._scenario,
                    appearanceLanguage: package.Seed.Language, forceArgument: forceArgument0);
                var messages = new NarrationResourceMessages(package._repository.LoadMessages(
                    package.Seed.Language, MessageKind.Game).Messages);
                return messages.ResolvePersonDeathSecond(people,
                    slot => slot switch { 0 => year, 1 => month, _ => throw Missing("second_report_integer_argument") },
                    () => caption, DrawDeathReportApp32);
            }
            catch (IOException error)
            { throw new InvalidDataException("person-death.second_report_text_resource", error); }
        }

        private void AppendSecondDeathEffect(PersonDeathReportRequest report, string observation)
        {
            AppendInitialDeathEffects([Request(report)]);
            Observe(observation);
        }

        private string FormatInitialDeathText(ushort argument0)
        {
            if (package.Seed.Language is not ("TC" or "SC")) throw Missing("initial_report_text_locale");
            try
            {
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                    package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                var people = new DeploymentNarrationCurrentPeople(latest, current, abilities, argument0, 0,
                    appearanceRepository: package._repository, appearanceScenario: package._scenario,
                    appearanceLanguage: package.Seed.Language);
                var messages = new NarrationResourceMessages(package._repository.LoadMessages(
                    package.Seed.Language, MessageKind.Game).Messages);
                return messages.ResolvePersonDeathInitial(people, DrawDeathReportApp32);
            }
            catch (IOException error)
            { throw new InvalidDataException("person-death.initial_report_text_resource", error); }
        }

        private int DrawDeathReportApp32(int bound)
        {
            if (bound < 2) return 0;
            var state = latest.CampaignAppRandom32 ?? throw Missing("initial_report_app32_draw_state");
            var random = new SharedSourceRandom32(state.State);
            int selected = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            return selected;
        }

        private void AppendInitialDeathEffects(IReadOnlyList<CampaignEffectRequest> requests)
        {
            if (requests.Count == 0) return;
            var pending = latest.CampaignEffectRequests ?? throw Missing("initial_report_effect_requests");
            long revision = latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("initial_report_source_revision");
            latest = latest with
            { CampaignEffectRequests = pending.Append(CampaignEffectSource.PersonDeath, revision, requests) };
        }

        private static CampaignEffectRequest Request(PersonDeathReportRequest report) =>
            new(null, null) { PersonDeathReport = report };
    }
}
