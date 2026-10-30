using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyFlagPaneNameSelection(string SourceTextBeforeSingleUnitCopy,
    ArmyFlagPaneGlyph Glyph, bool ForceValidity, int? PhysicalForce,
    byte? CurrentForceNameIndex48, ushort? CurrentRulerId10, int? SelectedDefinitionIndex,
    int? SelectedPersonLookupId, int? SelectedPersonPhysicalSlot, byte? CurrentForceColor49,
    string SelectionPath, string NativeSourceProvenance)
{
    public bool HasGlyph => Glyph.TwoUtf16Units[0] != '\0';
    public int GlyphCount => HasGlyph ? 1 : 0;
    public string NativeShortName => HasGlyph ? Glyph.TwoUtf16Units[..1] : string.Empty;
    public bool ForceName48IsCurrentStateField => true;
    public bool PersonNameFieldsAreRetainedSourceFields => true;
    public bool PaneNameStateFidelityVerified => false;
    // Ordinary309540 may append a message suffix. A nonempty source prefix
    // determines the sole consumed UTF16 unit, independently of that suffix.
    public bool FullFormattedFallbackTailPublished => SelectionPath != "ordinary person prefix";
}

/// <summary>Owns the actual CE source name48 byte, fixed custom names and
/// person name fields. Current force name index, validity/ruler and PersonID
/// lookup are read from the supplied owned context. Only the initial name index
/// and person text remain source fields; CoreForceState.Name is not an operand.</summary>
public sealed class LoadedArmyFlagPaneNames
{
    private readonly byte[] forceNameIndices;
    private readonly S14CharacterName[] personNames;
    private readonly ushort[] personIdentities;
    private readonly string[] shortNames;
    public LoadedScenario Source { get; }
    public S14ForceNameDefinitionCatalog CustomNames { get; }
    public string DecodedFixedSha256 { get; }

    internal LoadedArmyFlagPaneNames(LoadedScenario source,
        S14ForceNameDefinitionCatalog customNames, string decodedFixedSha256,
        byte[] indices, S14CharacterName[] names, ushort[] identities)
    {
        Source = source; CustomNames = customNames; DecodedFixedSha256 = decodedFixedSha256;
        forceNameIndices = indices.ToArray(); personNames = names.ToArray(); personIdentities = identities.ToArray();
        shortNames = customNames.Definitions.Select(value => value.ShortName).ToArray();
    }

    public byte ReadSourceForceNameIndex48(int physicalForce)
    {
        if ((uint)physicalForce >= 52) throw Error("source force identity is outside52 rows");
        return forceNameIndices[physicalForce];
    }

    public ArmyFlagPaneNameSelection Resolve(SourceWorldContext current,
        ForceReference force)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(force);
        if (current.StaticSource?.Sha256 != Source.ScenarioSource.Sha256 ||
            current.StaticSource.ResourceUri != Source.ScenarioSource.ResourceUri)
            throw Error("requires a current owned context from the same complete scenario source");
        bool valid = current.ReadForceNativeValidity(force)
            ?? throw Error("current force validity/ruler10 is unknown");
        int? physical = force.PhysicalSlot;
        if (!valid) return Select(string.Empty, false, physical, null, null, null, null, null, null,
            "invalid force", "SC2ACFDA..2ACFE1 zeros4-byte text;32FDF0 invalid Force skips source name operands");
        if (physical is not int slot || (uint)slot >= 52) throw Error("valid force has no supported owned identity");
        ushort ruler = current.ReadForceRecord(slot)?.RawRuler10
            ?? throw Error("current ruler10 is unknown");
        byte index = current.ReadForceKingdomNameIndex(slot) ?? throw Error("current kingdom name index is unknown");
        int definition = index <= 100 ? index : 0;
        byte? color = current.ReadForceReportColor(force);
        if (definition != 0)
            return Select(shortNames[definition], true, slot, index, ruler, definition, null, null, color,
                "custom name definition", $"SC2ACFF2..2AD028 current Force+48={index}, World+78168[{definition}], CKingdomData+10;30E110 copiesoneUTF16unit");

