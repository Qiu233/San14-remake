using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One finite source field formatter shared with the City's hint consumer.</summary>
public static class CityDomesticMessageFormatting
{
    public static string FormatCityNameField(string name)
    {
        var output = new StringBuilder(); Append(output, "^05"); Append(output, name); Append(output, "^00");
        return output.ToString();
    }
    internal static string FormatAbilityField(string text)
    { var output = new StringBuilder(); Append(output, text); return output.ToString(); }
    internal static string FormatPersonNameField(string name)
    {
        var output = new StringBuilder(); Append(output, "^02"); Append(output, name); Append(output, "^00");
        return output.ToString();
    }
    private static void Append(StringBuilder output, string value)
    {
        int nul = value.IndexOf('\0'); int units = nul < 0 ? value.Length : nul;
        uint combined = unchecked((uint)(output.Length + units + 1));
        if ((ulong)combined * 2 <= 0x80) output.Append(value, 0, units);
    }
}

internal sealed class CityDomesticNarrationSource(GameState state, SourceWorldContext current,
    ArmyOperationContext definitions, LoadedScenario scenario, string language,
    GameResourceRepository repository, OwnedBaseCapacityInputs panels)
{
    internal CityDomesticNarration Prepare(string locale, int root, int speaker, int addressee,
        ushort cityLink, Func<int, int> random)
    {
        if (locale is not ("TC" or "SC") || locale != language ||
            state.NativeArmySimulation?.Parameters is not {} parameters || parameters.Language != language ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language)) throw Missing("rule_profile");
        current.MoraleWavePersonPostE7Reader ??= p => OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, p);
        var abilities = new TacticRemakeCurrentAbilities(current, scenario, language, SimulationRuleIdentity.ForLocale(language));
        MessageDocument? data = null;
        var context = new DeploymentNarrationCurrentPeople(state, current, abilities, speaker, addressee,
            panelReader: (force, effect) => panels.ReadPanelModifier(state, force, effect),
            cityArgument: cityLink, readZeroStrengthText: () =>
            {
                data ??= repository.LoadMessages(locale, MessageKind.Data).Messages;
                return data.Sections[1].Records[0x353].Text ?? throw Missing("zero_strength_resource");
            });
        var message = new NarrationResourceMessages(repository.LoadMessages(locale, MessageKind.Game).Messages)
            .ResolveCity(context, random, root);

        //Only the selected actor, after the complete VM and its random prefix.
        int physical = current.ReadPersonLookupReference(speaker is >= 0 and <= 6000 ? speaker : 0)
            ?.PhysicalSlot ?? throw Missing("speaker_pointer");
        if (current.ReadPersonNativeValidity(physical) is not bool valid) throw Missing("speaker_validity");
        if (!valid) throw Missing("invalid_speaker");
        int identity = current.ReadPersonIdentity(physical) ?? throw Missing("speaker_identity");
        if (definitions.ResolveOfficerSource(identity) != physical || current.ResolveOfficerSource(identity) != physical ||
            !state.Officers.TryGetValue(identity, out var person)) throw Missing("speaker_owner");
        string name = person.Name ?? throw Missing("speaker_name");
        ushort appearance = person.PortraitAppearanceIndex ?? throw Missing("speaker_appearance");
        var portrait = ArmyCampaignRuleAdapter.ReadNarrationPortrait(state, current, definitions, identity, appearance);
        return new(speaker, addressee, cityLink, name, appearance, portrait, message.RootMessageId,
            message.MessageId, message.Choices, message.Text, message.Pages);
    }
    private static InvalidDataException Missing(string field) => new("city-domestic.handover." + field);
}
