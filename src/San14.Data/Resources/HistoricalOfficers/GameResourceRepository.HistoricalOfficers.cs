using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    public HistoricalOfficerEditorCatalog LoadHistoricalOfficerEditorCatalog(string locale,
        RegisteredOfficerLibrary registeredLibrary, CollectionOfficerAdmission? admission = null,
        PersonalityEditorLibrary? personalities = null)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentException("Historical editor locale must be TC or SC.", nameof(locale));
        ArgumentNullException.ThrowIfNull(registeredLibrary); registeredLibrary.Validate();
        var (prototypeBytes, prototypeSource) = ReadSource($"0010_{locale}/scedaexce08.s14");
        var (fixedBytes, fixedSource) = ReadSource($"0010_{locale}/fixdataexce.s14");
        var prototype = S14Reader.Read(prototypeBytes, true);
        var fixedData = S14Reader.Read(fixedBytes, false);
        if (prototype.FormatMarker != 0x66 || fixedData.FormatMarker != 0x66)
            throw new InvalidDataException("Historical editor requires the CE title prototype and fixed definitions.");
        var title = new CollectionOfficerCatalog(locale, prototype, prototypeSource, fixedData, fixedSource,
            LoadMessages(locale, MessageKind.Data), admission, ReadCollectionSpecialOfficerDirectory(admission));
        var traits = PersonalityEditorRuntime.Compose(LoadPersonalities($"0010_{locale}/fixdataexce.s14"),
            personalities ?? PersonalityEditorLibrary.Empty).Catalog;
        return title.CreateHistoricalEditorCatalog(prototype, fixedData, registeredLibrary, traits, this);
    }
}
