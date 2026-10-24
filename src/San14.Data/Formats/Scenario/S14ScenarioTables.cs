using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14CharacterName(int Slot, string FamilyName, string GivenName)
{
    public string DisplayName => FamilyName + GivenName;
    public string? NameSortKey { get; init; }
    public string? SurnameSortKey { get; init; }
    public string? GivenNameSortKey { get; init; }
}

public sealed record S14CharacterTable(int RecordOffset, int RecordStride,
    IReadOnlyList<S14CharacterName> Names);

public sealed record S14CharacterPlacement(int Slot, byte DistrictSlot, ushort CitySlot,
    ushort SecondaryCitySlot, byte StatusCode, string RawBytesHex);

public sealed record S14CharacterAbilities(int Slot, byte Leadership, byte Strength,
    byte Intelligence, byte Politics, byte Charm);

public sealed record S14CharacterIdentity(int Slot, ushort CharacterId, ushort AppearanceCode);

// Retain unknown source codes; semantic mapping belongs to the core adapter.
public sealed record S14CharacterSex(int Slot, byte RawCode);

public sealed record S14CityName(int Slot, string Name);

public sealed record S14CityDefinition(int Slot, string Name, string SecondaryText,
    IReadOnlyList<byte> LinkedLocationSlots, uint RawLimit0, uint RawLimit1,
    uint RawLimit2, byte GroupCode);

public sealed record S14GateDefinition(int Slot, int LocationSlot, string Name,
    string SecondaryText, IReadOnlyList<byte> LinkedLocationSlots);

public sealed record S14AreaDefinition(int Slot, string Name, string SecondaryText,
    byte CitySlot, ushort HexId, string RemainingBytesHex)
{
    public byte FacilityCapacity { get; init; }
    // Fixed-row words +41/+43 are copied to the native area object at +44/+46.
    // Their gameplay meaning remains unverified; keep the original bytes too.
    public ushort RawField44 { get; init; }
    public ushort RawField46 { get; init; }
    public IReadOnlyList<int> AdjacentAreaSlots { get; init; } = [];
    // All eight source words, including zero terminators and reserved raw values.
    public IReadOnlyList<ushort>? RawNeighborSlots { get; init; }
    public byte RegionalNeighborMask { get; init; }
}

public sealed record S14ScenarioCity(int Slot, ushort OfficerSlot, byte DistrictSlot,
    uint Gold, uint Supplies, uint Troops, byte Morale, ushort Durability,
    byte SizeCode, string RemainingBytesHex)
{
    public byte? RawValidityFlag69 { get; init; }
    public byte? RawAutomaticTactics48 { get; init; }
}

public sealed record S14ScenarioGate(int Slot, ushort OfficerSlot, byte DistrictSlot,
    uint Amount0, uint Amount1, uint Troops, string RemainingBytesHex)
{
    public byte? RawValidityFlag69 { get; init; }
    public byte? RawAutomaticTactics48 { get; init; }
    public byte? RawMorale44 { get; init; }
    public ushort? RawField46 { get; init; }
}

public sealed record S14ScenarioForce(int Slot, ushort RulerSlot, string RemainingBytesHex)
{
    public byte? RawKingdomName48 { get; init; }
    public byte? RawReportColor49 { get; init; }
    // CE force-row byte ranges copied into the native force object at +0x13 and +0xEA.
    // Their gameplay meaning is not established; older editions remain unresolved.
    public byte[]? RawIndexedBytes13 { get; init; }
    public byte[]? RawIndexedBytesEA { get; init; }
}

public sealed record S14ScenarioDistrict(int Slot, byte ForceSlot, byte Number,
    ushort LeaderSlot);

public sealed record S14ScenarioArea(int Slot, ushort Amount0, byte Value2,
    byte ForceSlot, string RemainingBytesHex)
{
    public byte? RawFlags38 { get; init; }
    public byte? RawFlag74 { get; init; }
}

/// <summary>
/// Verified byte boundaries in the installed base, PK, and CE S14 payloads.
/// Confirmed fields use game names. Undecoded bytes remain available without
/// assigning guessed semantics.
/// </summary>
public static class S14ScenarioTables
{
    private const int CityCount = 52;
    private const int GateCount = 11;
    private const int AreaCount = 501;
    private const int ForceSlotCount = 52;
    private const int CityNameStride = 53;
    private const int GateStride = 40;
    private const int AreaStride = 65;
    private const int MapCellCount = 48_400;

