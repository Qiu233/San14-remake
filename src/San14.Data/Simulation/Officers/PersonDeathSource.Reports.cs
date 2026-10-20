using San14.Core;
using San14.Data.Formats;
using System.Text;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal static class PersonDeathReportInputs
    {
        internal const string RankThresholdKey = "simulation.person-death.office-report-rank-threshold";

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
        private void ApplyInitialReports(int person, byte capturedStatus, int capturedOffice)
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

            //Annual argument8 is1. Its fresh player-force read precedes the
            //first unowned history reporter-selection/person predicate unit.
            var player = current.ReadCurrentPlayerForce() ?? throw Missing("death_history_current_player");
            if (Known(current.ReadForceNativeValidity(player), "death_history_player_validity"))
                throw Missing("death_history_reporter_22E930_2B44E0");
            throw Missing("death_history_person_branch_232460");
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
