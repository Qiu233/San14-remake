using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The mode-one Help speaker supplied by the normal title resource.
/// This is a resource publication, not a campaign or a fabricated Person.</summary>
public sealed record EventEditorHelpAdviser(int PersonId, string Name, string Body,
    DeploymentNarrationPortraitRoute StandingPortrait, SourceProvenance PersonSource,
    SourceProvenance MessageSource);

public sealed partial class EventEditorHelpCatalog
{
    internal static EventEditorHelpAdviser ReadAdviser(string locale, S14Document prototype,
        SourceProvenance personSource, LoadedMessages game)
    {
        //849BB0 mode1 uses the ordinary Person table's5014 receiver, then
        //84B620 reads that Person's actual name and A6 appearance.
        const int personId = 5014;
        var identity = S14ScenarioTables.ReadCharacterIdentities(prototype, locale)
            .SingleOrDefault(row => row.CharacterId == personId)
            ?? throw new InvalidDataException("event_editor.help.adviser.person_definition");
        var names = S14ScenarioTables.ReadCharacterNames(prototype, locale);
        string name = names.Names[identity.Slot].DisplayName;
        ushort appearance = identity.AppearanceCode;
        DeploymentNarrationPortraitRoute portrait;
        if (DeploymentNarrationPortraitRoute.IsAnimatedAppearance(appearance))
            portrait = DeploymentNarrationPortraitRoute.Animated(appearance);
        else
        {
            //The installed mode-one speaker is ordinary appearance1000.
            //Do not infer DLC/edit availability if a replacement resource
            //changes this speaker to one of those separate route families.
            if (appearance > 1310)
                throw new InvalidDataException("event_editor.help.adviser.nonordinary_appearance_availability");
            var route = ArmyMapUiSources.Create().PortraitNames(appearance);
            string number = route.ResolvedAppearanceIndex.ToString("D4", CultureInfo.InvariantCulture) +
                (route.ResolvedAppearanceIndex != appearance ? "b" : "");
            string stem = "face_" + number;
            portrait = new(appearance, route.ResolvedAppearanceIndex, "texture_chara_anime_" + number,
                [stem + "_b", stem + "_a", stem + "_c"]);
        }
        //The actual TC/SC9FCD records contain literal text and their normal
        //terminator only. Use the same strict parser as the Help catalogue;
        //no duplicate message expander or fake World parameters are needed.
        return new(personId, name, Text(game.Messages, 0x9FCD), portrait, personSource, game.Source);
    }
}

public sealed partial class GameResourceRepository
{
    public EventEditorHelpAdviser LoadEventEditorHelpAdviser(string locale)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        //Use the existing normal title Person prototype, as the title's
        //resource-only officer catalogues do. Never construct a World merely
        //to obtain the fixed Help speaker.
        var (bytes, source) = ReadSource($"0010_{locale}/scedaexce08.s14");
        var document = S14Reader.Read(bytes, true);
        if (document.FormatMarker != 0x66)
            throw new InvalidDataException("event_editor.help.adviser.prototype_profile");
        return EventEditorHelpCatalog.ReadAdviser(locale, document, source, LoadMessages(locale, MessageKind.Game));
    }
}
