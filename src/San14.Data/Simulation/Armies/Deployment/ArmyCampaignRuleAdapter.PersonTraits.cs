using San14.Core;
using System.Text;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DeploymentPersonTraitQuery QueryDeploymentPersonTraits(GameState state,
        IReadOnlyList<int> candidateIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidateIds);
        try
        {
            var source = new PersonTraitReader(state, _definitions);
            var rows = new List<DeploymentPersonTraitRow>(candidateIds.Count);
            foreach (int id in candidateIds)
            {
                source.RequireOwner(id);
                var cells = new List<DeploymentPersonBasicValue<DeploymentPersonTraitValue>>(5);
                for (int slot = 0; slot < 5; slot++)
                {
                    try { cells.Add(new(source.Display(id, slot), null)); }
                    catch (InvalidDataException error) { cells.Add(new(null, error.Message)); }
                }
                rows.Add(new(id, cells.AsReadOnly()));
            }
            return new(rows.AsReadOnly(), null);
        }
        catch (PersonTraitStructureError error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public DeploymentPersonOrderQuery QueryDeploymentPersonTraitOrder(GameState state,
        IReadOnlyList<DeploymentPersonSortRow> currentRows, int slot, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(currentRows);
        try
        {
            if ((uint)slot >= 5) throw PersonTraitMissing("slot");
            var source = new PersonTraitReader(state, _definitions);
            int[] rows = currentRows.Select(row => row.OfficerId).ToArray();
            DeploymentPersonTraitSort.Sort(rows, (left, right) => source.Before(left, right, slot, ascending));
            return new(Array.AsReadOnly(rows), null);
        }
        catch (PersonTraitStructureError error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private sealed class PersonTraitReader(GameState state, ArmyOperationContext definitions)
    {
        private SourceWorldContext? _current;
        private readonly Dictionary<int, int> _physicals = [];
        private readonly Dictionary<(int Officer, int Slot), PersonalityReference> _references = [];
        private readonly Dictionary<int, string> _names = [], _keys = [];

        private SourceWorldContext Current
        {
            get
            {
                if (_current is not null) return _current;
                try { return _current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions); }
                catch (InvalidDataException error) { throw new PersonTraitStructureError(error.Message); }
            }
        }

        internal int RequireOwner(int id)
        {
            if (_physicals.TryGetValue(id, out int physical)) return physical;
            if (!state.Officers.ContainsKey(id) || definitions.ResolveOfficerSource(id) is not int resolved ||
                definitions.ReadOfficerSourceIdentity(resolved) != id)
                throw new PersonTraitStructureError(PersonTraitMissing("person_owner").Message);
            return _physicals[id] = resolved;
        }

        private PersonalityReference Resolve(int id, int slot)
        {
            if (_references.TryGetValue((id, slot), out var reference)) return reference;
            int physical = RequireOwner(id);
            reference = TacticRemakeCurrentTraits.ResolveCommanderPersonality(Current, physical, slot)
                ?? throw PersonTraitMissing("current_trait_reference");
            return _references[(id, slot)] = reference;
        }

        private bool Valid(PersonalityReference trait) => Current.ReadSkillPersonalityValidity(trait)
            ?? throw PersonTraitMissing("trait_validity");

        private int Physical(PersonalityReference trait) => Current.ReadPersonalityPhysicalSlot(trait)
            ?? throw PersonTraitMissing("trait_physical");

        internal DeploymentPersonTraitValue? Display(int id, int slot)
        {
            var reference = Resolve(id, slot);
            return Valid(reference) ? new(Physical(reference), Text(reference, name: true)) : null;
        }

        internal bool Before(int left, int right, int slot, bool ascending)
        {
            // Both current slots are resolved before the outer validity gates.
            // These predicates are intentionally not strict for invalid/equal
            // values. In particular, even the same officer must be consumed.
            var one = Resolve(left, slot);
            var two = Resolve(right, slot);
            if (!Valid(one) || !Valid(two)) return !Valid(two);
            string first = Text(one, name: false), second = Text(two, name: false);
            bool before = first.Length != 0 && second.Length != 0
                ? string.CompareOrdinal(first, second) < 0
                : first.Length == 0 && second.Length == 0
                    ? string.CompareOrdinal(Text(one, name: true), Text(two, name: true)) < 0
                    : second.Length == 0;
            return ascending ? before : !before;
        }

        private string Text(PersonalityReference trait, bool name)
        {
            int physical = Physical(trait);
            var cache = name ? _names : _keys;
            if (cache.TryGetValue(physical, out string? value)) return value;
            int count = name ? 6 : 16, offset = name ? 0x10 : 0x1C;
            var text = new StringBuilder(count);
            for (int index = 0; index < count; index++)
            {
                ushort unit = Current.ReadPersonalityWord(trait, offset + index * 2)
                    ?? throw PersonTraitMissing(name ? "trait_name" : "trait_sort_key");
                if (unit == 0) return cache[physical] = text.ToString();
                text.Append((char)unit);
            }
            throw PersonTraitMissing(name ? "trait_name_terminator" : "trait_sort_key_terminator");
        }
    }

    private sealed class PersonTraitStructureError(string message) : Exception(message);
    private static InvalidDataException PersonTraitMissing(string field) =>
        new("deployment.person-traits." + field);
}
