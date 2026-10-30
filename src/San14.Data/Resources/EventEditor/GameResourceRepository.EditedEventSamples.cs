namespace San14.Data;

public sealed record LoadedEditedEventSamples(EditedEventSampleCatalog Catalog, SourceProvenance Source);

public sealed partial class GameResourceRepository
{
    /// <summary>Reads the installed sample archive through the ordinary file
    /// resource/override boundary. Each load observes the current override.</summary>
    public LoadedEditedEventSamples LoadEditedEventSamples(string locale)
    {
        string relative = locale switch
        {
            "TC" => "0004/EventSampleTc.s14",
            "SC" => "0004/EventSampleSc.s14",
            _ => throw new ArgumentException("Original message locale must be SC or TC.", nameof(locale))
        };
        var (bytes, source) = ReadSource(relative);
        return new(Formats.EditedEventSampleReader.Read(bytes), source);
    }
}
