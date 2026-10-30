using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Voice event banks and the original CABE6F83 selector dispatch.
/// Channel1 and Army voices retain their distinct request selector keys.</summary>
internal static class PersonVoiceResourceBinding
{
    internal static AudioCueBank Bank(NarrationVoiceLanguage language, int voiceEvent)
    {
        if (voiceEvent is < 1 or > 46) throw new InvalidDataException("person.voice.event");
        bool japanese = language switch
        {
            NarrationVoiceLanguage.Japanese => true,
            NarrationVoiceLanguage.Chinese => false,
            _ => throw new InvalidDataException("person.voice.language")
        };
        return voiceEvent switch
        {
            >= 14 and <= 23 => new(japanese ? 0xD82DE5D1u : 0xE63226A7u, japanese ? 0xC5444BA4u : 0xD3488C7Au),
            >= 28 and <= 31 => new(japanese ? 0xC8FEED30u : 0xD7032E06u, japanese ? 0xB6155303u : 0xC41993D9u),
            10 or 12 or 13 or >= 32 => NarrationVoiceSelection.Bank(language),
            _ => new(japanese ? 0x7572622Eu : 0x68957B84u, null)
        };
    }

    internal static AudioNamedCue ResolveCue(AudioSelectionBank bank, uint sourceKey, bool spatial = false)
    {
        ArgumentNullException.ThrowIfNull(bank);
        var bytes = bank.Source.Bytes;
        uint? target = null;
        foreach (var section in bank.Sections)
        {
            if (section.TypeId != 0xCABE6F83) continue;
            int at = section.Offset;
            if (section.Bytes < 0x28 || at < 0 || at > bytes.Length - section.Bytes)
                throw new InvalidDataException("person.voice.selector_section");
            uint key = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 8, 4));
            if (key != sourceKey) continue;
            if (target is not null) throw new InvalidDataException("person.voice.selector_identity");
            //404680 supplies0x10552 for channel1 and0x10571 for a real spatial
            //request.12262D8 matches each12-byte selector entry, falling back
            //to words4..6 only when no entry has that actual request key.
            uint U32(int field) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(field, 4));
            int selected = at + 0x10;
            uint count = U32(at + 0x1C), relative = U32(at + 0x20);
            if (relative > section.Bytes || count > (section.Bytes - relative) / 12)
                throw new InvalidDataException("person.voice.selector_entries");
            for (int index = 0; index < count; index++)
            {
                int entry = checked(at + (int)relative + index * 12);
                if (U32(entry) == (spatial ? 0x10571u : 0x10552u)) { selected = entry; break; }
            }
            target = U32(selected + 4);
            if (U32(selected + 8) != 0)
                throw new NotSupportedException("person.voice.selector_subentry");
        }
        uint cueKey = target ?? sourceKey;
        var cues = bank.Cues.Where(cue => cue.CueId == cueKey).Take(2).ToArray();
        if (cues.Length != 1 || cues[0].Name is not { Length: > 0 })
            throw new InvalidDataException("person.voice.registered_cue");
        return cues[0];
    }

    internal static AudioSampleStorage Storage(AudioSelectionBank bank, AudioNamedCue cue)
    {
        var bytes = bank.Source.Bytes;
        int start = cue.SectionOffset, end = checked(start + cue.SectionBytes);
        uint U32(int relative)
        {
            if (relative < 0 || relative > cue.SectionBytes - 4 || end > bytes.Length)
                throw new InvalidDataException("person.voice.cue_graph");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(start + relative, 4));
        }
        if (U32(0x20) != 1) throw new NotSupportedException("person.voice.cue_roots");
        uint table = U32(0x24);
        if (table > int.MaxValue) throw new InvalidDataException("person.voice.cue_root_table");
        uint root = U32((int)table);
        if (root > int.MaxValue) throw new InvalidDataException("person.voice.cue_root");
        return U32((int)root) switch
        {
            0x562FC4B9 => AudioSampleStorage.Internal,
            0x51D25082 => AudioSampleStorage.External,
            _ => throw new NotSupportedException("person.voice.cue_root_kind")
        };
    }
}
