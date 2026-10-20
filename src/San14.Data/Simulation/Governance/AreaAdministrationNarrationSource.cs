using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The shared draft current inputs plus the existing resource owner.
/// No second projection, writer or executable-derived runtime cache is created.</summary>
internal sealed class AreaAdministrationNarrationSource(GameState state, SourceWorldContext current,
    ArmyOperationContext definitions, LoadedScenario scenario, string language,
    GameResourceRepository repository, OwnedBaseCapacityInputs panels)
{
    private TacticRemakeCurrentAbilities? _abilities;

    internal AreaAdministrationNarration Prepare(string locale, int speaker, int addressee,
        int area, bool appointing, Func<int,int> appPresentationRandom)
    {
        if (locale is not ("TC" or "SC") || state.NativeArmySimulation?.Parameters is not {} parameters ||
            parameters.Language != language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language))
            throw Missing("rule_profile");
        int physical = current.ReadPersonLookupReference(speaker is >= 0 and <= 6000 ? speaker : 0)?.PhysicalSlot
            ?? throw Missing("speaker_pointer");
        if (current.ReadPersonNativeValidity(physical) is not bool valid) throw Missing("speaker_validity");
        if (!valid) throw Missing("invalid_speaker");
        if (current.ReadPersonIdentity(physical) != speaker || definitions.ResolveOfficerSource(speaker) != physical ||
            !state.Officers.TryGetValue(speaker, out var person)) throw Missing("speaker_owner");
        ushort appearance = person.PortraitAppearanceIndex ?? throw Missing("current_portrait");
        current.MoraleWavePersonPostE7Reader ??= person => OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, person);
        _abilities ??= new(current, scenario, language, SimulationRuleIdentity.ForLocale(language));
        var context = new DeploymentNarrationCurrentPeople(state, current, _abilities, speaker, addressee, area,
            (force, effect) => panels.ReadPanelModifier(state, force, effect));
        var message = new NarrationResourceMessages(repository.LoadMessages(locale, MessageKind.Game).Messages)
            .Resolve(context, appPresentationRandom, appointing ? NarrationResourceFamily.AreaAppointment : NarrationResourceFamily.AreaDismissal);
        var portrait = ArmyCampaignRuleAdapter.ReadNarrationPortrait(state, current, definitions, speaker, appearance);
        return new(speaker, person.Name, appearance, portrait, addressee, area,
            message.RootMessageId, message.MessageId, message.Choices, message.Text, message.Pages);
    }
    internal Dictionary<int, PersonalityReference> ReadOfficerSpeechTraits(int physical)
    {
        var result = new Dictionary<int, PersonalityReference>();
        for (int slot = 0; slot < 9; slot++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, physical, slot)
                ?? throw SpeechMissing("trait_pointer");
            bool valid = current.ReadSkillPersonalityValidity(trait) ?? throw SpeechMissing("trait_validity");
            if (!valid) continue;
            int id = trait.PhysicalSlot ?? throw SpeechMissing("trait_identity");
            int key = id switch { 110 => 0, 111 => 1, 113 => 2, 114 => 3, 115 => 4, 116 => 5,
                215 => 16, 218 => 17, 219 => 18, _ => -1 };
            if (key >= 0) result[key] = trait;
        }
        return result;
    }

    internal int ReadOfficerSpeechAbility(int physical, int dimension)
    {
        int id = SpeechPerson(physical);
        _abilities ??= new(current, scenario, language, SimulationRuleIdentity.ForLocale(language));
        return _abilities.ResolveDimension(state, id, dimension,
            (force, effect) => panels.ReadPanelModifier(state, force, effect))
            ?? throw SpeechMissing("effective_ability." + dimension);
    }

    internal AreaAdministrationOfficerSpeechText PrepareOfficerSpeech(string locale, int physical,
        int area, int advice, PersonalityReference? selectedTrait, Func<int,int> appPresentationRandom)
    {
        if (locale is not ("TC" or "SC") || state.NativeArmySimulation?.Parameters is not {} parameters ||
            parameters.Language != language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language))
            throw SpeechMissing("rule_profile");
        int speaker = SpeechPerson(physical);
        //Unlike the parent handover this is the speaker's Force and RAW ruler.
        var force = current.ReadPersonForce(physical) ?? throw SpeechMissing("person_force");
        bool forceValid = current.ReadForceNativeValidity(force) ?? throw SpeechMissing("force_validity");
        int addressee = forceValid ? current.ReadForceRecord(force.PhysicalSlot ?? throw SpeechMissing("force_identity"))?.RawRuler10
            ?? throw SpeechMissing("force_ruler") : 5014;
        current.MoraleWavePersonPostE7Reader ??= person => OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, person);
        _abilities ??= new(current, scenario, language, SimulationRuleIdentity.ForLocale(language));
        var context = new DeploymentNarrationCurrentPeople(state, current, _abilities, speaker, addressee, area,
            (owner, effect) => panels.ReadPanelModifier(state, owner, effect));
        var message = new NarrationResourceMessages(repository.LoadMessages(locale, MessageKind.Game).Messages)
            .ResolveOfficerSpeech(context, appPresentationRandom, OfficerSpeechRoots[advice],
                () => selectedTrait is not null ? current.ReadPersonalityName(selectedTrait) : throw SpeechMissing("selected_trait_name"));
        return new(advice, message.RootMessageId, message.MessageId < 0 ? null : message.MessageId,
            message.Choices, message.Text);
    }

    private int SpeechPerson(int physical)
    {
        int id = current.ReadPersonIdentity(physical) ?? throw SpeechMissing("person_identity");
        if (definitions.ResolveOfficerSource(id) != physical || current.ResolveOfficerSource(id) != physical ||
            current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0)?.PhysicalSlot != physical ||
            !state.Officers.ContainsKey(id)) throw SpeechMissing("person_owner_binding");
        return id;
    }
    private static readonly int[] OfficerSpeechRoots =
        [0x56D9,0x56DE,0x56E3,0x56E8,0x56ED,0x56F2,0x573B,0x56FB,0x5700,0x5705,
         0x570A,0x570F,0x5714,0x571A,0x5720,0x5726,0x572C,0x5731,0x5736];
    private static InvalidDataException SpeechMissing(string input) => new("area-draft.speech." + input);
    private static InvalidDataException Missing(string input) => new("area-draft.handover." + input);
}
