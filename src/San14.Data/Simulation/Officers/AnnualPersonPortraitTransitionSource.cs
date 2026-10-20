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
        {
            if (!Valid(physical)) return false;
            if (Mode() == 2) return false;
            if (Mode() == 3 && (state.CampaignEventWorld?.ScenarioId
                ?? throw Missing("current_scenario_id")) == 151) return false;
            // The signed year is captured before the source's identity scan,
            // including before the later event45/Intelligence branches.
            short year = unchecked((short)(state.NativeMapSeason?.ThirdDate.Year
                ?? throw Missing("current_third_year")));
            ushort id = Need(current.ReadPersonIdentity(physical), "person_identity");
            foreach (var rule in PersonPortraitTransitionCatalog.Rules)
            {
                if (rule.PersonId != id) continue;
                if (id != 988) return rule.Direction == 0
                    ? year >= rule.ThresholdYear : year < rule.ThresholdYear;
                if (rule.Direction == 0) return false;
                var occurred = state.CampaignEvents?.Occurred ?? throw Missing("current_event_occurrence");
                if (occurred.Length <= 45) throw Missing("current_event_occurrence");
                if (occurred[45]) return false;
                if (year >= rule.ThresholdYear) return false;
                int intelligence = Person(physical, id).BaseAbilities?.Intelligence
                    ?? throw Missing("current_base_intelligence");
                if (intelligence is < 0 or > byte.MaxValue) throw Missing("current_base_intelligence");
                return intelligence < 70;
            }
            return false;
        }

        private OfficerState Person(int physical, ushort id) =>
            current.ResolveOfficerSource(id) == physical && state.Officers.TryGetValue(id, out var person)
                ? person : throw Missing("current_person_owner");
        private bool Valid(int physical) => Need(current.ReadPersonNativeValidity(physical), "current_person_validity");
        private int Mode() => state.CampaignEventWorld?.ScenarioMode ?? throw Missing("current_world_mode");
        private void Observe(string point) { if (observe is not null) observe(point, GameStateCopy.Copy(state)); }
        private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => new("annual-person-portrait-transition." + name);
    }
}
