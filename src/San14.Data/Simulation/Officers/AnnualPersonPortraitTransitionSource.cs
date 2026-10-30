using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The complete annual reference scan, on the enclosing candidate.
    /// Saved portrait writes are immediately visible to this same current.
    /// Committed-world queries own portrait/HUD refresh; this does not execute
    /// the original five-card UI, App dispatcher or any presentation ACK.</summary>
    internal sealed class AnnualPersonPortraitTransitionSource(GameState state,
        SourceWorldContext current, Action<string, GameState>? observe = null)
    {
        internal GameState Run()
        {
            foreach (var row in PersonPortraitTransitionCatalog.Rules)
            {
                Observe($"portrait-transition.row.{row.PersonId}");
                var reference = (row.PersonId <= 6000
                    ? current.ReadPersonLookupReference(row.PersonId)
                    : current.ReadTransferPersonTableReference(0)) ?? throw Missing("person_lookup");
                if (reference.PhysicalSlot is not int physical || !Valid(physical)) continue;
                // The predicate, including its independent validity read,
                // precedes membership in the two expected portrait values.
                bool selected = Predicate(physical);
                Observe($"portrait-transition.predicate.{row.PersonId}");
                ushort appearance = Need(current.ReadPersonPortraitAppearance(physical), "current_appearance");
                if (appearance != row.PortraitWhenFalse && appearance != row.PortraitWhenTrue) continue;
                int raw = selected ? row.PortraitWhenTrue : row.PortraitWhenFalse;
                ushort next = unchecked((ushort)Math.Max(raw, 0));
                ushort id = Need(current.ReadPersonIdentity(physical), "person_identity");
                var owner = Person(physical, id);
                // Lookup aliases and selected pointer identity are distinct.
                // The actual current owner must be publishable at that pointer.
                state = state with { Officers = new(state.Officers)
                    { [id] = owner with { PortraitAppearanceIndex = next } } };
                current.SetPersonPortraitAppearance(physical, next);
                Observe($"portrait-transition.written.{row.PersonId}");
            }
            Observe("portrait-transition.completed");
            return state;
        }

        private bool Predicate(int physical)
            => PersonPortraitTransitionPredicate.Read(state, current, physical);

        private OfficerState Person(int physical, ushort id) =>
            current.ResolveOfficerSource(id) == physical && state.Officers.TryGetValue(id, out var person)
                ? person : throw Missing("current_person_owner");
        private bool Valid(int physical) => Need(current.ReadPersonNativeValidity(physical), "current_person_validity");
        private void Observe(string point) { if (observe is not null) observe(point, GameStateCopy.Copy(state)); }
        private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => new("annual-person-portrait-transition." + name);
    }
}
