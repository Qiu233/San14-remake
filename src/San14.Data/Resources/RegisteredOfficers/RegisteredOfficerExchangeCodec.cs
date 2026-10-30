using System.Collections.Immutable;
using System.Text;
using San14.Core;

namespace San14.Data;

/// <summary>The original portable .s14tc record. This is separate from the
/// application library save format and does not adopt a record into a slot.</summary>
public static class RegisteredOfficerExchangeCodec
{
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("SAN14EDITPERSON\0");

    public static byte[] Write(RegisteredOfficer officer, string locale)
    {
        CheckLocale(locale);
        using var stream = new MemoryStream(1137);
        using var writer = new BinaryWriter(stream, Encoding.Unicode, true);
        writer.Write(Signature);
        writer.Write(1u);
        writer.Write(0x00040000u); // Portable id zero, record version four.
        WriteText(writer, officer.FamilyName, 3);
        WriteText(writer, officer.GivenName, 3);
        WriteText(writer, officer.CourtesyName, 3);
        writer.Write(checked((ushort)officer.PortraitId));
        writer.Write(officer.Sex);
        writer.Write(officer.BirthYear);
        writer.Write(officer.DeathYear);
        writer.Write(KeepHistorical(officer.FamilyId));
        writer.Write(KeepHistorical(officer.FatherId));
        writer.Write(KeepHistorical(officer.MotherId));
        writer.Write((ushort)0); // Spouse and sworn group are not portable.
        writer.Write((ushort)0);
        writer.Write(KeepHistorical(officer.CompatibilityPersonId));
        writer.Write(officer.Compatibility);
        WriteSlots(writer, officer.LikePersonIds, 8, historical: true);
        WriteSlots(writer, officer.DislikePersonIds, 8, historical: true);
        writer.Write(officer.Leadership); writer.Write(officer.Strength);
        writer.Write(officer.Intelligence); writer.Write(officer.Politics); writer.Write(officer.Charm);
        WriteSlots(writer, officer.TraitIds, 9);
        uint formations = 0;
        foreach (int id in officer.FormationIds)
        {
            if (id is < 0 or > 20) throw new InvalidDataException("Invalid portable formation.");
            formations |= 1u << id;
        }
        writer.Write(formations);
        if (officer.TacticIds.IsDefault || officer.TacticIds.Length != 10)
            throw new InvalidDataException("Portable tactic slots must contain ten entries.");
        foreach (int id in officer.TacticIds) writer.Write(checked((byte)id));
        writer.Write(officer.DoctrineId); writer.Write(officer.PolicyId);
        writer.Write(officer.PolicyBaseCost); writer.Write(officer.VoiceType);
        WriteText(writer, officer.Description, 501);
        return stream.ToArray();
    }

