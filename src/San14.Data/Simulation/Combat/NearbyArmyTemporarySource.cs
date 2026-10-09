namespace San14.Data;

/// <summary>TC2A78D0 / 2A7EE0. The caller owns the output descriptor and
/// publishes the operation's primary pool. This fills actual current chains,
/// including the intermediate Hex allocation/free order; it does not project
/// Core Units, query233560, deduplicate Armies, or insert the center.</summary>
public static class NearbyArmyTemporarySource
{
    public static int FillNearbyArmyTemporary(SourceWorldContext current,
        ArmyUnitReference army, int radius, HexListHandle output)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(output);
        if (radius < 0) throw new InvalidDataException("Nearby Army radius must be nonnegative in this source profile.");
        if (current.ReadArmyUnitRawHex2A(army) is not ushort rawHex)
            throw Missing("Army.raw2A");
        var center = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw Missing("Army.center_Hex_pointer");

        current.ClearNearbyTemporary(output, false);
        var hexes = current.CreateNearbyTemporary();
        try
        {
            _ = FillNearbyHexTemporary(current, center, radius, hexes);
            var node = current.ReadSkillTemporaryHead(hexes) ?? throw Missing("intermediate_Hex_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw Missing("intermediate_Hex_payload");
                ushort raw = current.ReadHexRaw1C(hex) ?? throw Missing("Hex.raw1C");
                //231660 sign extends i16, then2A78D0 accepts only1..500.
                int slot = unchecked((short)raw);
                if (slot is >= 1 and <= 500)
                {
                    var candidate = current.ReadArmyUnitTableReference(slot) ?? throw Missing("Hex.Army_pointer");
                    if (current.ReadArmyUnitNativeValidity(candidate) is not bool valid) throw Missing("Hex.Army_native_validity");
                    if (valid) current.AppendNearbyArmyTemporary(output, candidate);
                }
                node = current.ReadHexListNodeNext(node) ?? throw Missing("intermediate_Hex_next");
            }
            return current.ReadTemporaryHexListCount(output) ?? throw Missing("output_Army_count");
        }
        finally { current.ClearNearbyTemporary(hexes, true); }
    }

    /// <summary>TC2A7EE0's ring-only current Hex-pointer wrapper. Every
    /// nonnull CHex is natively valid, including table slot0. Failed tail node
    /// allocations suppress only that append, as in018CC0(flag1).</summary>
    public static int FillNearbyHexTemporary(SourceWorldContext current,
        HexReference center, int radius, HexListHandle output)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(output);
        if (radius < 0) throw new InvalidDataException("Nearby Hex radius must be nonnegative in this source profile.");
        current.ClearNearbyTemporary(output, false);
        if (!center.PhysicalSlot.HasValue || current.ReadHexPhysicalSlot(center) is not int physical)
            throw Missing("center_Hex_physical_identity");
        foreach (int key in RingHexOrder(physical, radius))
        {
            var hex = current.ReadHexTableReference(key) ?? throw Missing("candidate_Hex_pointer");
            if (hex.PhysicalSlot.HasValue) current.AppendNearbyHexTemporary(output, hex);
        }
        return current.ReadTemporaryHexListCount(output) ?? throw Missing("output_Hex_count");
    }

    private static IEnumerable<int> RingHexOrder(int center, int radius)
    {
        //Existing native target order already owns the proved2A7CD0 geometry.
        //Its center is inserted by a DIFFERENT caller (16BF20); remove it here.
        //The public target helper's byte-radius domain covers ordinary source
        //radii; retain the same loop for larger nonnegative integer radii.
        if (radius <= 255)
        {
            foreach (int hex in TacticRemakeCurrentContext.NativeHexOrder(center, radius).Skip(1)) yield return hex;
            yield break;
        }
        (int X, int Y)[][] directions =
        [
            [(-1, 0), (0, -1), (1, 0), (-1, 1), (0, 1), (1, 1)],
            [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 1), (1, 0)]
        ];
        int[] order = [2, 3, 6, 5, 4, 1];
        for (int ring = 1; ring <= radius; ring++)
        {
            int x = center % 220, y = center / 220;
            for (int step = 0; step < ring; step++) Step(4);
            foreach (int direction in order)
                for (int step = 0; step < ring; step++)
                {
                    Step(direction);
                    if ((uint)x < 220 && (uint)y < 220) yield return y * 220 + x;
                }
            void Step(int direction)
            {
                var delta = directions[Math.Abs(x) % 2][direction - 1];
                x += delta.X; y += delta.Y;
            }
        }
    }
    private static InvalidDataException Missing(string field) => new("Current nearby source field is unknown: " + field);
}

public sealed partial class SourceWorldContext
{
    internal HexListHandle CreateNearbyTemporary()
    {
        ConditionalRegistryEnter();
        try { return CreateTemporaryHexList() ?? throw new InvalidDataException("Current nearby descriptor allocator is unknown."); }
        finally { ConditionalRegistryExit(); }
    }
    internal void ClearNearbyTemporary(HexListHandle descriptor, bool release)
    {
        ConditionalRegistryEnter();
        try { ClearTemporaryHexList(descriptor); if (release) ReleaseTemporaryHexList(descriptor); }
        finally { ConditionalRegistryExit(); }
    }
    internal void AppendNearbyHexTemporary(HexListHandle descriptor, HexReference hex)
    {
        ConditionalRegistryEnter();
        try { _ = AppendTemporaryHexReference(descriptor, hex); }
        finally { ConditionalRegistryExit(); }
    }
    internal void AppendNearbyArmyTemporary(HexListHandle descriptor, ArmyUnitReference army)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = AllocateRegistryNode(new(descriptor.Identity))
                ?? throw new InvalidDataException("Current nearby Army node allocator is unknown.");
            if (node.Identity.HasValue) WriteRegistryNodePayload(node, new(RegistryKind.Army, army.PhysicalSlot));
        }
        finally { ConditionalRegistryExit(); }
    }
}
