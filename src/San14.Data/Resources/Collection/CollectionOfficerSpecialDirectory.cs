using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record CollectionSpecialOfficerDefinition(int Id, ushort Appearance, S14CharacterName Name,
    S14CharacterDialogue Dialogue, byte Sex, byte Status, byte DistrictSlot, ushort FirstLocation,
    short DeathYear, PersonRelationFields Relations, int[] Abilities, ushort[] Traits, byte[] Tactics,
    uint Formations, byte Doctrine, byte Policy, byte PolicyCost, short[] Growth);
internal sealed record LoadedCollectionSpecialOfficer(int Slot, int FilenameKey, int AppId,
    CollectionSpecialOfficerDefinition? Person, SourceProvenance? Source, bool? Subscribed);

/// <summary>The normal 846-byte DLC definition format. It has its own stream
/// cipher and no S14 scenario header. Filename keys never replace identities.</summary>
internal static class CollectionSpecialOfficerReader
{
    internal static readonly int[] AppIds =
    [
        1213611,1213611,1213612,1213612,1213611,1213611,1213612,1213612,
        1258150,1258150,1258150,1258150,1258151,1258151,1258151,1258151,
        1258152,1258152,1258152,1258152,1258152,1258152,1258152,1258152,
        0,0,0,0,0,0,1198835,1198835,1198835,1198835,1198835,1198835,
        1258155,1431486,1431486,1431486,1431486,0,
        1543950,1543950,1543950,1543950,1543950,1543950,1543950,
        1638310,1638310,1638310,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    ];

    internal static CollectionSpecialOfficerDefinition Read(byte[] encrypted, int filenameKey, int appId, int slot)
    {
        if (encrypted.Length != 0x34E) throw new InvalidDataException("collection.officer.special_person_payload_length");
        byte[] bytes = new byte[encrypted.Length];
        uint state = unchecked((uint)(filenameKey + appId));
        for (int index = 0; index < bytes.Length; index++)
        {
            state = unchecked(state * 0x6C078965u + 0x3039u);
            bytes[index] = (byte)(encrypted[index] ^ (byte)(state >> 16) ^ (byte)(state >> 24));
        }
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        string Text(int offset, int cap)
        {
            // The native copy cap includes its final NUL. Read the actual
            // source starts; family and given starts are only eight bytes apart.
            int count = 0;
            while (count < cap - 1 && Word(offset + count * 2) != 0) count++;
            return new UnicodeEncoding(false, false, true).GetString(bytes, offset, count * 2);
        }
        int id = Word(0); string given = Text(0x230, 7), courtesy = Text(0x238, 3), reading = Text(0x312, 19);
        var name = new S14CharacterName(slot, Text(0x228, 7), given)
            { NameSortKey = reading, SurnameSortKey = reading, GivenNameSortKey = "" };
        var dialogue = new S14CharacterDialogue(bytes[0x1EC], Word(0x60), bytes[0x78], given, courtesy)
            { VoiceType = bytes[0x1E4], SpecialVoiceType = bytes[0x1E8] };
        ushort[] References(int start, int count) => Enumerable.Range(0, count).Select(i => Word(start + i * 4)).ToArray();
        uint formations = 0;
        for (int bit = 1; bit <= 20; bit++) if (Int(0x160 + (bit - 1) * 4) != 0) formations |= 1u << bit;
        return new(id, Word(0x50), name, dialogue, bytes[0x54], bytes[0xD4], bytes[0xC8], Word(0xCC), unchecked((short)Word(0x64)),
            new(Word(0x6C), Word(0x70), Word(0x74), Word(0x7C), Word(0x80), bytes[0x84], References(0x88, 8), References(0xA8, 8)),
            Enumerable.Range(0, 5).Select(i => (int)bytes[0xE8 + i * 4]).ToArray(), References(0x124, 9),
            Enumerable.Range(0, 10).Select(i => bytes[0x1B0 + i * 4]).ToArray(), formations,
            bytes[0x1D8], bytes[0x1DC], bytes[0x1E0],
            Enumerable.Range(0, 12).Select(i => unchecked((short)Int(0x1F8 + i * 4))).ToArray());
    }
}

public sealed partial class GameResourceRepository
{
    internal IReadOnlyList<LoadedCollectionSpecialOfficer> ReadCollectionSpecialOfficerDirectory(CollectionOfficerAdmission? admission)
    {
        var rows = new List<LoadedCollectionSpecialOfficer>(100);
        for (int slot = 0; slot < 100; slot++)
        {
            int key = 5101 + slot, appId = CollectionSpecialOfficerReader.AppIds[slot];
            bool? subscribed = null;
            if (admission is not null && admission.SubscribedDlcAppIds.TryGetValue(appId, out var known)) subscribed = known;
            string relative = $"DLC/0011/gd_{key}.s14";
            if (appId is 0 or 1258155 || subscribed == false)
            { rows.Add(new(slot, key, appId, null, null, subscribed)); continue; }
            //A genuinely missing payload leaves the constructed receiver empty.
            //Unreadable present input must retain its actual IO failure.
            byte[] bytes; SourceProvenance source;
            try
            {
                _=File.GetAttributes(_overrides.Find(GameResourceId.File(relative)) ?? SourcePath(relative));
                (bytes,source)=ReadSource(relative);
            }
            catch(FileNotFoundException) { rows.Add(new(slot,key,appId,null,null,subscribed)); continue; }
            catch(DirectoryNotFoundException) { rows.Add(new(slot,key,appId,null,null,subscribed)); continue; }
            rows.Add(new(slot, key, appId, CollectionSpecialOfficerReader.Read(bytes, key, appId, slot), source, subscribed));
        }
        return rows.AsReadOnly();
    }
}