        // Concrete CForceData+60 is (this-WorldDF30)/268. The native
        // special slots46..50 use2313C0 and a separate formatted branch.
        TransferPersonReference person = (ruler <= 6000
            ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))
            ?? throw Error("selected current PersonID lookup pointer is unknown");
        int? personPhysical = person.PhysicalSlot;
        if (!personPhysical.HasValue)
            return Select(string.Empty, true, slot, index, ruler, definition,
                ruler <= 6000 ? ruler : null, null, color, "invalid fallback person",
                "SC22F148..22F152 selected Person pointer fails32FDF0; existing zero text remains");
        int personSlot = personPhysical.Value;
        bool personValid = current.ReadPersonNativeValidity(personSlot)
            ?? throw Error("selected current Person validity is unknown");
        if (!personValid)
            return Select(string.Empty, true, slot, index, ruler, definition,
                ruler <= 6000 ? ruler : null, personSlot, color, "invalid fallback person",
                "SC22F148..22F152 selected Person fails32FDF0; existing zero text remains");
        if ((uint)personSlot >= personNames.Length ||
            current.ReadPersonIdentity(personSlot) != personIdentities[personSlot])
            throw Error("selected current Person identity differs from the retained source name fields");
        ushort personIdentity = personIdentities[personSlot];
        bool specialForce = slot is >= 46 and <= 50;
        bool specialConcat = unchecked((ushort)(personIdentity - 5101)) <= 99 ||
            unchecked((ushort)(personIdentity - 2062)) <= 4;
        string prefix = specialForce && !specialConcat ? personNames[personSlot].FamilyName :
            personNames[personSlot].FamilyName + personNames[personSlot].GivenName;
        if (prefix.Length == 0 || prefix[0] == '\0')
        {
            if (!specialForce) throw Error("valid empty Person names require the actual current309540 message suffix");
            return Select(string.Empty, true, slot, index, ruler, definition,
                ruler <= 6000 ? ruler : null, personSlot, color, "special person name",
                "SC2313C0 selects Person family/concatenation;309540 special literal '%s' retains empty source text");
        }
        return Select(prefix, true, slot, index, ruler, definition,
            ruler <= 6000 ? ruler : null, personSlot, color,
            specialForce ? "special person name" : "ordinary person prefix",
            specialForce
                ? $"SC22F190..22F1A6 Force slot46..50;2313C0 identity{personIdentity} chooses{(specialConcat ? "Person+12/+24 concatenation" : "Person+12")};309540 special '%s';30E110 copiesoneUTF16unit"
                : $"SC22F0D0 current ruler10={ruler}->WorldPerson lookup;22F305..22F32E '%s%s' Person+12/+24;309540 appends only a suffix to nonempty prefix;30E110 copiesoneUTF16unit");
    }

    public ArmyFlagPaneNameSelection ResolveCarriedKind2(SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(current);
        ForceReference last = current.ReadForceTableReference(51)
            ?? throw Error("final banner-loop Force table slot51 is unknown");
        var selected = Resolve(current, last);
        return selected with { NativeSourceProvenance = selected.NativeSourceProvenance +
            ";SC17E4A0..17E5C7 banner loop leaves last Force51 buffer;17E5D0..17E617 kind2 reuses it without another Force name query" };
    }

    private static ArmyFlagPaneNameSelection Select(string sourcePrefix, bool valid,
        int? physical, byte? nameIndex, ushort? ruler, int? definition, int? personId,
        int? personPhysical, byte? color, string path, string provenance)
    {
        var glyph = ArmyFlagPaneColors.CopySourceNameGlyph(sourcePrefix, provenance);
        return new(sourcePrefix, glyph, valid, physical, nameIndex, ruler, definition, personId,
            personPhysical, color, path, provenance);
    }
    private static InvalidDataException Error(string message) => new($"Army flag pane name {message}.");
}