    public static S14CharacterTable ReadCharacterNames(S14Document scenario, string locale)
    {
        int count = scenario.LeadingRecordCount is int idCount ? idCount - 1 : -1;
        (int offset, int expectedCount, int scStride, int tcStride) = scenario.FormatMarker switch
        {
            0x33 => (0xA8EC, 1551, 354, 298),
            0x5C => (0xABE8, 1651, 363, 307),
            0x66 => (0xAE14, 1851, 372, 316),
            _ => throw new InvalidDataException("Unsupported scenario edition for character records.")
        };
        if (count != expectedCount)
            throw new InvalidDataException($"Character count {count} differs from {expectedCount}.");
        bool simplified = locale switch
        {
            "SC" => true,
            "TC" => false,
            _ => throw new InvalidDataException($"Unsupported S14 locale: {locale}")
        };
        int stride = simplified ? scStride : tcStride;
        int nameBytes = simplified ? 18 : 14;
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        if (offset < 4 || offset + (long)count * stride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset - 4, 4)) != count)
            throw new InvalidDataException("S14 character table boundary or count differs.");

        var names = new List<S14CharacterName>(count);
        for (int slot = 0; slot < count; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(offset + slot * stride, stride);
            string? firstSort = scenario.FormatMarker == 0x66
                ? ReadNameSortSlot(row.Slice(simplified ? 54 : 34, simplified ? 50 : 38)) : null;
            string? secondSort = scenario.FormatMarker == 0x66
                ? ReadNameSortSlot(row.Slice(simplified ? 104 : 72, simplified ? 50 : 38)) : null;
            names.Add(new S14CharacterName(slot,
                ReadUtf16Slot(row[..nameBytes]),
                ReadUtf16Slot(row.Slice(nameBytes, nameBytes)))
            {
                NameSortKey = firstSort is not null && secondSort is not null ? firstSort + secondSort : null,
                SurnameSortKey = firstSort,
                GivenNameSortKey = secondSort
            });
        }
        if (names[0].DisplayName.Length == 0)
            throw new InvalidDataException("S14 character slot zero is unexpectedly empty.");
        return new S14CharacterTable(offset, stride, names);
    }

    public static IReadOnlyList<S14CityDefinition> ReadCityDefinitions(S14Document fixedData)
    {
        int offset = FixedCityOffset(fixedData.FormatMarker);
        ReadOnlySpan<byte> data = fixedData.DecodedPayload;
        if (offset < 4 || offset + CityCount * CityNameStride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset - 4, 4)) != CityCount)
            throw new InvalidDataException("S14 fixed city-name table boundary or count differs.");
        var definitions = new List<S14CityDefinition>(CityCount);
        for (int slot = 0; slot < CityCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(offset + slot * CityNameStride, CityNameStride);
            string name = ReadUtf16Slot(row[..8]);
            if (name.Length == 0)
                throw new InvalidDataException($"S14 city name is empty at slot {slot}.");
            if (row[39] != slot)
                throw new InvalidDataException($"S14 fixed city slot ID differs at {slot}.");
            definitions.Add(new S14CityDefinition(slot, name,
                ReadUtf16Slot(row.Slice(8, 20)), ReadLocationLinks(row.Slice(28, 11), slot),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(40, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(44, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(48, 4)), row[52]));
        }
        return definitions;
    }

    public static IReadOnlyList<S14CityName> ReadCityNames(S14Document fixedData) =>
        ReadCityDefinitions(fixedData).Select(item => new S14CityName(item.Slot, item.Name)).ToArray();

    public static IReadOnlyList<S14GateDefinition> ReadGateDefinitions(S14Document fixedData)
    {
        int header = checked(FixedCityOffset(fixedData.FormatMarker) + CityCount * CityNameStride);
        ReadOnlySpan<byte> data = fixedData.DecodedPayload;
        if (header + 8 + GateCount * GateStride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header, 4)) != 2 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header + 4, 4)) != GateCount)
            throw new InvalidDataException("S14 fixed gate table boundary or count differs.");
        var gates = new List<S14GateDefinition>(GateCount);
        for (int slot = 0; slot < GateCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(header + 8 + slot * GateStride, GateStride);
            if (row[39] != slot)
                throw new InvalidDataException($"S14 fixed gate slot ID differs at {slot}.");
            gates.Add(new S14GateDefinition(slot, slot == 0 ? 0 : 51 + slot,
                ReadUtf16Slot(row[..8]), ReadUtf16Slot(row.Slice(8, 20)),
                ReadLocationLinks(row.Slice(28, 11), 51 + slot)));
        }
        return gates;
    }

    public static IReadOnlyList<S14AreaDefinition> ReadAreaDefinitions(S14Document fixedData)
    {
        int mapCountOffset = FixedMapCountOffset(fixedData.FormatMarker);
        int header = checked(mapCountOffset + 4 + MapCellCount * 3);
        ReadOnlySpan<byte> data = fixedData.DecodedPayload;
        if (header + 8 + AreaCount * AreaStride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(mapCountOffset, 4)) != MapCellCount ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header, 4)) != 6 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header + 4, 4)) != AreaCount)
            throw new InvalidDataException("S14 fixed area table boundary or count differs.");
        var areas = new List<S14AreaDefinition>(AreaCount);
        var occupiedHexes = new HashSet<ushort>();
        for (int slot = 0; slot < AreaCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(header + 8 + slot * AreaStride, AreaStride);
            byte citySlot = row[36];
            ushort hexId = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(37, 2));
            if (citySlot >= CityCount || hexId >= MapCellCount ||
                (citySlot == 0 && hexId != 0) ||
                (citySlot != 0 && !occupiedHexes.Add(hexId)))
                throw new InvalidDataException($"S14 fixed area reference is invalid at {slot}.");
            var neighbors = new ushort[8];
            for (int index = 0; index < neighbors.Length; index++)
                neighbors[index] = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(47 + index * 2,2));
            areas.Add(new S14AreaDefinition(slot, ReadUtf16Slot(row[..8]),
                ReadUtf16Slot(row.Slice(8, 28)), citySlot, hexId,
                Convert.ToHexString(row[39..]))
            {
                RawField44 = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(41, 2)),
                RawField46 = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(43, 2)),
                // Reserved inactive records can contain original uninitialized bytes.
                AdjacentAreaSlots = citySlot == 0 ? [] : ReadAreaLinks(row.Slice(47, 16), slot),
                RawNeighborSlots = neighbors,
                RegionalNeighborMask = row[64],
                FacilityCapacity = row[63]
            });
        }
        foreach (S14AreaDefinition area in areas)
            foreach (int adjacent in area.AdjacentAreaSlots)
                if (areas[adjacent].CitySlot == 0)
                    throw new InvalidDataException($"S14 fixed area {area.Slot} links to inactive area {adjacent}.");
        return areas;
    }

    private static IReadOnlyList<int> ReadAreaLinks(ReadOnlySpan<byte> bytes, int slot)
    {
        var links = new List<int>(8);
        bool terminated = false;
        for (int index = 0; index < 8; index++)
        {
            int adjacent = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index * 2, 2));
            if (adjacent == 0) { terminated = true; continue; }
            if (terminated || adjacent >= AreaCount || adjacent == slot || links.Contains(adjacent))
                throw new InvalidDataException($"S14 fixed area {slot} has invalid adjacent area links.");
            links.Add(adjacent);
        }
        // Original PK/CE contain one directed link without its reverse; preserve source order.
        return links;
    }

    public static void ValidateFixedMapCodes(S14Document fixedData, MapDocument map)
    {
        int countOffset = FixedMapCountOffset(fixedData.FormatMarker);
        ReadOnlySpan<byte> data = fixedData.DecodedPayload;
        if (map.Cells.Length != MapCellCount || countOffset + 4 + MapCellCount * 3 > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(countOffset, 4)) != MapCellCount)
            throw new InvalidDataException("S14 fixed map code table boundary or count differs.");
        int start = countOffset + 4;
        for (int hex = 0; hex < MapCellCount; hex++)
        {
            uint code = (uint)(data[start + hex * 3] |
                data[start + hex * 3 + 1] << 16 |
                data[start + hex * 3 + 2] << 24);
            if (map.Cells[hex].Field1 != code)
                throw new InvalidDataException($"S14 fixed map code differs at hex {hex}.");
        }
    }

    private static int FixedCityOffset(uint marker) => marker switch
    {
        0x33 => 0x3D1,
        0x5C or 0x66 => 0x3E8,
        _ => throw new InvalidDataException("Unsupported fixed-data edition for city names.")
    };

    private static int FixedMapCountOffset(uint marker) => marker switch
    {
        0x33 => 0x10D1,
        0x5C or 0x66 => 0x10E8,
        _ => throw new InvalidDataException("Unsupported fixed-data edition for map codes.")
    };

    private static byte[] ReadLocationLinks(ReadOnlySpan<byte> links, int slot)
    {
        int count = links.IndexOf((byte)0);
        if (count < 0) count = links.Length;
        byte[] used = links[..count].ToArray();
        if (links[count..].IndexOfAnyExcept((byte)0) >= 0 ||
            used.Distinct().Count() != count || used.Any(link => link > 61))
            throw new InvalidDataException($"S14 fixed location links are invalid at {slot}.");
        return used;
    }

    public static IReadOnlyList<S14CharacterPlacement> ReadCharacterPlacements(
        S14Document scenario, string locale)
    {
        S14CharacterTable table = ReadCharacterNames(scenario, locale);
        int stateOffset = locale == "SC" ? 212 : 156;
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        var placements = new List<S14CharacterPlacement>(table.Names.Count);
        for (int slot = 0; slot < table.Names.Count; slot++)
        {
            ReadOnlySpan<byte> state = data.Slice(table.RecordOffset +
                slot * table.RecordStride + stateOffset, 8);
            // Both locations are words even when the installed static scenarios
            // only use low values. Preserve unknown high codes for validation.
            placements.Add(new S14CharacterPlacement(slot, state[0],
                BinaryPrimitives.ReadUInt16LittleEndian(state[1..]),
                BinaryPrimitives.ReadUInt16LittleEndian(state[3..]),
                state[5], Convert.ToHexString(state)));
        }
        return placements;
    }

    public static IReadOnlyList<S14ScenarioCity> ReadScenarioCities(S14Document scenario)
    {
        if (scenario.LeadingRecordCount is not int count || count < 1)
            throw new InvalidDataException("S14 scenario ID table is missing.");
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        int afterIds = checked(8 + count * 2);
        int countOffset = afterIds + 0x1E;
        int recordOffset = afterIds + 0x22;
        int stride = scenario.FormatMarker switch
        {
            0x33 => 22,
            0x5C or 0x66 => 23,
            _ => throw new InvalidDataException("Unsupported scenario edition for city records.")
        };
        if (recordOffset + CityCount * stride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(countOffset, 4)) != CityCount)
            throw new InvalidDataException("S14 scenario city table boundary or count differs.");

        var cities = new List<S14ScenarioCity>(CityCount);
        for (int slot = 0; slot < CityCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(recordOffset + slot * stride, stride);
            cities.Add(new S14ScenarioCity(slot,
                BinaryPrimitives.ReadUInt16LittleEndian(row), row[2],
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(3, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(7, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(11, 4)),
                row[15], BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(16, 2)),
                row[scenario.FormatMarker == 0x33 ? 19 : 20],
                Convert.ToHexString(row[15..]))
            {
                RawValidityFlag69 = scenario.FormatMarker == 0x66 ? row[19] : null,
                RawAutomaticTactics48 = scenario.FormatMarker == 0x66 ? row[18] : null
            });
        }
        return cities;
    }

    public static IReadOnlyList<S14CharacterAbilities> ReadCharacterAbilities(
        S14Document scenario, string locale)
    {
        S14CharacterTable table = ReadCharacterNames(scenario, locale);
        int offset = locale == "SC" ? 222 : 166;
        var abilities = new List<S14CharacterAbilities>(table.Names.Count);
        for (int slot = 0; slot < table.Names.Count; slot++)
        {
            ReadOnlySpan<byte> row = scenario.DecodedPayload.AsSpan(
                table.RecordOffset + slot * table.RecordStride + offset, 5);
            abilities.Add(new S14CharacterAbilities(slot,
                row[0], row[1], row[2], row[3], row[4]));
        }
        return abilities;
    }

    public static IReadOnlyList<S14CharacterIdentity> ReadCharacterIdentities(
        S14Document scenario, string locale)
    {
        S14CharacterTable table = ReadCharacterNames(scenario, locale);
        int offset = locale == "SC" ? 204 : 148;
        var identities = new List<S14CharacterIdentity>(table.Names.Count);
        var ids = new HashSet<ushort>();
        for (int slot = 0; slot < table.Names.Count; slot++)
        {
            ReadOnlySpan<byte> row = scenario.DecodedPayload.AsSpan(
                table.RecordOffset + slot * table.RecordStride + offset, 4);
            ushort appearance = BinaryPrimitives.ReadUInt16LittleEndian(row);
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(row[2..]);
            if (id != 0 && !ids.Add(id))
                throw new InvalidDataException($"S14 character ID {id} occurs more than once.");
            identities.Add(new S14CharacterIdentity(slot, id, appearance));
        }
        return identities;
    }

    public static IReadOnlyList<S14CharacterSex> ReadCharacterSexes(S14Document scenario, string locale)
    {
        S14CharacterTable table = ReadCharacterNames(scenario, locale);
        // PK inserted nine bytes before this field. CE retains the PK offset.
        int tailOffset = scenario.FormatMarker switch
        {
            0x33 => 207,
            0x5C or 0x66 => 216,
            _ => throw new InvalidDataException("Unsupported scenario edition for character sex records.")
        };
        int offset = tailOffset + (locale == "SC" ? 56 : 0);
        var values = new List<S14CharacterSex>(table.Names.Count);
        for (int slot = 0; slot < table.Names.Count; slot++)
            values.Add(new(slot, scenario.DecodedPayload[table.RecordOffset + slot * table.RecordStride + offset]));
        return values;
    }

    public static IReadOnlyList<ushort> ReadCharacterAppearanceYears(S14Document scenario, string locale)
    {
        if (scenario.FormatMarker != 0x66)
            throw new InvalidDataException("Character appearance-year loading requires the verified CE serializer.");
        var table = ReadCharacterNames(scenario, locale);
        int offset = 217 + (locale == "SC" ? 56 : 0);
        return Enumerable.Range(0, table.Names.Count).Select(slot => BinaryPrimitives.ReadUInt16LittleEndian(
            scenario.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride + offset, 2))).ToArray();
    }

    public static IReadOnlyList<(ushort TerminalYear, byte Kind)> ReadCharacterLifespans(S14Document scenario, string locale)
    {
        if (scenario.FormatMarker != 0x66)
            throw new InvalidDataException("Character lifespan loading requires the verified CE serializer.");
        var table = ReadCharacterNames(scenario, locale);
        int offset = 221 + (locale == "SC" ? 56 : 0);
        return Enumerable.Range(0, table.Names.Count).Select(slot =>
        {
            var row = scenario.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride + offset, 3);
            return (BinaryPrimitives.ReadUInt16LittleEndian(row), row[2]);
        }).ToArray();
    }

    public static IReadOnlyList<S14ScenarioForce> ReadScenarioForces(S14Document scenario)
    {
        (int offset, int stride) = scenario.FormatMarker switch
        {
            0x33 => (0x119F, 326),
            0x5C => (0x12A6, 326),
            0x66 => (0x1436, 329),
            _ => throw new InvalidDataException("Unsupported scenario edition for force records.")
        };
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        if (offset + 4 + ForceSlotCount * stride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset - 4, 4)) != 3 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)) != ForceSlotCount)
            throw new InvalidDataException("S14 scenario force table boundary or count differs.");
        var forces = new List<S14ScenarioForce>(ForceSlotCount);
        for (int slot = 0; slot < ForceSlotCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(offset + 4 + slot * stride, stride);
            forces.Add(new S14ScenarioForce(slot,
                BinaryPrimitives.ReadUInt16LittleEndian(row), Convert.ToHexString(row[2..]))
            {
                RawReportColor49 = scenario.FormatMarker == 0x66 ? row[56] : null,
                RawKingdomName48 = scenario.FormatMarker == 0x66 ? row[55] : null,
                RawIndexedBytes13 = scenario.FormatMarker == 0x66 ? row.Slice(2, ForceSlotCount).ToArray() : null,
                RawIndexedBytesEA = scenario.FormatMarker == 0x66 ? row.Slice(206, ForceSlotCount).ToArray() : null
            });
        }
        return forces;
    }

    public static IReadOnlyList<S14ScenarioGate> ReadScenarioGates(S14Document scenario)
    {
        if (scenario.LeadingRecordCount is not int count || count < 1)
            throw new InvalidDataException("S14 scenario ID table is missing.");
        int cityStride = scenario.FormatMarker == 0x33 ? 22 : 23;
        int stride = scenario.FormatMarker switch
        {
            0x33 => 19,
            0x5C or 0x66 => 20,
            _ => throw new InvalidDataException("Unsupported scenario edition for gate records.")
        };
        int header = checked(8 + count * 2 + 0x22 + CityCount * cityStride);
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        if (header + 8 + GateCount * stride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header, 4)) != 2 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header + 4, 4)) != GateCount)
            throw new InvalidDataException("S14 scenario gate table boundary or count differs.");
        var gates = new List<S14ScenarioGate>(GateCount);
        for (int slot = 0; slot < GateCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(header + 8 + slot * stride, stride);
            gates.Add(new S14ScenarioGate(slot, BinaryPrimitives.ReadUInt16LittleEndian(row),
                row[2], BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(3, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(7, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(11, 4)),
                Convert.ToHexString(row[15..]))
            {
                RawValidityFlag69 = scenario.FormatMarker == 0x66 ? row[19] : null,
                RawAutomaticTactics48 = scenario.FormatMarker == 0x66 ? row[18] : null,
                RawMorale44 = scenario.FormatMarker == 0x66 ? row[15] : null,
                RawField46 = scenario.FormatMarker == 0x66 ? BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(16, 2)) : null
            });
        }
        return gates;
    }

    public static IReadOnlyList<S14ScenarioDistrict> ReadScenarioDistricts(S14Document scenario)
    {
        int offset = ScenarioDistrictCountOffset(scenario.FormatMarker);
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        if (offset + 4 + ForceSlotCount * 4 > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset - 4, 4)) != 4 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)) != ForceSlotCount)
            throw new InvalidDataException("S14 scenario district table boundary or count differs.");
        var districts = new List<S14ScenarioDistrict>(ForceSlotCount);
        for (int slot = 0; slot < ForceSlotCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(offset + 4 + slot * 4, 4);
            if (row[0] >= ForceSlotCount)
                throw new InvalidDataException($"S14 district force reference is invalid at {slot}.");
            districts.Add(new S14ScenarioDistrict(slot, row[0], row[1],
                BinaryPrimitives.ReadUInt16LittleEndian(row[2..])));
        }
        return districts;
    }

    public static IReadOnlyList<S14ScenarioArea> ReadScenarioAreas(S14Document scenario)
    {
        int header = ScenarioDistrictCountOffset(scenario.FormatMarker) + 4 + ForceSlotCount * 4;
        int stride = scenario.FormatMarker == 0x33 ? 7 : 8;
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        if (header + 16 + AreaCount * stride > data.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header, 4)) != 5 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header + 4, 4)) != MapCellCount ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header + 8, 4)) != 6 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(header + 12, 4)) != AreaCount)
            throw new InvalidDataException("S14 scenario area table boundary or count differs.");
        var areas = new List<S14ScenarioArea>(AreaCount);
        for (int slot = 0; slot < AreaCount; slot++)
        {
            ReadOnlySpan<byte> row = data.Slice(header + 16 + slot * stride, stride);
            if (row[3] >= ForceSlotCount)
                throw new InvalidDataException($"S14 area force reference is invalid at {slot}.");
            areas.Add(new S14ScenarioArea(slot, BinaryPrimitives.ReadUInt16LittleEndian(row),
                row[2], row[3], Convert.ToHexString(row[4..]))
            {
                RawFlags38 = row[4],
                RawFlag74 = scenario.FormatMarker is 0x5C or 0x66 ? row[7] : null
            });
        }
        return areas;
    }

    // Use only after ReadScenarioAreas has validated this source layout.
    internal static int ScenarioAreaEndOffset(S14Document scenario) => checked(
        ScenarioDistrictCountOffset(scenario.FormatMarker) + 4 + ForceSlotCount * 4 + 16 +
        AreaCount * (scenario.FormatMarker == 0x33 ? 7 : 8));

    private static int ScenarioDistrictCountOffset(uint marker) => marker switch
    {
        0x33 => 0x53DF,
        0x5C => 0x54E6,
        0x66 => 0x5712,
        _ => throw new InvalidDataException("Unsupported scenario edition for district records.")
    };

    private static string ReadNameSortSlot(ReadOnlySpan<byte> bytes)
    {
        var units = new char[bytes.Length / 2];
        int count = 0;
        for (; count < units.Length; count++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(count * 2, 2));
            if (value == 0) break;
            units[count] = (char)value;
        }
        return new string(units, 0, count);
    }

    private static string ReadUtf16Slot(ReadOnlySpan<byte> bytes)
    {
        int end = 0;
        while (end + 1 < bytes.Length && (bytes[end] != 0 || bytes[end + 1] != 0))
            end += 2;
        return Encoding.Unicode.GetString(bytes[..end]);
    }
}
