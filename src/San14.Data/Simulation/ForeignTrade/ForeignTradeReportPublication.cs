using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public enum ForeignTradeReportStepKind { Speech, Reward, ReturnGold }
public sealed record ForeignTradeReportStep(ForeignTradeReportStepKind Kind, int MessageId, string Text,
    int? SpeakerPersonId, string SpeakerName, int SpeakerChannel, DeploymentNarrationPortraitRoute? Portrait,
    int ItemId, int GemId, int WeaponId, string Name);
public sealed record ForeignTradeReturnReportPublication(int TaskId, bool Successful,
    IReadOnlyList<ForeignTradeReportStep> Steps);

public sealed partial class ArmyCampaignRuleAdapter
{
    //Prepare once on actual entry to the report. Its real random phrase
    //selections use the UI's existing App32 stream; repeated frame queries do
    //not prepare again. This method never performs the reward settlement.
    public AreaAdministrationQuery<ForeignTradeReturnReportPublication> QueryForeignTradeReturnReport(
        GameState state, int taskId, Func<int, int> drawApp32)
    {
        ArgumentNullException.ThrowIfNull(drawApp32);
        try
        {
            var task = state.PersonnelCommands?.Caravans?.GetValueOrDefault(taskId)
                ?? throw ForeignTradeReturnMissing("report_task");
            var report = task.PendingReport ?? throw ForeignTradeReturnMissing("report_pending_result");
            if (task.MemberIds.Length == 0) throw ForeignTradeReturnMissing("report_member_head");
            var current = CaptureReportContentCurrent(state);
            int headId = task.MemberIds[0];
            int head = ForeignTradeReturnNeed(current.ResolveOfficerSource(headId), "report_head_pointer");
            var force = current.ReadPersonForce(head) ?? throw ForeignTradeReturnMissing("report_Force");
            int forceId = ForeignTradeReturnNeed(force.PhysicalSlot, "report_Force_pointer");
            int rulerId = ForeignTradeReturnNeed(current.ReadForceRecord(forceId)?.RawRuler10, "report_ruler10");
            var country = ReadForeignTradeCountry(task.CountryId);
            int foreignPerson = task.CountryId switch { 1 => 0x13CB, 2 => 0x13CC, 3 => 0x13CD, 4 => 0x13CE, _ => 0x1396 };
            int count = task.MemberIds.Length;
            var steps = new List<ForeignTradeReportStep>();
            string stageName = !report.Successful ? string.Empty : task.RewardStage switch
            {
                1 => ItemName(country.FirstItemId), 2 => WeaponName(country.FirstWeaponId),
                3 => WeaponName(country.SecondWeaponId), 4 => GemName(country.GemId), _ => string.Empty
            };
            bool any = report.GoldAmount != 0 || report.ItemId != 0 || report.GemId != 0 || report.WeaponId != 0;
            if (!report.Successful)
            {
                Speech(0x32BE, headId, rulerId, 3);
                Speech(0x32C3, rulerId, headId, 4);
                Speech(0x32C8, headId, rulerId, 3);
            }
            else
            {
                Speech(0x32CB, headId, rulerId, 3);
                Speech(0x32CE, rulerId, headId, 4);
                Speech(any ? 0x32D6 : 0x32D2, headId, rulerId, 3);
                if (any)
                {
                    Speech(0x32DB, rulerId, headId, 4);
                    Speech(0x32DF, foreignPerson, rulerId, 3, stageName);
                    int root;
                    string reward;
                    if (report.GoldAmount > 0) { root = 0x32E0; reward = string.Empty; }
                    else if (report.GemId is >= 1 and <= 30) { root = 0x32E1; reward = GemName(report.GemId); }
                    else { root = 0x32E2; reward = report.ItemId is >= 1 and <= 201
                            ? ItemName(report.ItemId) : report.WeaponId is >= 1 and <= 8 ? WeaponName(report.WeaponId) : string.Empty; }
                    var operands = new HistoryMessageOperands(Person0: rulerId, String0: country.Name, String1: reward,
                        Integers: [root == 0x32E0 ? report.GoldAmount : report.GemCount]);
                    steps.Add(new(ForeignTradeReportStepKind.Reward, root, Text(root, operands), null, string.Empty,
                        0, null, report.ItemId, report.GemId, report.WeaponId, reward));
                    Speech(0x32E3, rulerId, foreignPerson, 4);
                    Speech(0x32E7, foreignPerson, rulerId, 3);
                }
                int ruler = ForeignTradeReturnNeed(current.ResolveOfficerSource(rulerId), "report_return_ruler");
                var home = BaseLookup.Find(ForeignTradeReturnNeed(current.ReadPersonFirstLocation(ruler), "report_return_BaseE2"), current);
                int stock = ReadDiplomacyBaseResourceCurrent(current, home, BaseResourceKind.Gold);
                var summary = new HistoryMessageOperands(Person0: rulerId,
                    Base0: ForeignTradeReturnNeed(current.ReadBaseObjectLink(home), "report_return_Base4E"),
                    Integers: [report.BonusGold, stock]);
                steps.Add(new(ForeignTradeReportStepKind.ReturnGold, 0x32F8, Text(0x32F8, summary), null,
                    string.Empty, 0, null, 0, 0, 0, string.Empty));
            }
            return AreaAdministrationQuery<ForeignTradeReturnReportPublication>.Known(new(taskId, report.Successful, steps.AsReadOnly()));

            void Speech(int root, int speaker, int other, int channel, string? secondText = null)
            {
                var operands = new HistoryMessageOperands(Person0: speaker, Person1: other, String0: country.Name,
                    String1: secondText, Integers: [count]);
                int person = ForeignTradeReturnNeed(current.ResolveOfficerSource(speaker), "report_speaker_pointer");
                DeploymentNarrationPortraitRoute? portrait = null;
                if (ForeignTradeReturnNeed(current.ReadPersonNativeValidity(person), "report_speaker_validity"))
                    portrait = ReadNarrationPortrait(state, current, _definitions, speaker,
                        ForeignTradeReturnNeed(current.ReadPersonPortraitAppearance(person), "report_speaker_A6"));
                string name = state.Officers.GetValueOrDefault(speaker)?.Name ?? throw ForeignTradeReturnMissing("report_speaker_name");
                steps.Add(new(ForeignTradeReportStepKind.Speech, root, Text(root, operands), speaker, name,
                    channel, portrait, 0, 0, 0, string.Empty));
            }
            string Text(int root, HistoryMessageOperands operands) => FormatForeignTradeMessage(state, current, root, operands, drawApp32);
            string ItemName(int id)
            {
                var item = state.Items?.GetValueOrDefault(id < 202 ? id : 0)
                    ?? throw ForeignTradeReturnMissing("report_Item_owner");
                return state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.Name
                    ?? throw ForeignTradeReturnMissing("report_Item_name");
            }
            string GemName(int id) => _skills.Gems.Gems[id < 31 ? id : 0].Name;
            string WeaponName(int id) => _repository.LoadForeignTechnologies(_scenario.FixedDataSource.SourcePath)
                .Definitions[id < 9 ? id : 0].Name;
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<ForeignTradeReturnReportPublication>.Unknown(error.Message); }
    }

