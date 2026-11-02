namespace San14.Data.Formats;

/// <summary>The Force header flags retained by CE237CC0's normal329-byte
/// scenario row. This is a load-field reader, not a current-save fallback.</summary>
internal static class S14ForceScenarioFlagsReader
{
    internal static byte ReadFlags12(ForcePreviewRecord row)
    {
        ArgumentNullException.ThrowIfNull(row);
        const int recordSize = 329;
        const int rulerSize = 2;
        const int serializedFlagsOffset = 326;
        if (row.RemainingBytesHex.Length != (recordSize - rulerSize) * 2)
            throw new InvalidDataException("CE Force flags require the complete329-byte normal scenario row.");
        byte[] tail;
        try { tail = Convert.FromHexString(row.RemainingBytesHex); }
        catch (FormatException error)
        { throw new InvalidDataException("CE Force source flags are malformed.", error); }
        //237CC0 serializes Force16A and16C as words, then Force12 as one
        //byte, then1BC as a word. The native offset is not the file offset.
        return tail[serializedFlagsOffset - rulerSize];
    }
}