public static class ArmyFlagPaneNames
{
    public static LoadedArmyFlagPaneNames Load(LoadedScenario source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale is not ("SC" or "TC") ||
            source.Snapshot.Forces.Count != 52 || source.Snapshot.Characters.Count != 1851)
            throw Error("requires coherent complete CE source force/person rows");
        if (!source.Snapshot.FixedDataPath.Equals($"0010_{source.Snapshot.Locale}/fixdataexce.s14", StringComparison.OrdinalIgnoreCase) ||
            !source.FixedDataSource.SourcePath.Equals(source.Snapshot.FixedDataPath, StringComparison.OrdinalIgnoreCase) ||
            !source.ScenarioSource.SourcePath.Equals(source.Snapshot.SourcePath, StringComparison.OrdinalIgnoreCase) ||
            !source.Metadata.SourcePath.Equals(source.Snapshot.SourcePath, StringComparison.OrdinalIgnoreCase))
            throw Error("fixed/scenario source paths differ from the explicitly selected CE locale");
        S14Document scenario = source.InitialScenarioDocument ?? S14Reader.Read(Read(source.ScenarioSource), true);
        S14Document fixedData = S14Reader.Read(Read(source.FixedDataSource), false);
        string scenarioSha = Hash(scenario.DecodedPayload);
        if (source.InitialScenarioDocument is null &&
            (scenarioSha != source.Metadata.DecodedSha256 || scenarioSha != source.Snapshot.DecodedSha256) ||
            scenario.FormatMarker != 0x66 || fixedData.FormatMarker != 0x66)
            throw Error("scenario payload differs from the complete loaded source");
        var forces = S14ScenarioTables.ReadScenarioForces(scenario);
        byte[] indices = new byte[52];
        for (int slot = 0; slot < indices.Length; slot++)
        {
            ForcePreviewRecord snapshot = source.Snapshot.Forces[slot];
            S14ScenarioForce row = forces[slot];
            if (snapshot.Slot != slot || row.Slot != slot || snapshot.RulerSlot != row.RulerSlot ||
                !snapshot.RemainingBytesHex.Equals(row.RemainingBytesHex, StringComparison.OrdinalIgnoreCase))
                throw Error("retained Force row differs from the loaded scenario source");
            indices[slot] = row.RawKingdomName48 ?? throw Error("source kingdom name index is unknown");
        }
        var personTable = S14ScenarioTables.ReadCharacterNames(scenario, source.Snapshot.Locale);
        int nameBytes = source.Snapshot.Locale == "SC" ? 18 : 14;
        var names = new S14CharacterName[personTable.Names.Count];
        for (int slot = 0; slot < names.Length; slot++)
        {
            int at = personTable.RecordOffset + slot * personTable.RecordStride;
            // Native printf copies UTF16 words. Read raw fields without a
            // decoder replacement character or normalization step.
            names[slot] = new(slot, S14ForceNameDefinitionReader.ReadLiteralUnits(
                scenario.DecodedPayload.AsSpan(at, nameBytes)),
                S14ForceNameDefinitionReader.ReadLiteralUnits(scenario.DecodedPayload.AsSpan(at + nameBytes, nameBytes)));
        }
        var identities = S14ScenarioTables.ReadCharacterIdentities(scenario, source.Snapshot.Locale);
        ushort[] ids = new ushort[names.Length];
        for (int slot = 0; slot < names.Length; slot++)
        {
            CharacterPreviewRecord person = source.Snapshot.Characters[slot];
            if (person.Slot != slot || names[slot].Slot != slot || identities[slot].Slot != slot ||
                person.CharacterId != identities[slot].CharacterId || person.Name != names[slot].DisplayName)
                throw Error("retained Person name/identity differs from the loaded scenario source");
            ids[slot] = identities[slot].CharacterId;
        }
        return new(source, S14ForceNameDefinitionReader.Read(fixedData), Hash(fixedData.DecodedPayload), indices, names, ids);
    }

    private static byte[] Read(SourceProvenance source)
    {
        if (string.IsNullOrWhiteSpace(source.PhysicalPath)) throw Error("requires a concrete labelled source file");
        byte[] bytes = File.ReadAllBytes(source.PhysicalPath);
        if (bytes.LongLength != source.Bytes || Hash(bytes) != source.Sha256)
            throw Error("source file changed after the coherent scenario capture");
        return bytes;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static InvalidDataException Error(string message) => new($"Army flag pane name {message}.");
}
