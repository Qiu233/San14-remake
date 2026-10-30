using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record RegisteredOfficerSexChangeResult(RegisteredOfficerLibrary Library, RegisteredOfficer Officer);

/// <summary>The ordinary editor's sex-change transaction. It owns only the
/// registered library; the current campaign is never read or modified.</summary>
public static class RegisteredOfficerSexChange
{
    public static bool RequiresConfirmation(RegisteredOfficerLibrary library, RegisteredOfficer original,
        RegisteredOfficer proposed, RegisteredOfficerEditorCatalog catalog)
    {
        CheckIdentity(original, proposed);
        if (original.Sex == proposed.Sex) return false;
        return HasRelationships(library, original, catalog) ||
            proposed.Sex == 0 && original.TraitIds.Any(catalog.IsFemaleOnlyTrait);
    }

    public static RegisteredOfficerLibrary Apply(RegisteredOfficerLibrary library, RegisteredOfficer original,
        RegisteredOfficer proposed, RegisteredOfficerEditorCatalog catalog, bool confirmed) =>
        Prepare(library, original, proposed, catalog, confirmed).Library;

    public static RegisteredOfficerSexChangeResult Prepare(RegisteredOfficerLibrary library, RegisteredOfficer original,
        RegisteredOfficer proposed, RegisteredOfficerEditorCatalog catalog, bool confirmed)
    {
        CheckIdentity(original, proposed);
        proposed.Validate(requireName: false);
        if (RequiresConfirmation(library, original, proposed, catalog) && !confirmed)
            throw new InvalidOperationException("Registered-officer sex change requires the original relationship confirmation.");
        if (original.Sex == proposed.Sex) return new(ReplaceExisting(library, proposed), proposed);

        var working = library.Officers.ToDictionary(officer => officer.Id);
        bool existing = working.ContainsKey(original.Id);
        if (HasRelationships(library, original, catalog))
        {
            // 21DB50(record, 0) clears the old registered spouse's back reference
            // before clearing the current record. It does not erase arbitrary
            // incoming relationship fields throughout the library.
            if (working.TryGetValue(original.SpouseId, out var spouse))
                working[spouse.Id] = spouse with { SpouseId = 0 };
            proposed = proposed with { SpouseId = 0 };
            if (existing) working[original.Id] = proposed;

            // 206780 processes each actual registered child. Removing a father
            // invokes 21C6B0 and carries the new paternal family through descendants.
            foreach (int id in library.Officers.Select(officer => officer.Id))
            {
                var child = working[id];
                if (child.FatherId == original.Id && KnownPerson(catalog, original.Id))
                {
                    child = child with { FatherId = 0 };
                    working[id] = child;
                    ReassignPaternalFamily(working, id, catalog);
                }
                child = working[id];
                if (child.MotherId == original.Id)
                    working[id] = child with { MotherId = 0 };
            }
        }
        if (proposed.Sex == 0)
        {
            var traits = proposed.TraitIds.Where(id => id != 0 && !catalog.IsFemaleOnlyTrait(id)).ToArray();
            proposed = proposed with { TraitIds = [.. traits.Concat(Enumerable.Repeat(0, 9 - traits.Length))] };
        }
        if (existing) working[original.Id] = proposed;
        return new(new([.. library.Officers.Select(officer => working[officer.Id])]), proposed);
    }

    /// <summary>Returns the same source-adjusted draft as Apply, including for a
    /// new unregistered slot whose empty name must not enter the committed library.</summary>
    public static RegisteredOfficer ApplyDraft(RegisteredOfficerLibrary library, RegisteredOfficer original,
        RegisteredOfficer proposed, RegisteredOfficerEditorCatalog catalog, bool confirmed) =>
        Prepare(library, original, proposed, catalog, confirmed).Officer;

    private static bool HasRelationships(RegisteredOfficerLibrary library, RegisteredOfficer officer,
        RegisteredOfficerEditorCatalog catalog) =>
        KnownPerson(catalog, officer.SpouseId) || KnownPerson(catalog, officer.SwornBrotherId) ||
        library.Officers.Any(other => other.FatherId == officer.Id || other.MotherId == officer.Id);

    private static bool KnownPerson(RegisteredOfficerEditorCatalog catalog, int id) =>
        id != 0 && catalog.Officers.Any(officer => officer.Id == id);

    private static void ReassignPaternalFamily(Dictionary<int, RegisteredOfficer> working, int rootId,
        RegisteredOfficerEditorCatalog catalog)
    {
        var root = working[rootId];
        // The only caller has just removed this record's father. The source
        // therefore selects its own person identity as the new family identity.
        ushort family = (ushort)root.Id;
        if (root.Sex != 0 || root.FamilyId == family)
        { working[rootId] = root with { FamilyId = family }; return; }

        var pending = new Queue<int>();
        foreach (var child in working.Values.Where(child => child.Id != rootId && child.FatherId == rootId))
            pending.Enqueue(child.Id);
        var visited = new HashSet<int>();
        while (pending.TryDequeue(out int id))
        {
            if (!visited.Add(id)) continue;
            var child = working[id] with { FamilyId = family };
            // The source applies its full mother/spouse/sworn qualification
            // after a family change. Without their captured normal Person
            // relationship inputs this transaction must remain uncommitted.
            RequireRelationshipInputs(child, catalog);
            working[id] = child;
            if (child.Sex == 0)
                foreach (var descendant in working.Values.Where(value => value.Id != id && value.FatherId == id))
                    pending.Enqueue(descendant.Id);
        }
        working[rootId] = root with { FamilyId = family };
    }

    private static void RequireRelationshipInputs(RegisteredOfficer officer, RegisteredOfficerEditorCatalog catalog)
    {
        if (KnownPerson(catalog, officer.MotherId) || KnownPerson(catalog, officer.SpouseId) || KnownPerson(catalog, officer.SwornBrotherId))
            throw new InvalidDataException($"Registered officer {officer.Id} requires the source mother, spouse and sworn relationship qualification after its paternal family changes.");
    }
    private static RegisteredOfficerLibrary ReplaceExisting(RegisteredOfficerLibrary library, RegisteredOfficer proposed) =>
        new([.. library.Officers.Select(officer => officer.Id == proposed.Id ? proposed : officer)]);
    private static void CheckIdentity(RegisteredOfficer original, RegisteredOfficer proposed)
    {
        if (original.Id != proposed.Id) throw new InvalidDataException("Registered-officer sex-change identities differ.");
    }
}
