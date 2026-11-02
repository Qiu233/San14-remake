using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record ArmyReturnCaptiveSelectionPublication(OfficerInformationPageSet Pages,
    IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth,
    string DestinationName, int DestinationGold);

public sealed partial class ArmyCampaignRuleAdapter
{
    public static int ReturnCaptiveMonthlyPay(string language) =>
        NativeMonthlySalaryRules.ForLanguage(language).CaptivePay;

    /// <summary>The actual pending candidate supplies both list and summary.
    /// The page borrows this publication; it never reads the older committed
    /// session World or replaces recruitment advice while painting.</summary>
    public AreaAdministrationQuery<ArmyReturnCaptiveSelectionPublication> QueryArmyReturnCaptiveSelection(
        GameState state, int playerForceId)
    {
        try
        {
            var pending = state.ArmyReturnCaptiveContinuation;
            if (pending?.PendingSelection is null || pending.CapturingForceId != playerForceId)
                throw PlayerReturnCaptiveMissing("selection_owner");
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var target = pending.Destination.Kind == SettlementBaseKind.City
                ? new BaseReference(BaseKind.City, pending.Destination.Id)
                : new BaseReference(BaseKind.Gate, pending.Destination.Id - 51);
            if (current.ReadBaseNativeValidity(target) != true || current.ReadBaseForceSlot(target) != playerForceId)
                throw PlayerReturnCaptiveMissing("selection_destination");
            var reader = new OfficerInformationCurrent(this, state, playerForceId,
                rowPaletteEnabled: false, retainedCurrent: current);
            var pages = reader.ReturnCaptivePages(pending.RemainingOfficerIds);
            return AreaAdministrationQuery<ArmyReturnCaptiveSelectionPublication>.Known(new(pages,
                pending.RemainingOfficerIds.ToDictionary(id => id, id => reader.MovementAlteredHealth(id)),
                UnitDetailBaseName(state, current, target.Kind,
                    target.PhysicalSlot ?? throw PlayerReturnCaptiveMissing("selection_Base_pointer")),
                //The original Base.v90 reads City34. A Gate delegates to its
                //actual linked City through the same currency-owner query.
                current.ReadBaseResource(BaseCurrencyAdditionSource.CurrencyCity(target, current), BaseResourceKind.Gold)
                    ?? throw PlayerReturnCaptiveMissing("selection_Base_gold")));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<ArmyReturnCaptiveSelectionPublication>.Unknown(error.Message); }
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal OfficerInformationPageSet ReturnCaptivePages(IReadOnlyList<int> ids)
        {
            //The exact table has one group and one name column135. The
            //ordinary three information tabs belong to the detail pane.
            var column = new OfficerInformationColumn(135, 0x3EC, Data(1, 0x3EC), 132, 1, true, true, true);
            ImmutableArray<OfficerInformationGroup> groups =
                [new(0, 0x3CD, Data(1, 0x3CD), [column])];
            PreparedCell Name(int id, int columnId)
            {
                if (columnId != 135) throw Missing("return_captive_column");
                if (prepared.TryGetValue((id, columnId), out var saved)) return saved;
                var person = ReadPerson(id);
                bool Valid() => Need(current.ReadPersonNativeValidity(person.PhysicalSlot), "return_captive_name_validity");
                return prepared[(id, columnId)] = MakeCell(
                    () => Valid() ? Cells.Name(person.Candidate, false) : "",
                    () => Valid() ? new NameKey(Cells.Name(person.Candidate, true)) : new NameKey("", true));
            }
            return new(Data(1, 0x3EC), groups, ids,
                (id, col) => Name(id, col).Display with { SortUnresolvedOwner = Name(id, col).SortUnresolvedOwner },
                (id, col) => Name(id, col).SortUnresolvedOwner,
                (left, right, col, ascending) => (Name(left, col).Key ?? throw Missing("return_captive_sort_key"))
                    .Before(Name(right, col).Key ?? throw Missing("return_captive_sort_key"), ascending))
                { DynamicPaletteEnabled = false };
        }
    }

    private static InvalidDataException PlayerReturnCaptiveMissing(string field) => new("return.player-captives." + field);
}
