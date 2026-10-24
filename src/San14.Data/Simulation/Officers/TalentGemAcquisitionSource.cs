using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Normal scenario inventory loading and the successful search's
/// acquired-book and discovery-site writes. Reports belong to the caller.</summary>
internal static class TalentGemAcquisitionSource
{
    internal static GemForceInventoryState LoadFresh(LoadedScenario scenario)
    {
        if (scenario.Metadata.FormatMarker != 0x66 || scenario.Snapshot.Forces.Count != 52)
            throw Missing("scenario_52_Forces");
        var forces = new Dictionary<int, ushort[]>(52);
        for (int physical = 0; physical < 52; physical++)
        {
            var source = scenario.Snapshot.Forces[physical];
            if (source.Slot != physical || source.RemainingBytesHex.Length != 327 * 2)
                throw Missing("scenario_Force_row");
            byte[] bytes;
            try { bytes = Convert.FromHexString(source.RemainingBytesHex); }
            catch (FormatException error)
            { throw new InvalidDataException("talent.gem-inventory.scenario_Force_bytes", error); }
            var counts = new ushort[31];
            //237CC0 reaches 21F0A0 after 258 serialized bytes. The preview
            //tail excludes the leading two-byte ruler, so this starts at 256.
            for (int gem = 0; gem < counts.Length; gem++)
                counts[gem] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(256 + gem * 2, 2));
            forces.Add(physical, counts);
        }
        return new(forces);
    }

    internal static ushort? Read(GemForceInventoryState? saved, ForceReference force, GemReference gem)
    {
        int physical = GemPhysical(gem);
        //The original count getter does not read the force at all for Gem 0.
        if (physical == 0) return 0;
        int owner = ForcePhysical(force);
        if (saved is null || !saved.Forces.TryGetValue(owner, out var counts)) return null;
        if (counts is not { Length: 31 }) throw Missing("saved_31_counts");
        return counts[physical];
    }

    internal static GameState ApplyAcquire(GameState state, SourceWorldContext current,
        ForceReference force, GemReference gem, AreaReference area)
    {
        int physical = GemPhysical(gem);
        var inventory = state.GemForceInventory;
        //1DE430(force,gem,1) neither reads nor writes slot zero. In
        //particular, it does not apply the later report's Force-valid gate.
        if (physical != 0)
        {
            int owner = ForcePhysical(force);
            if (inventory is null || !inventory.Forces.TryGetValue(owner, out var counts))
                throw Missing("saved_Force_inventory");
            if (counts is not { Length: 31 }) throw Missing("saved_31_counts");
            var changed = (ushort[])counts.Clone();
            changed[physical] = (ushort)Math.Clamp((int)counts[physical] + 1, 0, 100);
            inventory = inventory with { Forces = new(inventory.Forces) { [owner] = changed } };
        }
        int areaPhysical = current.ReadAreaPhysicalSlot(area) ?? throw Missing("Area_physical");
        var sites = state.TalentGemDiscoveries ?? throw Missing("saved_discovery_sites");
        //223600 is reached even for Gem 0. Its caller ignores a missing match.
        sites = TalentGemDiscoveryInputs.Consume(sites, current, gem, checked((ushort)areaPhysical), out _);
        return state with { GemForceInventory = inventory, TalentGemDiscoveries = sites };
    }

    private static int GemPhysical(GemReference gem) => gem.PhysicalSlot is >= 0 and < 31
        ? gem.PhysicalSlot.Value : throw Missing("Gem_physical");
    private static int ForcePhysical(ForceReference force) => force.PhysicalSlot is >= 0 and < 52
        ? force.PhysicalSlot.Value : throw Missing("Force_physical");
    private static InvalidDataException Missing(string field) => new("talent.gem-inventory." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyTalentGemAcquisitionCurrent(GameState state, SourceWorldContext current,
        ForceReference force, GemReference gem, AreaReference area)
        => TalentGemAcquisitionSource.ApplyAcquire(state, current, force, gem, area);
}
