using San14.Core;

namespace San14.Data;

public sealed record DiplomacyExecutorSelection(OfficerInformationPageSet Pages,
    IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth, string? InitialOrderUnresolvedInput);

internal sealed record DiplomacyExecutorPreview(AreaAdministrationQuery<bool> Positive,
    AreaAdministrationQuery<int> Days);

public sealed partial class DiplomacyUiDraft
{
    /// <summary>The input event prepares its temporary predictions once.
    /// Sorting and repainting the returned page never evaluate new advice.</summary>
    public AreaAdministrationQuery<DiplomacyExecutorSelection> ReadExecutorSelection()
    {
        try
        {
            var offer = new DiplomacyOffer(GiftGold, GiftSupplies, GiftItemId, GiftAreaId);
            return AreaAdministrationQuery<DiplomacyExecutorSelection>.Known(source.ExecutorSelection(
                SelectedTargetForceId, AllianceMonths, SelectedTargetCityId, offer, RequestedOffer, SelectedCaptiveOfficerIds));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DiplomacyExecutorSelection>.Unknown(error.Message); }
    }
}

internal sealed partial class DiplomacyUiSource
{
    internal DiplomacyExecutorSelection ExecutorSelection(int? recipient, int months, int? targetCity,
        DiplomacyOffer given, DiplomacyOffer requested, IReadOnlyList<int> captives)
    {
        var previews = PrepareExecutorPreviews(recipient, months, targetCity, given, requested, captives,
            ExecutorTargetValid(recipient));
        return adapter.ReadDiplomacyExecutorSelectionCurrent(State, current, ForceId, Kind, Executors, previews);
    }

    //6B97D0 is the confirmed-captive consumer of the same advice/travel batch
    //used by745D00. It does not open or materialize an information-list UI.
    internal int? PreferredExecutor(int recipient, int months, int? targetCity,
        DiplomacyOffer given, DiplomacyOffer requested, IReadOnlyList<int> captives)
    {
        if (!ExecutorTargetValid(recipient)) return null;
        var previews = PrepareExecutorPreviews(recipient, months, targetCity, given, requested, captives, true);
        return adapter.ReadDiplomacyPreferredExecutorCurrent(State, current, ForceId, Executors, previews);
    }

    internal bool ExecutorNativeValid(int actor) =>
        Need(current.ReadPersonNativeValidity(people[actor]), "executor_validity");

    private bool ExecutorTargetValid(int? recipient) => recipient is int target &&
        Need(current.ReadForceNativeValidity(current.ReadForceTableReference(target)
            ?? throw Missing("executor_target_pointer")), "executor_target_validity");

    private IReadOnlyDictionary<int, DiplomacyExecutorPreview> PrepareExecutorPreviews(int? recipient, int months,
        int? targetCity, DiplomacyOffer given, DiplomacyOffer requested, IReadOnlyList<int> captives, bool targetValid)
    {
        bool terms = Kind == DiplomaticMissionKind.DemandSurrender || given.Gold != 0 || given.Supplies != 0;
        if (!terms && given.ItemId is int itemId)
        {
            var item = current.ReadRegistryTableReference(RegistryKind.Item, itemId) ?? throw Missing("executor_item_pointer");
            terms = Need(current.ReadRegistryObjectNativeValidity(item), "executor_item_validity") && item.PhysicalSlot is >= 1 and <= 201;
        }
        if (Kind == DiplomaticMissionKind.Alliance && months == 0 ||
            Kind == DiplomaticMissionKind.RequestAttack && targetCity is null ||
            Kind == DiplomaticMissionKind.ReturnCaptives && captives.Count == 0) terms = false;
        var previews = new Dictionary<int, DiplomacyExecutorPreview>();
        var travelByHome = new Dictionary<int, AreaAdministrationQuery<int>>();
        foreach (int id in Executors)
        {
            AreaAdministrationQuery<bool> prediction;
            try
            {
                var result = terms && targetValid
                    ? Kind == DiplomaticMissionKind.Exchange
                        ? ExchangeAdvice(id, recipient!.Value, given, requested)
                        : Advice(id, recipient!.Value, months, targetCity, given, captives)
                    : null;
                prediction = AreaAdministrationQuery<bool>.Known(result?.PositivePrediction ?? false);
            }
            catch (InvalidDataException error) { prediction = AreaAdministrationQuery<bool>.Unknown(error.Message); }
            AreaAdministrationQuery<int> travel;
            if (!targetValid) travel = AreaAdministrationQuery<int>.Known(0);
            else
            {
                int home = homes[id];
                if (!travelByHome.TryGetValue(home, out travel!))
                {
                    try { travel = AreaAdministrationQuery<int>.Known(ExecutorHomeTravel(home, recipient!.Value)); }
                    catch (InvalidDataException error) { travel = AreaAdministrationQuery<int>.Unknown(error.Message); }
                    travelByHome.Add(home, travel);
                }
            }
            previews.Add(id, new(prediction, travel));
        }
        return previews;
    }

