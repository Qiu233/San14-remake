namespace San14.Data.Formats;

/// <summary>The original stored loyalty byte. Values outside the normal displayed
/// range retain their original value and are not converted into a guessed loyalty.</summary>
public sealed record S14CharacterLoyaltyReference(int Slot, byte RawLoyalty);

public static class S14PersonnelSourceReader
{
    /// <summary>Reads the already retained eight-byte placement source window in
    /// an existing preview. Unverified editions and missing older windows remain
    /// unknown; malformed present source data does not silently disappear.</summary>
    public static byte? ReadCharacterLoyaltyPreview(CharacterPreviewRecord person,
        uint formatMarker)
    {
        ArgumentNullException.ThrowIfNull(person);
        if (formatMarker != 0x66 || string.IsNullOrEmpty(person.RawBytesHex)) return null;
        if (person.RawBytesHex.Length != 16)
            throw new InvalidDataException("Original CE loyalty preview requires its retained eight-byte placement window.");
        try { return Convert.FromHexString(person.RawBytesHex)[7]; }
        catch (FormatException error)
        { throw new InvalidDataException("Original CE loyalty preview has malformed source hexadecimal bytes.", error); }
    }

    /// <summary>CItemData+0x49 used by original grant/confiscation loyalty math.
    /// This differs from CItemData+0x46, the five-ability bonus value.
    /// CE fixed definitions use row+19; CE embedded definitions use their own
    /// row+57 (the enclosing scenario item's row+63).</summary>
    public static byte ReadItemLoyaltyValue(S14ItemDefinition definition, uint formatMarker)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (formatMarker != 0x66)
            throw new InvalidDataException("Original item loyalty value requires the verified CE source profile.");
        int offset = definition.RecordStride switch
        {
            22 => 19,
            59 => 57,
            _ => throw new InvalidDataException("Original CE item definition has an unsupported source stride.")
        };
        if (definition.RawBytes is null || definition.RawBytes.Length != definition.RecordStride)
            throw new InvalidDataException("Original CE item loyalty value source bytes differ from their definition stride.");
        return definition.RawBytes[offset];
    }

    /// <summary>CE CPersonData serializes this byte immediately after the verified
    /// office reference; the legacy two-byte intervening field is absent in marker 0x66.
    /// This is source storage, not a completed game startup or UI value.</summary>
    public static IReadOnlyList<S14CharacterLoyaltyReference> ReadCharacterLoyalty(
        S14Document scenario, string locale)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.FormatMarker != 0x66)
            throw new InvalidDataException("Original loyalty source reader requires the verified CE scenario profile.");
        if (locale is not ("SC" or "TC"))
            throw new ArgumentOutOfRangeException(nameof(locale));
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        int fieldOffset = locale == "SC" ? 219 : 163;
        var result = new S14CharacterLoyaltyReference[table.Names.Count];
        for (int slot = 0; slot < result.Length; slot++)
            result[slot] = new(slot, scenario.DecodedPayload[checked(table.RecordOffset +
                slot * table.RecordStride + fieldOffset)]);
        return result;
    }
}
