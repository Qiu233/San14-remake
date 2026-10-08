namespace San14.Data;

public sealed record OriginalArmyModelModeInputs(bool? GlobalObjectValid,
    byte? GlobalFlags2C, bool? ContextPredicate16EEC0, byte? ArmyByte1B);

public enum OriginalArmyDescriptorSelectionKind { Unresolved, Cleared, Category }

/// <summary>A single source descriptor, never a complete unit assembly.</summary>
public sealed record OriginalArmyDescriptorSelection(OriginalArmyDescriptorSelectionKind Kind,
    int DescriptorOffset, int? Category, string Reason);

/// <summary>Source resource-request fields, not troop counts or world transforms.</summary>
public sealed record OriginalArmyDescriptorRequest(int DescriptorOffset, int RequestedResourceCount,
    int? Category, uint Flags)
{
    public bool Cleared => Category is null;
}

public sealed record OriginalArmyModelDescriptorPlan(int Mode,
    IReadOnlyList<OriginalArmyDescriptorRequest> Descriptors, int? EmittedActionCode)
{
    public bool OriginalInstanceTransformsVerified => false;
}

public sealed record OriginalArmyContextIndependentDescriptors(IReadOnlyList<int> PossibleModes,
    IReadOnlyList<OriginalArmyDescriptorRequest> Descriptors)
{
    public bool OriginalCompleteAssemblyVerified => false;
}

/// <summary>Current CE 1C92A0/1C8D20 selection branches. The legacy Global
/// input names refer to the Terrain definition returned by Army 22BB50.
/// Native runtime inputs
/// remain separate from UnitState.FormationId and UnitMission.Kind.</summary>
public static class OriginalArmyModelSelection
{
    public static int ChooseMode(OriginalArmyModelModeInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        bool valid = inputs.GlobalObjectValid ?? throw Missing("global validity");
        if (valid && ((inputs.GlobalFlags2C ?? throw Missing("global byte+2C")) & 2) != 0) return 1;
        if (inputs.ContextPredicate16EEC0 ?? throw Missing("context predicate16EEC0")) return 3;
        byte formationByte = inputs.ArmyByte1B ?? throw Missing("Army byte+1B");
        return formationByte is >= 8 and <= 10 ? 2 : 0;
    }

    public static OriginalArmyDescriptorSelection ChooseKnownDescriptor(int mode,
        byte? armyByte1B, byte? armyByte1C)
    {
        if (mode is < 0 or > 3) throw new InvalidDataException($"Unsupported native Army model mode {mode}.");
        if (mode is 2 or 3)
        {
            byte value = armyByte1B ?? throw Missing("Army byte+1B");
            return value is >= 8 and <= 10
                ? new(OriginalArmyDescriptorSelectionKind.Category, 0x7A0, value - 4,
                    "source 1C8D20: byte+1B 8..10 writes category 4..6 at descriptor+1C")
                : new(OriginalArmyDescriptorSelectionKind.Cleared, 0x7A0, null,
                    "source non-siege branch clears descriptor 7A0");
        }
        if (mode == 1)
        {
            byte value = armyByte1C ?? throw Missing("Army byte+1C");
            int? category = value switch { 11 or 20 => 8, 12 or 17 => 7, 13 or 18 => 9, _ => null };
            if (category is int known) return new(OriginalArmyDescriptorSelectionKind.Category,
                0x818, known, "source 1C9034 jump table: category at descriptor+1C");
            return new(OriginalArmyDescriptorSelectionKind.Cleared,
                0x818, null, "source 1C8D20 clears descriptor 818 for every unmatched byte+1C");
        }
        return new(OriginalArmyDescriptorSelectionKind.Unresolved, 0x7A0, null,
            "source mode 0 requests three separate descriptors; use DescribeAssembly, not a single mesh");
    }

    /// <summary>Ports all six descriptor writes and clears from original
    /// SC 1C8D20 / TC 1C8D30. Counts are resource requests at descriptor+18.
    /// Instance positions, transforms, current bones and dynamic flags are
    /// separate source writers and are deliberately not synthesized here.</summary>
    public static OriginalArmyModelDescriptorPlan DescribeAssembly(int mode,
        byte? armyByte1B, byte? armyByte1C)
    {
        if (mode is < 0 or > 3) throw new InvalidDataException($"Unsupported native Army model mode {mode}.");
        int[] offsets = [0x750, 0x778, 0x7A0, 0x7C8, 0x818, 0x840];
        var result = offsets.ToDictionary(offset => offset,
            offset => new OriginalArmyDescriptorRequest(offset, 0, null, 0));
        void Write(int offset, int count, int category, uint flags) => result[offset] = new(offset, count, category, flags);
        void Ordinary()
        {
            Write(0x750, 4, 1, 1);
            Write(0x778, 15, 0, 1);
            Write(0x7C8, 3, 3, 0);
        }
        if (mode == 0) Ordinary();
        else if (mode == 1)
        {
            OriginalArmyDescriptorSelection ship = ChooseKnownDescriptor(mode, null, armyByte1C);
            if (ship.Category is int category) Write(0x818, 1, category, 0);
        }
        else
        {
            OriginalArmyDescriptorSelection siege = ChooseKnownDescriptor(mode, armyByte1B, null);
            if (siege.Category is int category) Write(0x7A0, 3, category, 0);
            else if (mode == 3) Ordinary();
            if (mode == 3) Write(0x840, 3, 10, 0);
        }
        return new(mode, Array.AsReadOnly(offsets.Select(offset => result[offset]).ToArray()),
            mode == 3 ? 0xD6 : null);
    }

    /// <summary>Source descriptors whose requests agree for every possible
    /// result of the unprovided 16EEC0 context predicate. This can display
    /// caller-selected source components without inventing that predicate.
    /// Current Army bytes and component instance poses still need explicit
    /// inputs; this is not a Core formation/mission conversion.</summary>
    public static OriginalArmyContextIndependentDescriptors DescribeContextIndependent(
        OriginalArmyTerrainSelection terrain,byte? armyByte1B,byte? armyByte1C)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if (terrain.UsesArmyByte1C)
            return new(Array.AsReadOnly(new[] {1}),DescribeAssembly(1,null,armyByte1C).Descriptors);
        byte value=armyByte1B ?? throw Missing("Army byte+1B");
        int regularMode=value is >=8 and <=10 ? 2 : 0;
        var regular=DescribeAssembly(regularMode,value,null);
        var alternate=DescribeAssembly(3,value,null);
        var shared=regular.Descriptors.Where(request=>alternate.Descriptors.Any(other=>request==other)).ToArray();
        return new(Array.AsReadOnly(new[] {regularMode,3}),Array.AsReadOnly(shared));
    }

    private static InvalidDataException Missing(string field) =>
        new($"Current Army model {field} is unknown; a Core gameplay field is not substituted.");
}
