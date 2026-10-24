using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Normal scenario Gem discovery state: five Area words followed by
/// five remaining-charge bytes. Fixed Gem definitions do not supply these.</summary>
internal static class TalentGemDiscoveryInputs
{
    internal static TalentGemDiscoveryState LoadFresh(LoadedScenario scenario)
    {
        var world = scenario.Snapshot.WorldParameters ?? throw Missing("scenario_World");
        var tables = world.SourceBoundary?.StateTables ?? throw Missing("scenario_forward_boundary");
        var selected = tables.Where(table => table.Section == 19).ToArray();
        if (selected.Length != 1 || selected[0].RawCount != 31 || selected[0].RecordBytes != 15 ||
            selected[0].RawBytes is not { Length: 465 } bytes)
            throw Missing("scenario_section19_31x15");
        var gems = new Dictionary<int, GemDiscoverySite[]>(31);
        for (int slot = 0; slot < 31; slot++)
        {
            var sites = new GemDiscoverySite[5];
            var row = bytes.AsSpan(slot * 15, 15);
            for (int index = 0; index < 5; index++)
                sites[index] = new(BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(index * 2, 2)),
                    row[10 + index]);
            gems.Add(slot, sites);
        }
        //238440 reads the actual five words and five bytes after221340's
        //constructor zeroing. Never replace these scenario values with zeros
        //or the independent fixed-definition maxima at4D..51.
        return new(gems);
    }

    internal static GemDiscoverySite? Read(TalentGemDiscoveryState? saved, GemReference gem, int index)
    {
        int physical = Physical(gem);
        if ((uint)index >= 5) throw Missing("site_index");
        if (saved is null || !saved.Gems.TryGetValue(physical, out var sites)) return null;
        if (sites is not { Length: 5 }) throw Missing("saved_five_sites");
        return sites[index];
    }

    /// <summary>223600(gem,actualArea,-1): update only the first matching
    /// location, even if its remaining count was already zero. The fixed cap
    /// is read only for that reached slot; no validity or extra match gate.</summary>
    internal static TalentGemDiscoveryState Consume(TalentGemDiscoveryState saved,
        SourceWorldContext current, GemReference gem, ushort area, out bool matched)
    {
        int physical = Physical(gem);
        if (!saved.Gems.TryGetValue(physical, out var sites) || sites is not { Length: 5 })
            throw Missing("saved_five_sites");
        for (int index = 0; index < 5; index++)
        {
            var site = sites[index] ?? throw Missing("saved_site");
            if (site.Area != area) continue;
            int maximum = current.ReadSkillGemRawByte(gem, 0x4D + index)
                ?? throw Missing("fixed_remaining_cap");
            byte remaining = unchecked((byte)Math.Clamp(unchecked(site.Remaining - 1), 0, maximum));
            var changed = (GemDiscoverySite[])sites.Clone();
            changed[index] = site with { Remaining = remaining };
            matched = true;
            return saved with { Gems = new(saved.Gems) { [physical] = changed } };
        }
        matched = false;
        return saved;
    }

    private static int Physical(GemReference gem) => gem.PhysicalSlot is >= 0 and < 31
        ? gem.PhysicalSlot.Value : throw Missing("Gem_physical");
    private static InvalidDataException Missing(string field) => new("talent.gem-discovery." + field);
}
