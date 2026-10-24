using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record PersonnelHireNameDefinition(int Id, string Text, string SortKey, byte Flags);
internal sealed record PersonnelHireNameCatalog(IReadOnlyList<PersonnelHireNameDefinition> Names, SourceProvenance Source);
internal sealed record PersonnelHireGeneratedName(GameState State, string Surname, string GivenName,
    int SurnameId, int GivenNameId, string SurnameSortKey, string GivenNameSortKey);

/// <summary>The fixed make-name resource and the two independent4C9E10
/// selections used by334EF0. It caches definitions, never generated names.</summary>
internal static class PersonnelHireNameSource
{
    private const int Count = 400;
    private const int Stride = 13;
    //Section19's existing CGem serializer is21 bytes for31 receivers.
    private const int Start = S14GemReader.RecordStart + S14GemReader.Count * S14GemReader.RecordStride + 8;

    internal static PersonnelHireNameCatalog Read(S14Document document, SourceProvenance source)
    {
        ReadOnlySpan<byte> bytes = document.DecodedPayload;
        int end = Start + Count * Stride;
        if (document.FormatMarker != 0x66 || bytes.Length < end + 8 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x66 ||
            Word(Start - 8) != 20 || Word(Start - 4) != Count || Word(end) != 21 || Word(end + 4) != 2001)
            throw Missing("fixed_section20_format");
        var names = new PersonnelHireNameDefinition[Count];
        for (int id = 0; id < Count; id++)
        {
            var row = bytes.Slice(Start + id * Stride, Stride);
            //23EB10 copies exactly2 UTF16 units at10,4 units at14, then
            //the selection flags byte1C. No text normalization is performed.
            names[id] = new(id, S14ForceNameDefinitionReader.ReadLiteralUnits(row[..4]),
                S14ForceNameDefinitionReader.ReadLiteralUnits(row.Slice(4, 8)), row[12]);
        }
        return new(Array.AsReadOnly(names), source);

        int Word(int at) => BinaryPrimitives.ReadInt32LittleEndian(document.DecodedPayload.AsSpan(at, 4));
    }

    internal static PersonnelHireGeneratedName Generate(GameState state, SourceWorldContext current,
        PersonnelHireNameCatalog catalog, int sexType, IReadOnlySet<(int, int)> forbiddenPairs)
    {
        if (sexType is not (3 or 4)) throw Missing("sex_type");
        ArgumentNullException.ThrowIfNull(forbiddenPairs);
        var first = Select(state, current, catalog, sexType, 0);
        var working = first.State;
        PersonnelHireNameDefinition second;
        do
        {
            var selected = Select(working, current, catalog, sexType, 1);
            working = selected.State;
            second = selected.Name;
        } while (forbiddenPairs.Contains((first.Name.Id, second.Id)));
        //334EF0 calls33EEF0/308920 after combining the name. The cached TC
        //308920 returns1 unconditionally (14CC30 is only a global getter).
        //There is no world-name lookup or additional full-name random retry.
        return new(working, first.Name.Text, second.Text, first.Name.Id, second.Id,
            first.Name.SortKey, second.SortKey);
    }

    private static (GameState State, PersonnelHireNameDefinition Name) Select(GameState state,
        SourceWorldContext current, PersonnelHireNameCatalog catalog, int type, int part)
    {
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("temporary_list");
        var candidates = new List<PersonnelHireNameDefinition>();
        try
        {
            //4C9E10 visits every fixed receiver in native table order, without
            //adding a native-validity, nonempty-name or character-length gate.
            foreach (var name in catalog.Names)
            {
                if ((name.Flags & (1 << type)) == 0 || (name.Flags & (1 << part)) == 0) continue;
                if (current.AppendPersonnelHireNameTemporary(temporary, name)) candidates.Add(name);
            }
            if (candidates.Count == 0) throw Missing("selected_name_pointer");
            //934EE0 is a clock getter; its ignored result does not seed or
            //advance CRT rand. A singleton does not consume141027BF0.
            int index = 0;
            if (candidates.Count > 1)
            {
                var saved = state.CampaignCrtRandom ?? throw Missing("current_CRT_random_state");
                uint next = unchecked(saved.State * 0x343FDu + 0x269EC3u);
                int value = (int)((next >> 16) & 0x7FFFu);
                state = state with { CampaignCrtRandom = new(next) };
                index = value % candidates.Count;
            }
            return (state, candidates[index]);
        }
        finally
        {
            current.ClearTemporaryHexList(temporary);
            current.ReleaseTemporaryHexList(temporary);
        }
    }

    private static InvalidDataException Missing(string field) => new("personnel.hire.name." + field);
}

public sealed partial class GameResourceRepository
{
    internal PersonnelHireNameCatalog LoadPersonnelHireNames(string relativePath)
    {
        var (bytes, source) = ReadSource(relativePath);
        try { return PersonnelHireNameSource.Read(S14Reader.Read(bytes, false), source); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original personnel names failed to load: {source.ResourceUri} ({source.PhysicalPath}). {error.Message}", error);
        }
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private PersonnelHireNameCatalog? _personnelHireNames;

    internal PersonnelHireGeneratedName GeneratePersonnelHireNameCurrent(GameState state, SourceWorldContext current,
        int sexType, IReadOnlySet<(int, int)> forbiddenPairs)
    {
        _personnelHireNames ??= _repository.LoadPersonnelHireNames(_scenario.FixedDataSource.SourcePath);
        return PersonnelHireNameSource.Generate(state, current, _personnelHireNames, sexType, forbiddenPairs);
    }
}

public sealed partial class SourceWorldContext
{
    internal bool AppendPersonnelHireNameTemporary(HexListHandle handle, PersonnelHireNameDefinition name)
    {
        if (!handle.Identity.HasValue) return false;
        var node = AllocateHexListNode(handle);
        if (node.Identity is not int identity) return false;
        WriteCurrentListPayload(identity, name);
        return true;
    }
}
