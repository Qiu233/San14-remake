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
            if (step is not (StoryMessage or StoryNarration or StoryFormatOnlyMessage))
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
                StoryFormatOnlyMessage f => (f.People, f.Names, f.MessageId),
                _ => throw StoryTextMissing("text_step")
            };
            int[]? cities = step switch { StoryMessage m => m.Cities, StoryNarration n => n.Cities, StoryFormatOnlyMessage f => f.Cities, _ => null };
            int[]? forces = step switch { StoryMessage m => m.Forces, StoryNarration n => n.Forces, StoryFormatOnlyMessage f => f.Forces, _ => null };
            int[]? areas = step switch { StoryMessage m => m.Areas, StoryNarration n => n.Areas, StoryFormatOnlyMessage f => f.Areas, _ => null };
            int[]? items = step switch { StoryMessage m => m.Items, StoryNarration n => n.Items, StoryFormatOnlyMessage f => f.Items, _ => null };
            int?[]? numbers = step switch { StoryMessage m => m.Numbers, StoryNarration n => n.Numbers, StoryFormatOnlyMessage f => f.Numbers, _ => null };
            if (ids.Length != 5 || names is not null && names.Length != 5)
                throw StoryTextMissing("arguments");
            if (cities is not null && (cities.Length != 5 || cities.Any(id => id is < 0 or > ushort.MaxValue)))
                throw StoryTextMissing("city_arguments");
            if (forces is not null && (forces.Length != 5 || forces.Any(id => id is < 0 or > ushort.MaxValue)) ||
                areas is not null && (areas.Length != 5 || areas.Any(id => id is < 0 or > ushort.MaxValue)))
                throw StoryTextMissing("force_area_arguments");
            if (items is not null && (items.Length != 5 || items.Any(id => id is < 0 or > 201)) ||
                numbers is not null && numbers.Length != 5)
                throw StoryTextMissing("item_number_arguments");
            //94's experience-bonus wrapper has already selected its resource
            //and written Number1 in the invocation-local execution context.
            //All message families now consume their captured bank normally.
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
            ushort? City(int index) => cities is null ? null : checked((ushort)cities[index]);
            int? Force(int index) => forces?[index];
            int? Area(int index) => areas?[index];
            var report = new ReportCurrent(this, () => latest, current, people,
                new(Base0: City(0), Base1: City(1), Base2: City(2), Base3: City(3), Base4: City(4),
                    Force0: Force(0), Force1: Force(1), Force2: Force(2), Force3: Force(3), Force4: Force(4),
                    Area0: Area(0), Area1: Area(1), Area2: Area(2), Area3: Area(3), Area4: Area(4)), true);
            int?[]? provinces = step switch { StoryMessage m => m.Provinces, StoryNarration n => n.Provinces,
                StoryFormatOnlyMessage f => f.Provinces, _ => null };
            var context = new StoryTextCurrent(report, names, numbers, new(this, () => latest, current, items),
                new(this, () => latest, current, provinces));
            var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            TalentOfficerPresentationContext? speaker = null;
            string speakerName = string.Empty;
            if (step is StoryMessage message)
            {
                var pointer = (message.WithoutPortrait && message.Speaker > 6000
                    ? current.ReadTransferPersonTableReference(0) : current.ReadPersonLookupReference(message.Speaker))
                    ?? throw StoryTextMissing("speaker_lookup");
                int physical = pointer.PhysicalSlot ?? throw StoryTextMissing("speaker_pointer");
                //332 has no outer validity gate. Its invalid source receiver
                //still formats/logs/displays text, but842D70 leaves the name
                //pane unchanged unless an explicit name resource replaces it.
                bool valid = !message.WithoutPortrait || (current.ReadPersonNativeValidity(physical)
                    ?? throw StoryTextMissing("without_portrait_speaker_validity"));
                string? fullName = null;
                if (valid)
                {
                    int identity = current.ReadPersonIdentity(physical) ?? throw StoryTextMissing("speaker_identity");
                    var person = candidate.Officers.GetValueOrDefault(identity) ?? throw StoryTextMissing("speaker_owner");
                    fullName = (person.CurrentSurname ?? throw StoryTextMissing("speaker_surname")) +
                        (person.CurrentGivenName ?? throw StoryTextMissing("speaker_given_name"));
                    speaker = new(message.Speaker, fullName, current.ReadPersonPortraitAppearance(physical));
                }
                //8439F0 expands an optional display name before the body.
                speakerName = message.NameMessageId is >= 0
                    ? vm.ResolveStoryText(context, Draw, message.NameMessageId.Value)
                    : fullName ?? story.Scene.RetainedSpeakerName
                        ?? throw StoryTextMissing("without_portrait_retained_speaker_name");
            }
            string text = vm.ResolveStoryText(context, Draw, root);
            //8440D0 (SpeechOnly) presents the whole formatted line with one
            //InputWait. It never calls845660's three-line pagination helper.
            var pages = step is StoryFormatOnlyMessage ? ImmutableArray<string>.Empty
                : step is StoryMessage { SpeechOnly: true }
                ? ImmutableArray.Create(text) : SplitStoryPages(text);
            var expanded = new CampaignStoryPreparedText(speakerName, text, pages, speaker,
                (voices?.Pending ?? []).Select(SaveVoice).ToImmutableArray())
                { SpeechOnly = step is StoryMessage { SpeechOnly: true }, FormatOnly = step is StoryFormatOnlyMessage };
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

    private sealed class StoryTextCurrent(INarrationCurrentContext inner, string[]? names,
        int?[]? numbers, StoryItemMessageCurrent items, StoryProvinceMessageCurrent? provinces = null) : INarrationCurrentContext
    {
        public int ProvinceValue(int a, int f) => provinces?.ProvinceValue(a, f) ?? inner.ProvinceValue(a, f);
        public string ProvinceText(int a, int f) => provinces?.ProvinceText(a, f) ?? inner.ProvinceText(a, f);
        public int PersonValue(int a, int f) => inner.PersonValue(a, f);
        public string PersonText(int a, int f) => inner.PersonText(a, f);
        public int Relationship(int f, int a, int b) => inner.Relationship(f, a, b);
        public int Personality(int a, Func<int> key) => inner.Personality(a, key);
        public string AreaText(int a, int f) => inner.AreaText(a, f);
        public int AreaValue(int a, int f) => inner.AreaValue(a, f);
        public int ArmyValue(int a, int f) => inner.ArmyValue(a, f);
        public string ArmyText(int a, int f) => inner.ArmyText(a, f);
        public int IntegerValue(int a) => numbers is not null && (uint)a < (uint)numbers.Length && numbers[a] is int value
            ? value : throw StoryTextMissing("Number" + a);
        public int ItemValue(int a, int f) => items.ItemValue(a, f);
        public string ItemText(int a, int f) => items.ItemText(a, f);
        public int CityValue(int a, int f) => inner.CityValue(a, f);
        public string CityText(int a, int f) => inner.CityText(a, f);
        public int BaseArgumentValidity(int raw) => inner.BaseArgumentValidity(raw);
        public int ForceValue(int a, int f) => inner.ForceValue(a, f);
        public string ForceText(int a, int f) => inner.ForceText(a, f);
        public string DistrictText(int a, int f) => inner.DistrictText(a, f);
        private string Name(int a) => names is not null && (uint)a < (uint)names.Length && names[a] is string value
            ? HeadHistory(value) : throw StoryTextMissing("String" + a);
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