    internal HistoryTextResult FormatForeignTradeMessage(GameState state, SourceWorldContext current, int root,
        HistoryMessageOperands operands)
    {
        var latest = state;
        int Draw(int bound)
        {
            if (bound < 2) return 0;
            var saved = latest.CampaignAppRandom32 ?? throw ForeignTradeReturnMissing("message_App32");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            return value;
        }
        var voices = _personnelNarrationVoice is { } settings ? new NarrationVoiceRequests(settings, Draw) : null;
        string text = FormatForeignTradeMessage(state, current, root, operands, Draw, voices);
        return new(latest, text) { Voices = voices?.Pending ?? [] };
    }

    private string FormatForeignTradeMessage(GameState state, SourceWorldContext current, int root,
        HistoryMessageOperands operands, Func<int, int> drawApp32, NarrationVoiceRequests? voices = null)
    {
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities, operands.Person0, operands.Person1,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect),
            cityArgument: operands.Base0, appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language,
            personArgument2: operands.Person2, personArgument3: operands.Person3, personArgument4: operands.Person4,
            forceArgument: operands.Force0, forceArgument1: operands.Force1, voiceRequests: voices);
        var context = new ReportCurrent(this, () => state, current, people, operands);
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
        return vm.ResolveForeignTradeReport(context, drawApp32, root);
    }
}