    private int ExecutorHomeTravel(int home, int recipient)
    {
        //745D00 caches the Home Base -> recipient capital route. It neither
        //uses the moving person's current Hex nor canonicalizes the endpoints.
        var startBase = BaseLookup.Find(home, current);
        var endBase = qualification.Capital(recipient);
        HexReference Hex(BaseReference location)
        {
            int raw = current.ReadBaseCurrentHexIndex(location);
            return current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0) ?? throw Missing("executor_route_Hex");
        }
        var own = current.ReadForceTableReference(ForceId) ?? throw Missing("executor_route_Force");
        var output = current.CreateTemporaryHexList() ?? throw Missing("executor_route_list");
        try
        {
            var result = PersonReturnPathSource.Fill(State, current, Hex(startBase), Hex(endBase), own, output);
            return Need(result.Days, result.UnresolvedOwner ?? "executor_route_days");
        }
        finally { current.ClearTemporaryHexList(output); current.ReleaseTemporaryHexList(output); }
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal DiplomacyExecutorSelection ReadDiplomacyExecutorSelectionCurrent(GameState state, SourceWorldContext current,
        int forceId, DiplomaticMissionKind kind, IReadOnlyList<int> candidates,
        IReadOnlyDictionary<int, DiplomacyExecutorPreview> previews)
    {
        var reader = new OfficerInformationCurrent(this, state, forceId, rowPaletteEnabled: true, retainedCurrent: current);
        return reader.DiplomacyExecutorPages(kind, candidates, previews);
    }

    internal int? ReadDiplomacyPreferredExecutorCurrent(GameState state, SourceWorldContext current,
        int forceId, IReadOnlyList<int> candidates, IReadOnlyDictionary<int, DiplomacyExecutorPreview> previews)
    {
        //6B97D0 invokes6A1960 only for at least two candidates. The sole
        //candidate still receives the full advice/travel preparation above.
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];
        var reader = new OfficerInformationCurrent(this, state, forceId, rowPaletteEnabled: true, retainedCurrent: current);
        return reader.OrderDiplomacyExecutors(candidates, previews)[0];
    }

    private sealed partial class OfficerInformationCurrent
    {
        private IReadOnlyDictionary<int, DiplomacyExecutorPreview>? diplomacyExecutorCells;

        internal DiplomacyExecutorSelection DiplomacyExecutorPages(DiplomaticMissionKind kind, IReadOnlyList<int> candidates,
            IReadOnlyDictionary<int, DiplomacyExecutorPreview> previews)
        {
            diplomacyExecutorCells = previews; commandBodyPalette = true;
            var ids = candidates.ToArray(); string? initialMissing = null;
            try
            {
                ids = OrderDiplomacyExecutors(ids, previews);
            }
            catch (InvalidDataException error) { initialMissing = error.Message; }
            var pages = Pages(ids, Data(1, 0x58F), DiplomacyExecutorColumnsSource.Create(kind, Data));
            var health = ids.ToDictionary(id => id, MovementAlteredHealth);
            return new(pages, health, initialMissing);
        }

        internal int[] OrderDiplomacyExecutors(IReadOnlyList<int> candidates,
            IReadOnlyDictionary<int, DiplomacyExecutorPreview> previews)
        {
            var keys = candidates.ToDictionary(id => id, id => (Positive: ExecutorValue(previews[id].Positive),
                Days: ExecutorValue(previews[id].Days), Politics: Need(Abilities.ResolveDimension(state, id, 3,
                    (force, effect) => rules._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "executor_politics")));
            return candidates.OrderByDescending(id => keys[id].Positive).ThenBy(id => keys[id].Days)
                .ThenByDescending(id => keys[id].Politics).ThenBy(id => id).ToArray();
        }

        private PreparedCell DiplomacyExecutorCell(Person person, int column)
        {
            int Days() => ExecutorValue(diplomacyExecutorCells![person.Id].Days);
            bool Positive() => ExecutorValue(diplomacyExecutorCells![person.Id].Positive);
            bool HasTrait()
            {
                for (int slot = 0; slot < 9; slot++)
                    if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person.PhysicalSlot, slot),
                        "executor_trait_slot") == 121) return true;
                return false;
            }
            return column switch
            {
                75 => MakeCell(() => Positive() ? Data(1, 0x338) : "", () => new NumberKey(Positive() ? 1 : 0)),
                105 => MakeCell(() => HasTrait() ? Data(1, 0x338) : "", () => new NumberKey(HasTrait() ? 1 : 0)),
                102 => MakeCell(() => Number(Days()), () => new NumberKey(InformationNativeValid(person) ? Days() : int.MaxValue),
                    statePalette: () => BasicRowPalette(person, 5)),
                _ => throw Missing("executor_column")
            };
        }

        private static T ExecutorValue<T>(AreaAdministrationQuery<T> query) where T : struct
            => query.Available ? query.Value : throw new InvalidDataException(query.UnresolvedInput);
    }
}
