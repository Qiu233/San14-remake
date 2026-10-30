using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public OfficerInformationPageSet PrepareMerchantOfficerInformationPages(GameState state,
        int playerForceId, IReadOnlyList<CityMerchantCandidate> actualCandidates)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actualCandidates);
        return new OfficerInformationCurrent(this, state, playerForceId, rowPaletteEnabled: false).MerchantPages(actualCandidates);
    }

    /// <summary>Open the merchant's Person reader over its draft-owned current.
    /// Capacity and checked sorting do not recapture the world or edit choices.</summary>
    public AreaAdministrationQuery<MerchantOfficerSelection> PrepareMerchantOfficerSelection(
        CityMerchantDraft draft, int playerForceId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            var source = draft.Source;
            //69DA40 is stable descending26A580 before B0=-1's checked sort.
            //Missing capacity is an unresolved opening, never a zero weight.
            var candidates = source.Candidates.OrderByDescending(candidate =>
                candidate.CapacityWeight ?? throw OfficerInformationCurrent.Missing(
                    "merchant.capacity_weight." + candidate.OfficerId)).ToArray();
            var initial = draft.SelectedOfficerIds;
            var reader = new OfficerInformationCurrent(this, source.State, playerForceId,
                rowPaletteEnabled: false, retainedCurrent: source.Current);
            var pages = reader.MerchantPages(candidates);
            pages.SortSelection(initial, selectedFirst: true);
            //This same actual Game5A33 no-argument message program is already
            //supported by the mature selector guide reader.
            string guide = new NarrationResourceMessages(
                _repository.LoadMessages(Seed.Language, San14.Data.Formats.MessageKind.Game).Messages)
                .ResolveIntelligenceSelectionGuide(0x5A33);
            return AreaAdministrationQuery<MerchantOfficerSelection>.Known(new(pages,
                initial, Math.Min(candidates.Length, CityMerchantSource.MaximumOfficers), 0x032E,
                guide, RequireSelection: false)
            {
                //cfg178=0 disables column overrides;2AFFE0 remains the
                //independent health1..3 callback2 consumed by the body.
                AlteredHealth = candidates.ToDictionary(candidate => candidate.OfficerId,
                    candidate => reader.MovementAlteredHealth(candidate.OfficerId))
            });
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<MerchantOfficerSelection>.Unknown(error.Message); }
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal OfficerInformationPageSet MerchantPages(IReadOnlyList<CityMerchantCandidate> candidates)
        {
            var weights = new Dictionary<int, int?>();
            var ids = new int[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                ids[index] = candidate.OfficerId;
                weights.TryAdd(candidate.OfficerId, candidate.CapacityWeight);
            }

            PreparedCell MerchantCell(int id, int column)
            {
                if (column != 141) return Cell(id, column);
                if (prepared.TryGetValue((id, column), out var saved)) return saved;
                int Capacity() => Need(weights.GetValueOrDefault(id), "merchant.capacity_weight." + id);
                return prepared[(id, column)] = MakeCell(() => Number(Capacity()),
                    () => new NumberKey(Capacity()));
            }

            OfficerInformationCell MerchantDisplay(int id, int column)
            {
                var cell = MerchantCell(id, column);
                //cfg178 disables only State/Advantage (slot56). Independent
                //credit/Force/Person callbacks for22/99 remain active.
                return cell.Display with
                {
                    StatePresets = null,
                    SortUnresolvedOwner = cell.SortUnresolvedOwner
                };
            }

            return new(Data(1, 0x032E), MerchantPersonInformationSources.Create(Data), ids,
                MerchantDisplay,
                (id, column) => MerchantCell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) =>
                    (MerchantCell(left, column).Key ?? throw Missing("sort_key"))
                    .Before(MerchantCell(right, column).Key ?? throw Missing("sort_key"), ascending))
                { DynamicPaletteEnabled = rowPaletteEnabled };
        }
    }
}
