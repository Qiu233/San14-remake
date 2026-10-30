using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Historical identities and immutable prototype text stay separate
/// from the registered-person editor's editable name/family record.</summary>
internal static class HistoricalOfficerEditorValueMapper
{
    internal static RegisteredOfficerEditorValues Create(GameResourceRepository repository, PreviewText text,
        HistoricalOfficerEditorRow row, HistoricalOfficerEdit value, HistoricalOfficerEditorCatalog catalog,
        IReadOnlyList<LoadedSprite>? portrait, bool canConfirm, string notice)
    {
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        string Message(int id) => messages[id].Text ?? throw new InvalidDataException("Missing historical officer value caption.");
        string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
        string Named(IReadOnlyList<RegisteredOfficerEditorChoice> choices, int id) => id == 0 ? ""
            : choices.FirstOrDefault(choice => choice.Id == id)?.Name ?? text.Get("detail_unknown");
        var basic = new Dictionary<int, string>
        {
            [3] = row.Name, [4] = row.CourtesyName.Length == 0 ? "" : "(" + row.CourtesyName + ")",
            [5] = Message(0x4E0), [6] = row.CourtesyName,
            [9] = Message(row.Sex == 0 ? 0x495 : 0x496),
            [10] = Message(0x4E5), [11] = row.VoiceType is >= 1 and <= 13
                ? Message(0xE8 + row.VoiceType) : text.Get("detail_unknown"),
            [43] = Named(catalog.Doctrines, value.DoctrineId), [44] = Named(catalog.Policies, value.PolicyId),
            [45] = Message(0x250), [46] = Number(value.PolicyBaseCost)
        };
        int[] labels = [19, 21, 23, 25, 27], numbers = [20, 22, 24, 26, 28];
        byte[] abilities = [value.Leadership, value.Strength, value.Intelligence, value.Politics, value.Charm];
        for (int index = 0; index < abilities.Length; index++)
        { basic[labels[index]] = Message(0x238 + index); basic[numbers[index]] = Number(abilities[index]); }
        int[] traitNodes = [31, 33, 35, 37, 39];
        for (int index = 0; index < traitNodes.Length; index++) basic[traitNodes[index]] = Named(catalog.Personalities, value.TraitIds[index]);
        for (int index = 0; index < 10; index++)
        {
            basic[49 + index] = Named(catalog.Tactics, value.TacticIds[index]);
            basic[62 + index * 2] = index < value.FormationIds.Length ? Named(catalog.Formations, value.FormationIds[index]) : "";
        }
        var relations = new Dictionary<int, string>
        {
            [3] = Message(0x55F), [4] = Number(row.BirthYear) + "～" + Number(value.DeathYear),
            [5] = Message(0x4C6), [6] = Number(value.DeathYear - row.BirthYear + 1),
            [47] = Message(0x42C), [48] = Named(catalog.Officers, row.FatherId),
            [49] = Message(0x42D), [50] = Named(catalog.Officers, row.MotherId),
            [51] = Message(0x42E), [52] = Named(catalog.Officers, row.SpouseId),
            [53] = Message(0x2BF), [54] = Named(catalog.Officers, row.SwornBrotherId),
            // The historical compatibility byte is not a registered Person ID.
            // Its readonly source presentation remains an explicit UI boundary.
            [56] = Message(0x4EA), [57] = text.Get("detail_unknown")
        };
        for (int index = 0; index < 8; index++)
        {
            relations[60 + index] = Named(catalog.Officers, value.LikePersonIds[index]);
            relations[70 + index] = Named(catalog.Officers, value.DislikePersonIds[index]);
        }
        return new(portrait, [basic, relations, new Dictionary<int, string>()], canConfirm, notice);
    }
}
