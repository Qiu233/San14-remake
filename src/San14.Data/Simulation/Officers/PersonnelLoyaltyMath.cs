namespace San14.Data;

/// <summary>Explicit results of the original guards. The two eight-entry lists
/// remain identified by native member offsets, without guessing relation names.</summary>
public sealed record PersonnelLoyaltyGuards(bool? IsRuler,
    bool? RelationPredicate276C30, bool? RulerInListD0, bool? RulerInListC0,
    bool? ForceStored255Flag);

public static class PersonnelLoyaltyMath
{
    /// <summary>Null means a consumed guard is unknown. In TC, the source routine
    /// skips rulers, skips relation=true/listD0=false, then skips listC0=true.
    /// SC has the corresponding language-shifted person member offsets.</summary>
    public static bool? ShouldChange(PersonnelLoyaltyGuards guards)
    {
        ArgumentNullException.ThrowIfNull(guards);
        if (guards.IsRuler is not bool ruler) return null;
        if (ruler) return false;
        if (guards.RelationPredicate276C30 is not bool relation) return null;
        if (relation)
        {
            if (guards.RulerInListD0 is not bool listed) return null;
            if (!listed) return false;
        }
        return guards.RulerInListC0 is bool exempt ? !exempt : null;
    }

    /// <summary>Pure source calculation for an original item assignment, after
    /// explicit guard and parameter resolution. Null is unresolved; returned 255
    /// retains the native flagged sentinel and is not ordinary Core loyalty 100.</summary>
    public static int? TryApply(byte storedLoyalty, byte rawItemLoyaltyValue,
        bool grant, int confiscationPenalty, PersonnelLoyaltyGuards guards)
    {
        bool? change = ShouldChange(guards);
        if (change is null) return null;
        if (!change.Value) return storedLoyalty;
        if (guards.ForceStored255Flag is not bool force255) return null;
        if (force255) return 255;
        int itemValue = Math.Min((int)rawItemLoyaltyValue, 50);
        // Original operations are 32-bit; parameter changes are explicit operands.
        return grant ? Math.Min(storedLoyalty + itemValue, 100) :
            Math.Max(unchecked(storedLoyalty - itemValue - confiscationPenalty), 0);
    }
}
