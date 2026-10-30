using San14.Core;

namespace San14.Data;

/// <summary>A title editor's detached working record. Saving the independent
/// profile and publishing its new value remain the application's responsibility.</summary>
public sealed class HistoricalOfficerEditorDraft
{
    private readonly HistoricalOfficerEditorCatalog catalog;
    private readonly HistoricalOfficerProfile profile;
    public HistoricalOfficerEditorRow Row { get; }
    public HistoricalOfficerEdit Original => Row.Original;
    public HistoricalOfficerEdit Saved { get; }
    public HistoricalOfficerEdit Value { get; private set; }
    public bool DirtyAgainstSaved => !HistoricalOfficerEditing.Equivalent(Value, Saved);
    public bool DirtyAgainstOriginal => !HistoricalOfficerEditing.Equivalent(Value, Original);

    public HistoricalOfficerEditorDraft(HistoricalOfficerEditorCatalog catalog,
        HistoricalOfficerProfile profile, int id)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        this.catalog = catalog; this.profile = profile;
        Row = catalog.Find(id);
        Saved = profile.Find(id) ?? Original;
        Saved.Validate();
        Value = Saved;
    }

    public void Replace(HistoricalOfficerEdit value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id != Original.Id)
            throw new InvalidDataException("A historical officer draft cannot change identity.");
        catalog.Validate(value);
        Value = value;
    }

    /// <summary>The original reset action changes the draft. It does not
    /// delete the saved record until the normal confirmation is accepted.</summary>
    public void RestoreOriginal() => Value = Original;

    public HistoricalOfficerProfile Commit()
    {
        catalog.Validate(Value);
        return DirtyAgainstOriginal ? profile.Set(Value) : profile.Remove(Value.Id);
    }
}

public static class HistoricalOfficerEditing
{
    /// <summary>Matches the original field comparison: slots retain their
    /// order, while the formation selection is a bit set.</summary>
    public static bool Equivalent(HistoricalOfficerEdit left, HistoricalOfficerEdit right) =>
        left.Id == right.Id && left.PortraitId == right.PortraitId && left.DeathYear == right.DeathYear &&
        left.Leadership == right.Leadership && left.Strength == right.Strength &&
        left.Intelligence == right.Intelligence && left.Politics == right.Politics && left.Charm == right.Charm &&
        left.DoctrineId == right.DoctrineId && left.PolicyId == right.PolicyId && left.PolicyBaseCost == right.PolicyBaseCost &&
        left.LikePersonIds.AsSpan().SequenceEqual(right.LikePersonIds.AsSpan()) &&
        left.DislikePersonIds.AsSpan().SequenceEqual(right.DislikePersonIds.AsSpan()) &&
        left.TraitIds.AsSpan().SequenceEqual(right.TraitIds.AsSpan()) &&
        left.TacticIds.AsSpan().SequenceEqual(right.TacticIds.AsSpan()) &&
        FormationMask(left) == FormationMask(right);

    public static HistoricalOfficerProfile Restore(HistoricalOfficerProfile profile, IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(ids);
        profile.Validate();
        var selected = ids.ToHashSet();
        if (selected.Any(id => profile.Find(id) is null))
            throw new InvalidDataException("Only saved historical officer overrides can be restored.");
        return new([.. profile.Overrides.Where(value => !selected.Contains(value.Id))]);
    }

    private static uint FormationMask(HistoricalOfficerEdit value) =>
        value.FormationIds.Aggregate(0u, (mask, id) => mask | (1u << id));
}
