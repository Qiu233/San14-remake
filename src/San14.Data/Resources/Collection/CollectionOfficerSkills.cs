using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Runs the existing person post-load kernel against the title's
/// resource definitions. This owns only one person's skill arrays and a small
/// temporary list; no campaign, map, command state or World is constructed.</summary>
internal sealed class CollectionOfficerSkills : IPersonSkillPostLoadState, IPersonalityNormalizationState
{
    private readonly byte[][] traits;
    private readonly S14FormationCatalog combat;
    private readonly S14GemCatalog gems;
    private readonly List<PersonalityReference> temporary = [];
    private ushort personId;
    private ushort[] personality = [];
    private byte[] tactics = [];
    private uint formations;
    private ushort mask;

    internal CollectionOfficerSkills(S14PersonalityCatalog traits, S14FormationCatalog combat,
        S14GemCatalog gems)
    {
        this.traits = traits.Rows.Select(row => row.RawBytes.ToArray()).ToArray();
        this.combat = combat; this.gems = gems;
        PersonalityNormalization.ApplyManagerTable(this);
    }

    internal (ushort[] Traits, byte[] Tactics, uint Formations) Project(S14PersonSkillReference source)
    {
        personId = source.CharacterId; personality = source.RawPersonalitySlots150.ToArray();
        tactics = source.RawTacticSlots168.ToArray(); formations = source.RawFormationMask164;
        mask = 0; temporary.Clear();
        _ = PersonSkillPostLoad.Apply(0, this);
        var selected = new ushort[5];
        for (int slot = 0; slot < selected.Length; slot++)
        {
            int requested = personality[slot];
            var reference = new PersonalityReference(requested < traits.Length ? requested : 0);
            if (ReadSkillPersonalityValidity(reference) == true &&
                (personId is >= 5101 and <= 5200 || (ReadSkillPersonalityFlags(reference)!.Value & 0x10) != 0))
                selected[slot] = (ushort)requested;
        }
        return (selected, tactics.ToArray(), formations);
    }

    internal string TraitName(int id) => Text(traits[id].AsSpan(0, 12));
    internal bool TraitBefore(int left, int right, bool natural)
    {
        bool Valid(int id) => id > 0 && id < traits.Length && ReadSkillPersonalityValidity(new(id)) == true;
        if (!Valid(left) || !Valid(right)) return !Valid(right);
        string a = Text(traits[left].AsSpan(12, 32)), b = Text(traits[right].AsSpan(12, 32));
        bool before = a.Length != 0 && b.Length != 0 ? PersonNameOrder.Compare(a, b) < 0 :
            a.Length == 0 && b.Length == 0 ? PersonNameOrder.Compare(TraitName(left), TraitName(right)) < 0 : b.Length == 0;
        return natural ? before : !before;
    }
    internal string TraitDescription(int id) => Text(traits[id].AsSpan(44, 102));
    internal byte TraitStyle(int id) => traits[id][158];
    internal bool TacticAvailable(int id) => id is > 0 and < 201 &&
        ((combat.Tactics[id].RawBytes[51] & 2) != 0 ||
         gems.Gems.Any(gem => gem.Slot != 0 && gem.RawField53 != 0 && gem.RawField52 == id));
    private static string Text(ReadOnlySpan<byte> bytes) =>
        new System.Text.UnicodeEncoding(false, false, true).GetString(bytes).Split('\0')[0];

    public ushort? ReadPersonIdentity(int receiver) => personId;
    public uint? ReadPersonFormationMask164(int receiver) => formations;
    public void WritePersonFormationMask164(int receiver, uint value) => formations = value;
    public ushort? ReadPersonTraitSlot150(int receiver, int index) => personality[index];
    public void WritePersonTraitSlot150(int receiver, int index, ushort value) => personality[index] = value;
    public ushort? ReadPersonTraitMask1BC(int receiver) => mask;
    public void WritePersonTraitMask1BC(int receiver, ushort value) => mask = value;
    public void WritePersonTraitExtra1BE(int receiver, int index, ushort value) { }
    public byte? ReadPersonTacticSlot168(int receiver, int index) => tactics[index];
    public void WritePersonTacticSlot168(int receiver, int index, byte value) => tactics[index] = value;
    public PersonalityReference? ReadPersonalityTableReference(int slot) => new(slot);
    public bool? ReadSkillPersonalityValidity(PersonalityReference reference) => reference.PhysicalSlot is int id &&
        id > 0 && id < traits.Length && (id < 256 || ReadPersonalityByte(reference, 0xB6) != 0);
    public byte? ReadSkillPersonalityFlags(PersonalityReference reference) => ReadPersonalityByte(reference, 0xBF);
    public TacticReference? ReadSkillTacticReference(int slot) => new(slot);
    public bool? ReadSkillTacticValidity(TacticReference reference) => reference.PhysicalSlot is > 0 and < 201;
    public byte? ReadSkillTacticFlags(TacticReference reference) => combat.Tactics[reference.PhysicalSlot!.Value].RawBytes[51];
    public int? ReadSkillTacticPhysicalSlot(TacticReference reference) => reference.PhysicalSlot;
    public GemReference? ReadSkillGemReference(int slot) => new(slot);
    public bool? ReadSkillGemValidity(GemReference reference) => reference.PhysicalSlot is int slot && slot > 0 &&
        slot < gems.Gems.Count && gems.Gems[slot].RawField53 != 0;
    public byte? ReadSkillGemTactic52(GemReference reference) => gems.Gems[reference.PhysicalSlot!.Value].RawField52;
    public HexListHandle? CreateTemporaryHexList() { temporary.Clear(); return new(1); }
    public HexListNodeReference? ReadSkillTemporaryHead(HexListHandle handle) => new(temporary.Count == 0 ? null : 0);
    public PersonalityReference? ReadSkillTemporaryPayload(HexListNodeReference node) => temporary[node.Identity!.Value];
    public HexListNodeReference? ReadHexListNodeNext(HexListNodeReference node) =>
        new(node.Identity!.Value + 1 < temporary.Count ? node.Identity.Value + 1 : null);
    public bool AppendSkillTemporaryPersonality(HexListHandle handle, PersonalityReference reference)
    { temporary.Add(reference); return true; }
    public bool? ReadRegistryLockEnabled() => false; // This catalog owns its local list, not the global manager.
    public void EnterRegistryLock() { }
    public void ExitRegistryLock() { }

    public int? ReadPersonalityPhysicalSlot(PersonalityReference personality) => personality.PhysicalSlot;
    private static int SourceField(int offset) => S14PersonalityReader.FixedFields
        .Where(field => offset >= field.ObjectOffset && offset < field.ObjectOffset + field.Bytes)
        .Select(field => field.SourceOffset + offset - field.ObjectOffset).Single();
    public byte? ReadPersonalityByte(PersonalityReference personality, int offset) =>
        traits[personality.PhysicalSlot!.Value][SourceField(offset)];
    public void WritePersonalityByte(PersonalityReference personality, int offset, byte value) =>
        traits[personality.PhysicalSlot!.Value][SourceField(offset)] = value;
    public void WritePersonalityWord(PersonalityReference personality, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(traits[personality.PhysicalSlot!.Value].AsSpan(SourceField(offset), 2), value);
}