    /// <summary>Reads wire values before the original import normalization.
    /// The caller must normalize them with captured source people before use.</summary>
    public static RegisteredOfficer Read(ReadOnlySpan<byte> data, string locale)
    {
        CheckLocale(locale);
        if (data.Length < 24 || !data[..16].SequenceEqual(Signature))
            throw new InvalidDataException("Invalid registered-officer portable signature.");
        using var stream = new MemoryStream(data.ToArray(), false);
        using var reader = new BinaryReader(stream, Encoding.Unicode, true);
        stream.Position = 20; // Original loader compares signature, not the header uint.
        uint identity = reader.ReadUInt32();
        int version = (int)(identity >> 16);
        if (version > 4) throw new InvalidDataException($"Unsupported portable officer version {version}.");
        int length = version switch { <= 1 => 624, 2 => 628, 3 => 1128, _ => 1137 };
        if (data.Length < length) throw new InvalidDataException("Truncated registered-officer portable record.");
        string family = ReadName(reader, version), given = ReadName(reader, version), courtesy = ReadName(reader, version);
        int portrait = reader.ReadUInt16(); byte sex = reader.ReadByte();
        ushort birth = reader.ReadUInt16(), death = reader.ReadUInt16();
        ushort familyId = reader.ReadUInt16(), father = reader.ReadUInt16(), mother = reader.ReadUInt16();
        ushort spouse = reader.ReadUInt16(), sworn = reader.ReadUInt16(), compatibilityPerson = reader.ReadUInt16();
        byte compatibility = reader.ReadByte();
        var likes = ReadSlots(reader, 8);
        var dislikes = ReadSlots(reader, 8);
        byte leadership = reader.ReadByte(), strength = reader.ReadByte(), intelligence = reader.ReadByte(),
            politics = reader.ReadByte(), charm = reader.ReadByte();
        var traits = ImmutableArray.CreateBuilder<int>(9);
        for (int i = 0; i < 9; i++) traits.Add(version < 4 ? reader.ReadByte() : reader.ReadUInt16());
        uint formationMask = reader.ReadUInt32();
        var tactics = ImmutableArray.CreateBuilder<int>(10);
        for (int i = 0; i < 10; i++) tactics.Add(reader.ReadByte());
        byte doctrine = reader.ReadByte(), policy = reader.ReadByte(), cost = reader.ReadByte(), voice = reader.ReadByte();
        string description = version < 2 ? ReadLegacy(reader, 501, 500) : ReadText(reader, version == 2 ? 251 : 501);
        return new(0)
        {
            FamilyName = family, GivenName = given, CourtesyName = courtesy,
            PortraitId = portrait, Sex = sex, BirthYear = birth, DeathYear = death,
            FamilyId = familyId, FatherId = father, MotherId = mother, SpouseId = spouse,
            SwornBrotherId = sworn, CompatibilityPersonId = compatibilityPerson, Compatibility = compatibility,
            LikePersonIds = likes, DislikePersonIds = dislikes,
            Leadership = leadership, Strength = strength, Intelligence = intelligence, Politics = politics, Charm = charm,
            TraitIds = traits.MoveToImmutable(), FormationIds = [.. Enumerable.Range(0, 21).Where(id => (formationMask & (1u << id)) != 0)],
            TacticIds = tactics.MoveToImmutable(), DoctrineId = doctrine, PolicyId = policy,
            PolicyBaseCost = cost, VoiceType = voice, Description = description
        };
    }

    private static void CheckLocale(string locale)
    {
        if (locale is not ("SC" or "TC")) throw new ArgumentOutOfRangeException(nameof(locale));
    }
    private static ushort KeepHistorical(int id) => id is >= 1 and <= 1400 ? (ushort)id : (ushort)0;
    private static void WriteSlots(BinaryWriter writer, ImmutableArray<int> slots, int count, bool historical = false)
    {
        if (slots.IsDefault || slots.Length != count) throw new InvalidDataException("Invalid portable officer slot count.");
        foreach (int id in slots) writer.Write(historical ? KeepHistorical(id) : checked((ushort)id));
    }
    private static ImmutableArray<int> ReadSlots(BinaryReader reader, int count)
    {
        var values = ImmutableArray.CreateBuilder<int>(count);
        for (int i = 0; i < count; i++) values.Add(reader.ReadUInt16());
        return values.MoveToImmutable();
    }
    private static void WriteText(BinaryWriter writer, string text, int units)
    {
        if (text is null || text.Length >= units || text.Contains('\0'))
            throw new InvalidDataException("Portable officer text exceeds its fixed window.");
        writer.Write(Encoding.Unicode.GetBytes(text));
        writer.Write(new byte[(units - text.Length) * 2]);
    }
    private static string ReadName(BinaryReader reader, int version) =>
        version < 2 ? ReadLegacy(reader, 5, 2) : ReadText(reader, 3);
    private static string ReadText(BinaryReader reader, int units)
    {
        string text = Encoding.Unicode.GetString(reader.ReadBytes(units * 2));
        int end = text.IndexOf('\0');
        return end >= 0 ? text[..end] : text;
    }
    private static string ReadLegacy(BinaryReader reader, int bytes, int maximumUnits)
    {
        byte[] buffer = reader.ReadBytes(bytes);
        int end = Array.IndexOf(buffer, (byte)0);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // The CE reader's conversion helper explicitly uses code page 950,
        // independently of the selected message-resource language.
        string text = Encoding.GetEncoding(950).GetString(buffer, 0, end < 0 ? buffer.Length : end);
        return text.Length <= maximumUnits ? text : ""; // Original bounded name/string copy rejects overflow.
    }
}
