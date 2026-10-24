using System.Collections.ObjectModel;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Reads the ordinary pre-start government page without starting
    /// strategy, allocating a saved policy bank, executing an order or changing
    /// the world. The host retains this publication until the modal closes.</summary>
    public NewGameForceGovernancePreview CaptureNewGameForceGovernance(GameState state,
        int forceId, NewGameForceGovernanceApplication? application = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var issues = new List<string>();
        var people = new Dictionary<int, NewGameGovernanceOfficer>();
        var slots = new List<NewGameGovernancePersonnelSlot>(35);
        bool? valid = null;
        bool? canEdit = application?.UserStrategyStatePresent == false ? false : null;
        if (canEdit is null) issues.Add("new-game.government.application-edit-owner");
        var roster = new GovernanceRosterView(forceId, null, [], [], null, null,
            "new-game.government.normal-source-owner");
        if (!ArmyOperationContext.IsNewGamePreview(state) || _scenario.Metadata.FormatMarker != 0x66)
            return Publish();
        try
        {
            if (state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256 ||
                state.ContentHashes.GetValueOrDefault(_scenario.FixedDataSource.ResourceUri) != _scenario.FixedDataSource.Sha256)
                throw new InvalidDataException("new-game.government.content-owner");
            if (forceId is < 1 or > 51 || !state.Forces.TryGetValue(forceId, out var force))
            { valid = false; return Publish(); }
            var sourceRow = _scenario.Snapshot.Forces.FirstOrDefault(row => row.Slot == forceId)
                ?? throw new InvalidDataException("new-game.government.normal-Force-row");
            ushort? ruler = force.RulerId is >= 0 and <= ushort.MaxValue ? (ushort)force.RulerId.Value
                : sourceRow.RulerSlot == 0 && force.RulerId is null ? (ushort)0 : null;
            valid = NativeValidity.Force(forceId, ruler);
            if (valid != true)
            { if (valid is null) issues.Add("new-game.government.force-validity"); return Publish(); }
            var fields = (ForcePolicyPersonnelFields.Read(sourceRow) with { Ruler = ruler!.Value }).ForGovernmentPage();
            roster = GovernanceRosterSource.Read(state, forceId, _scenario.Snapshot.DoctrineParameters, [], fields);
            for (int group = 0; group < 5; group++)
                for (int ordinal = 0; ordinal < 7; ordinal++)
                    slots.Add(new(group, ordinal, fields.Departments[group].Members[ordinal],
                        fields.Departments[group].PendingValues[ordinal]));
            //Resolve only reached references once. A vacant raw0 reference is
            //not replaced with a fabricated current officer or a role heuristic.
            var references = new[] { (int)fields.Ruler, fields.Advisor }
                .Concat(fields.Departments.SelectMany(group => group.Members.Select(id => (int)id)))
                .Where(id => id != 0).Distinct();
            foreach (int referenceId in references)
            {
                //The normal ID directory is built once by the existing source
                //owner. This page does not recapture the map or registry lists.
                int? physical = referenceId <= 6000 ? _definitions.ResolveOfficerSource(referenceId) : 0;
                var raw = physical is int p ? _scenario.Snapshot.Characters[p] : null;
                bool? personValid = raw is null ? false : NativeValidity.Person(raw.Slot,
                    checked((ushort)raw.CharacterId), raw.CreditReference?.RawFlagsWord,
                    raw.CreditReference?.RawTailCode, raw.StatusCode);
                int? actual = raw?.CharacterId;
                var person = actual is int id ? state.Officers.GetValueOrDefault(id) : null;
                int? policyId = person?.PolicyId;
                string? policyName = policyId is int policy ? state.Policies.GetValueOrDefault(policy)?.Name : null;
                int? cost = actual is int officer && PolicyCosts.TryCalculate(state, officer, out var quote)
                    ? quote.Total : null;
                string? missing = personValid is null ? "new-game.government.person-validity"
                    : personValid == false ? null : person is null ? "new-game.government.person-owner"
                    : cost is null ? "new-game.government.person-policy-cost" : null;
                people[referenceId] = new(referenceId, physical, actual, personValid,
                    person?.Name, policyId, policyName, cost, missing);
            }
            if (roster.UnresolvedInput is string rosterIssue) issues.Add(rosterIssue);
            //The experience/modifier owner is independently mutable in the
            //started game. Its absence here is visible, not inferred as zero.
            issues.Add("new-game.government.cost-limit-current-owner");
        }
        catch (InvalidDataException error) { issues.Add(error.Message); }
        return Publish();

        NewGameForceGovernancePreview Publish() => new(forceId, valid, canEdit, roster,
            new ReadOnlyDictionary<int, NewGameGovernanceOfficer>(people), slots.AsReadOnly(), issues.AsReadOnly());
    }
}
