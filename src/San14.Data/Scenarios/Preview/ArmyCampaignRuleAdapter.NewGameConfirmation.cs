using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One source-backed confirmation row. Unknown membership leaves
/// Count and Value null; a known partial list is never formatted as complete.</summary>
public sealed record NewGameConfirmationRow(int TitleId, string Title, string? Value,
    int? Count, IReadOnlyList<int> OrderedOfficerIds, bool? NonTrophy,
    IReadOnlyList<string> UnresolvedInputs);

public sealed record NewGameConfirmationPreview(IReadOnlyList<NewGameConfirmationRow> OfficerRows,
    bool? CollectionEligible, uint? FreshTransferServiceFlags, IReadOnlyList<string> UnresolvedInputs);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Publishes the ordinary confirmation page's five editor rows
    /// from the actual detached new-game load. The historical IDs are the
    /// already-applied adoption list, in management order, not checked UI rows.
    /// No fresh bank installation, random draw or world mutation occurs.</summary>
    public NewGameConfirmationPreview CaptureNewGameConfirmation(GameState preview,
        CollectionOfficerAdmission? admission = null, IReadOnlyList<int>? appliedHistoricalOfficerIds = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ArmyOperationContext.IsNewGamePreview(preview) || _scenario.Metadata.FormatMarker != 0x66)
            throw new InvalidDataException("new-game.confirmation.normal-preview-owner");
        return new NewGameConfirmationReader(this, CapturePersonPresentationCurrent(preview),
            admission, appliedHistoricalOfficerIds).Read();
    }

    private sealed class NewGameConfirmationReader(ArmyCampaignRuleAdapter owner,
        SourceWorldContext current, CollectionOfficerAdmission? admission, IReadOnlyList<int>? historicalIds)
    {
        private readonly List<string> allIssues = [];
        private IReadOnlyList<MessageRecord>? messages;
        private readonly List<(int Physical, int Identity)> people = [];
        private readonly List<string> peopleIssues = [];
        private int? scenario;

        internal NewGameConfirmationPreview Read()
        {
            messages = owner._repository.LoadMessages(owner._scenario.Snapshot.Locale, MessageKind.Data)
                .Messages.Sections[1].Records;
            scenario = owner._scenario.Snapshot.WorldParameters?.RawField48;
            if (scenario is null) peopleIssues.Add("new-game.confirmation.scenario-identity");
            foreach (int id in Enumerable.Range(3001, 150).Concat(Enumerable.Range(2001, 88))
                .Concat(Enumerable.Range(5101, 100))) ReadCandidate(id);
            var registered = PersonRow(0x4EB, people.Select(x => x.Identity).ToArray(), peopleIssues, false);
            var forces = ForceRow();
            var historyIssues = new List<string>();
            int[] selectedHistory = [];
            bool historicalAdopted = owner._scenario.InitialHistoricalOfficerEditsAdopted;
            if (historicalAdopted)
            {
                if (historicalIds is null) historyIssues.Add("new-game.confirmation.applied-historical-officer-list");
                else if (historicalIds.Count == 0 || historicalIds.Distinct().Count() != historicalIds.Count)
                    historyIssues.Add("new-game.confirmation.applied-historical-officer-list-identity");
                else selectedHistory = historicalIds.ToArray();
            }
            else if (historicalIds is { Count: > 0 })
                historyIssues.Add("new-game.confirmation.historical-list-not-applied");
            var historical = PersonRow(0x6B3, selectedHistory, historyIssues, historicalAdopted);
            var itemIssues = new List<string>();
            int? createdItems = CreatedItemCount(itemIssues);
            int historicalItems = owner._scenario.InitialItemEditorState?.Selection.HistoricalIds.Length ?? 0;
            // OwnedArmyReturnServiceInputs.InstallFresh starts at zero, then
            // HistoricalOfficerScenario installs bit2. Item finalization is
            // the only later fresh writer of bits6/22. Runtime editing/events
            // and turn operations are not part of this pre-start projection.
            uint flags = historicalAdopted ? 4u : 0u;
            bool? createdNonTrophy = false;
            if (owner._scenario.InitialItemEditorState is not null)
            {
                if (historicalItems > 0) flags |= 0x40;
                if (createdItems is int itemCount)
                { createdNonTrophy = itemCount > 0; if (itemCount > 0) flags |= 0x400000; }
                else createdNonTrophy = null;
            }
            var created = CountRow(0x7D5, createdItems, createdNonTrophy, itemIssues);
            var edited = CountRow(0x791, historicalItems, historicalItems > 0, []);
            uint? completeFlags = createdNonTrophy.HasValue ? flags : null;
            bool? eligible = null;
            try
            {
                eligible = TransferServiceEligibility.IsEligible(new ConfirmationEligibility(completeFlags,
                    owner.ReadApplicationTransferModeCurrent()));
            }
            catch (InvalidDataException error) { allIssues.Add(error.Message); }
            var rows = new[] { registered, forces, historical, created, edited };
            allIssues.AddRange(rows.SelectMany(row => row.UnresolvedInputs));
            return new(Array.AsReadOnly(rows), eligible, completeFlags,
                Array.AsReadOnly(allIssues.Distinct().ToArray()));
        }

        private void ReadCandidate(int id)
        {
            try
            {
                var reference = current.ReadPersonLookupReference(id);
                if (reference is null) throw Missing("person-lookup:" + id);
                // The cleared normal ID table has explicit null receivers for
                // unused created slots. NativeValid(null) rejects these two
                // bands; an unknown lookup remains a distinct missing owner.
                if (id is not (>= 2001 and <= 2088) && reference.PhysicalSlot is null) return;
                int physical = reference.PhysicalSlot ?? throw Missing("bonus-person-receiver:" + id);
                if (id is not (>= 2001 and <= 2088))
                {
                    bool valid = Need(current.ReadPersonNativeValidity(physical), "person-validity:" + id);
                    if (!valid) return;
                }
                // 5D3540 admits valid HomeBase OR status7/8. In particular a
                // selected roaming officer must not require a valid city.
                byte? status = current.ReadPersonStatus(physical);
                bool? validHome = null;
                if (current.ReadPersonFirstLocation(physical) is ushort home)
                    validHome = current.ReadBaseNativeValidity(BaseLookup.Find(home, current));
                if (validHome != true && status is not (7 or 8))
                {
                    if (!validHome.HasValue || !status.HasValue) throw Missing("person-home-or-status:" + id);
                    return;
                }
                int identity = Need(current.ReadPersonIdentity(physical), "person-identity:" + id);
                if (scenario == 0x13 && ((unchecked((uint)identity) + 0xEC13u) & 0xFFFBu) == 0) return;
                if (scenario == 0x3F && ScenarioExcludesBonus(identity)) return;
                // These are immutable admission facts. A candidate already
                // rejected by the independent final filter cannot affect the
                // result even when its unused profile/content fact is unknown.
                if (id is >= 2001 and <= 2088)
                {
                    bool? admitted = BonusAdmission(id);
                    if (!admitted.HasValue) throw Missing("bonus-admission:" + id);
                    if (!admitted.Value) return;
                }
                people.Add((physical, identity));
            }
            catch (InvalidDataException error) { peopleIssues.Add(error.Message); }
        }

        private bool? BonusAdmission(int id)
        {
            // 2CA3E0(mode0) bypasses its setting gates for these scenario63
            // identities. The later confirmation filter independently removes
            // them; the five content-gated identities do not take that bypass.
            if (scenario == 0x3F && ScenarioExcludesBonus(id)) return true;
            if (OrdinaryPortraitEligibility.NeedsApplicationSetting(id))
                return admission?.AllowedOfficerIds.TryGetValue(id, out bool allowed) == true ? allowed : null;
            if (id is >= 2050 and <= 2067 || !OrdinaryPortraitEligibility.NeedsUnlock(id)) return true;
            if (admission is null || !admission.Content.TryGetValue(id, out var content)) return null;
            return content.Owned switch { false => false, true => content.Available, _ => null };
        }

        // Complete cached 2CA520 switch, distinct from the smaller subset
        // whose ordinary portrait route requires an application setting.
        private static bool ScenarioExcludesBonus(int id) => id is >= 2001 and <= 2055
            or >= 2062 and <= 2077 or >= 2083 and <= 2088;

        private NewGameConfirmationRow ForceRow()
        {
            var issues = new List<string>(peopleIssues);
            var rulers = new List<int>();
            var members = people.Select(x => x.Physical).ToHashSet();
            for (int force = 1; force <= 51; force++)
            {
                try
                {
                    if (!Need(current.ReadForceNativeValidity(new(force)), "force-validity:" + force)) continue;
                    int ruler = Need(current.ReadForceRecord(force)?.RawRuler10, "force-ruler:" + force);
                    int physical = Person(ruler);
                    if (members.Contains(physical)) rulers.Add(Need(current.ReadPersonIdentity(physical), "ruler-identity:" + force));
                }
                catch (InvalidDataException error) { issues.Add(error.Message); }
            }
            return PersonRow(0x4EC, rulers, issues, false, true);
        }

        private int? CreatedItemCount(List<string> issues)
        {
            var initial = owner._scenario.InitialItemEditorState;
            var sourceItems = owner._scenario.Snapshot.ScenarioItems?.Items.ToDictionary(row => row.Slot);
            int count = 0;
            foreach (int id in Enumerable.Range(152, 50))
            {
                int? kind = initial?.Appearances.GetValueOrDefault(id)?.Kind;
                kind ??= sourceItems?.GetValueOrDefault(id)?.EmbeddedDefinition?.RawKind;
                if (kind is null) issues.Add("new-game.confirmation.effective-item-kind:" + id);
                else if (kind != 0) count++;
            }
            return issues.Count == 0 ? count : null;
        }

        private NewGameConfirmationRow PersonRow(int title, IReadOnlyList<int> ids, List<string> issues,
            bool? nonTrophy, bool force = false)
        {
            string? value = null;
            int? count = issues.Count == 0 ? ids.Count : null;
            if (issues.Count == 0)
            {
                if (ids.Count == 0) value = "-";
                else
                {
                    string? first = SourcePersonName(ids[0]);
                    if (first is null) issues.Add("new-game.confirmation.person-name:" + ids[0]);
                    else value = ids.Count == 1 ? first : Data(force ? 0x552 : 0x551)
                        .Replace("%s", first, StringComparison.Ordinal)
                        .Replace("%d", (ids.Count - 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                }
            }
            return new(title, Data(title), value, count,
                Array.AsReadOnly(ids.ToArray()), nonTrophy, Array.AsReadOnly(issues.Distinct().ToArray()));
        }

        private NewGameConfirmationRow CountRow(int title, int? count, bool? nonTrophy, List<string> issues) =>
            new(title, Data(title), count is int known ? known == 0 ? "-" : known.ToString(CultureInfo.InvariantCulture) : null,
                count, Array.Empty<int>(), nonTrophy, Array.AsReadOnly(issues.Distinct().ToArray()));
        private string? SourcePersonName(int identity)
        {
            int physical = Person(identity);
            // The normal detached snapshot includes unentered people that the
            // Core active roster does not publish. The same captured lookup
            // must still identify its already-applied source name record.
            if (current.ReadPersonIdentity(physical) != identity) return null;
            return owner._scenario.Snapshot.Characters.FirstOrDefault(row =>
                row.Slot == physical && row.CharacterId == identity)?.Name;
        }
        private int Person(int raw) => (raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw Missing("person-lookup:" + raw);
        private string Data(int id) => messages![id].Text ?? throw Missing("data1:" + id.ToString("X"));
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("new-game.confirmation." + field);
        private sealed record ConfirmationEligibility(uint? Flags, TransferModeState? Mode) : ITransferServiceEligibilityQueries
        {
            public uint? ReadWorldTransferServiceFlags() => Flags;
            public TransferModeState? ReadTransferModeState() => Mode;
        }
    }
}
