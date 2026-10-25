using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICampaignStoryPreparationRules
{
    public CampaignStoryPreparationResult PrepareNormalTurnStoryStep(GameState candidate, CampaignStoryRequest story)
    {
        try
        {
            if ((uint)story.NextStep >= (uint)story.Steps.Length)
                throw StoryTextMissing("step");
            if (story.PreparedStep is { } prepared && prepared.Step == story.NextStep)
                return new(prepared, candidate.CampaignAppRandom32, null);
            var step = story.Steps[story.NextStep];
            if (step is not (StoryMessage or StoryNarration))
                return new(new(story.NextStep, null), candidate.CampaignAppRandom32, null);

            var current = CaptureDeploymentPersonSelectionCurrent(candidate);
            current.RestoreOwnedArmyReturnSlots(candidate, candidate.ReadArmyLifecycle() ?? throw StoryTextMissing("army_lifecycle"));
            ForceUnregisterInputs.BindCurrent(candidate, current);
            OwnedArmyReturnServiceInputs.BindCurrent(candidate, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(candidate, current);
            PersonArmyCaptureInputs.BindCurrent(candidate, current);
            _personRelationInputs.BindCurrent(candidate, current);
            _moraleWaveInputs.BindCurrent(candidate, current);
            _baseCapacityInputs.BindCurrent(candidate, current);
            DeploymentTacticCurrentInputs.BindCurrent(candidate, current);
            current.EventConditions.CurrentYear = candidate.NativeMapSeason?.ThirdDate.Year;
            current.EventConditions.ScenarioMode = candidate.CampaignEventWorld?.ScenarioMode;

            var (ids, names, root) = step switch
            {
                StoryMessage m => (m.People, m.Names, m.MessageId),
                StoryNarration n => (n.People, (string[]?)n.Names, n.MessageId),
                _ => throw StoryTextMissing("text_step")
            };
            if (ids.Length != 5 || names is not null && names.Length != 5)
                throw StoryTextMissing("arguments");
            //The special87F3 wrapper reads an independent Integer0 register.
            //The present event packet does not own that register.
            if (root == 0x87F3) throw StoryTextMissing("narration_87F3_Integer0");
            var latest = candidate;
            int Draw(int bound)
            {
                if (bound < 2) return 0;
                var saved = latest.CampaignAppRandom32 ?? throw StoryTextMissing("App32");
                var random = new SharedSourceRandom32(saved.State);
                int result = random.Next(bound);
                latest = latest with { CampaignAppRandom32 = new(random.State) };
                return result;
            }
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            var voices = _personnelNarrationVoice is { } settings ? new NarrationVoiceRequests(settings, Draw) : null;
            var people = new DeploymentNarrationCurrentPeople(candidate, current, abilities, ids[0], ids[1],
                panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(latest, force, effect),
                appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language,
                personArgument2: ids[2], personArgument3: ids[3], personArgument4: ids[4], voiceRequests: voices);
            var report = new ReportCurrent(this, () => latest, current, people,
                new(), true);
            var context = new StoryTextCurrent(report, names);
            var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            TalentOfficerPresentationContext? speaker = null;
            string speakerName = string.Empty;
            if (step is StoryMessage message)
            {
                var pointer = current.ReadPersonLookupReference(message.Speaker) ?? throw StoryTextMissing("speaker_lookup");
                int physical = pointer.PhysicalSlot ?? throw StoryTextMissing("speaker_pointer");
                int identity = current.ReadPersonIdentity(physical) ?? throw StoryTextMissing("speaker_identity");
                var person = candidate.Officers.GetValueOrDefault(identity) ?? throw StoryTextMissing("speaker_owner");
                string fullName = (person.CurrentSurname ?? throw StoryTextMissing("speaker_surname")) +
                    (person.CurrentGivenName ?? throw StoryTextMissing("speaker_given_name"));
                speaker = new(message.Speaker, fullName, current.ReadPersonPortraitAppearance(physical));
                //843AF0 expands the optional display name before the body.
                speakerName = message.NameMessageId is >= 0 ? vm.ResolveStoryText(context, Draw, message.NameMessageId.Value) : fullName;
            }
            string text = vm.ResolveStoryText(context, Draw, root);
            var expanded = new CampaignStoryPreparedText(speakerName, text, SplitStoryPages(text), speaker,
                (voices?.Pending ?? []).Select(SaveVoice).ToImmutableArray());
            return new(new(story.NextStep, expanded), latest.CampaignAppRandom32, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    //845660 takes three lines per page and consumes the separating newline.
    private static ImmutableArray<string> SplitStoryPages(string text)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        int start = 0, lines = 0;
        for (int index = 0; index < text.Length; index++)
            if (text[index] == '\n' && ++lines == 3)
            {
                result.Add(text[start..index]); start = index + 1; lines = 0;
            }
        if (start < text.Length) result.Add(text[start..]);
        return result.ToImmutable();
    }

    private static InvalidDataException StoryTextMissing(string owner) => new("story.text." + owner);

    private sealed class StoryTextCurrent(INarrationCurrentContext inner, string[]? names) : INarrationCurrentContext
    {
        public int PersonValue(int a, int f) => inner.PersonValue(a, f);
        public string PersonText(int a, int f) => inner.PersonText(a, f);
        public int Relationship(int f, int a, int b) => inner.Relationship(f, a, b);
        public int Personality(int a, Func<int> key) => inner.Personality(a, key);
        public string AreaText(int a, int f) => inner.AreaText(a, f);
        public int ArmyValue(int a, int f) => inner.ArmyValue(a, f);
        public string ArmyText(int a, int f) => inner.ArmyText(a, f);
        public int IntegerValue(int a) => inner.IntegerValue(a);
        public int CityValue(int a, int f) => inner.CityValue(a, f);
        public string CityText(int a, int f) => inner.CityText(a, f);
        public int BaseArgumentValidity(int raw) => inner.BaseArgumentValidity(raw);
        public int ForceValue(int a, int f) => inner.ForceValue(a, f);
        public string ForceText(int a, int f) => inner.ForceText(a, f);
        public string DistrictText(int a, int f) => inner.DistrictText(a, f);
        private string Name(int a) => names is not null && (uint)a < (uint)names.Length
            ? HeadHistory(names[a]) : throw StoryTextMissing("String" + a);
        public string StringText(int a, int f)
        {
            string value = Name(a);
            return f is 0 or '2' ? CityDomesticMessageFormatting.FormatAbilityField(value)
                : f == '3' ? string.Empty : throw StoryTextMissing("String_field");
        }
        public int StringValue(int a, int f) => f == '3' ? Name(a).Length : f is 0 or '2'
            ? CityDomesticMessageFormatting.FormatAbilityField(Name(a)).Length : throw StoryTextMissing("String_field");
    }
}
