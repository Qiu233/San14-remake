using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignAllRetreatArmy(int UnitId, int ArmyPhysicalSlot,
    int ForceId, int OfficerId, string OfficerName, int Troops, int HexId);

/// <summary>One enabled expedition controller entry. Withdrawing entries stay
/// in the list with the native disabled hint; unknown totals are never zero.</summary>
public sealed record CampaignAllRetreatRow(int CampaignId, int? ForceId,
    int ProvinceId, string? ProvinceName, int SequenceNumber, int StartYear,
    int StartMonth, bool IsWithdrawing, bool Enabled, int? DisabledMessageId,
    int? ArmyCount, int? Troops, IReadOnlyList<CampaignAllRetreatArmy> Armies,
    string? UnresolvedOwner);

public sealed record CampaignAllRetreatPage(int ForceId, int? DistrictId,
    int? CommandBooks, int? CommandBookCost, int? CommandBooksAfter,
    IReadOnlyList<CampaignAllRetreatRow> Rows, string? UnresolvedOwner)
{
    public bool Available => UnresolvedOwner is null;
    public string? ConfirmationText { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>734770's actual Campaign list. One current capture on page
    /// entry, one manager60 pass for display totals, no world or selection write.</summary>
    public CampaignAllRetreatPage QueryCampaignAllRetreatPage(GameState state, int forceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        int? districtId = null, books = null, cost = null, after = null;
        string? confirmationText = null;
        var rows = new List<CampaignAllRetreatRow>();
        try
        {
            var owner = state.CampaignExpeditions ?? throw CampaignRetreatPageMissing("controller_owner");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw CampaignRetreatPageMissing("current_player");
            if (current.ReadForcePhysicalSlot(player) != forceId ||
                !CampaignRetreatPageNeed(current.ReadForceNativeValidity(player), "player_validity") ||
                state.Forces.GetValueOrDefault(forceId) is not { IsActive: true })
                throw CampaignRetreatPageMissing("current_player_control");
            if (state.Phase != TurnPhase.Strategy) throw CampaignRetreatPageMissing("strategy_phase");

            var primary = ForcePrimaryDistrictSource.Resolve(current, player);
            districtId = CampaignRetreatPageNeed(primary.PhysicalSlot, "primary_District");
            if (!CampaignRetreatPageNeed(current.ReadDistrictNativeValidity(primary), "District_validity") ||
                state.Districts.GetValueOrDefault(districtId.Value) is not { } district || district.ForceId != forceId)
                throw CampaignRetreatPageMissing("District_control");
            books = district.OrdersRemaining;
            string? pageMissing = books.HasValue ? null : CampaignRetreatPageMissing("District14").Message;
            if (books is int currentBooks)
            {
                try
                {
                    confirmationText = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages)
                        .ResolveCampaignConfirmation(new CampaignConfirmationContext(currentBooks));
                }
                catch (InvalidDataException error) { pageMissing ??= error.Message; }
            }
            try
            {
                //The same current fee as formal RetreatCampaignsOrder. The
                //confirm dialog's literal books-1 is not a business quote.
                cost = ReadCampaignAllRetreatBookCostCurrent(state);
                if (books.HasValue) after = Math.Clamp(unchecked(books.Value - cost.Value), 0, 200);
            }
            catch (InvalidDataException error) { pageMissing ??= error.Message; }

            var expeditions = new List<CampaignExpedition>();
            //The source table has51 entries. Physical0 is native-invalid;
            //known absent records are reset/free, not hidden started entries.
            for (int physical = 1; physical <= 50; physical++)
                if (owner.Expeditions.TryGetValue(physical, out var expedition))
                    expeditions.Add(expedition);
            var members = expeditions.ToDictionary(row => row.Id, _ => new List<CampaignAllRetreatArmy>());
            string? memberMissing = null;
            if (expeditions.Count != 0)
            {
                try
                {
                    var arena = state.ReadArmyLifecycle() ?? throw CampaignRetreatPageMissing("Army_lifecycle");
                    foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army)
                        ?? throw CampaignRetreatPageMissing("manager60"))
                    {
                        if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical } ||
                            (uint)physical >= arena.Slots.Count)
                            throw CampaignRetreatPageMissing("manager60_Army");
                        //Reuse the formal execution owner's active/pending
                        //image join and its same independent Army13C owner.
                        var image = CampaignRetreatArmyImage(state, physical);
                        var source = arena.Slots[physical];
                        int campaignId = CampaignRetreatPageNeed(image.NativeMapUiByte13C ?? source.MapUiByte13C, "Army13C");
                        if (!members.TryGetValue(campaignId, out var group)) continue;
                        var army = new ArmyUnitReference(physical);
                        int rawForce = current.ReadArmyUnitRawForceSlot(army);
                        int commander = CampaignRetreatPageNeed(current.ReadArmyUnitCommanderId(army), "Army12");
                        var person = state.Officers.GetValueOrDefault(commander)
                            ?? throw CampaignRetreatPageMissing("Commander_owner");
                        int hex = CampaignRetreatPageNeed(current.ReadArmyUnitRawHex2A(army), "Army2A");
                        group.Add(new(image.Id, physical, rawForce, commander, person.Name, image.Troops, hex));
                    }
                }
                catch (InvalidDataException error) { memberMissing = error.Message; }
            }
            foreach (var expedition in expeditions)
            {
                string? name = null, rowMissing = memberMissing;
                try
                {
                    var states = _growthQueries.Definitions.Catalog.States;
                    if ((uint)expedition.ProvinceId >= states.Count || states[expedition.ProvinceId].RawBytes.Length < 30)
                        throw CampaignRetreatPageMissing("Province_name");
                    name = S14ForceNameDefinitionReader.ReadLiteralUnits(states[expedition.ProvinceId].RawBytes.AsSpan(24, 6));
                }
                catch (InvalidDataException error) { rowMissing ??= error.Message; }
                var group = members[expedition.Id];
                //Campaign10 is Province, not Force. Only actual membership can
                //establish a unique Force; an empty/mixed group retains null.
                int[] forces = group.Select(army => army.ForceId).Distinct().Take(2).ToArray();
                int? groupForce = memberMissing is null && forces.Length == 1 ? forces[0] : null;
                int? total = memberMissing is null ? group.Aggregate(0, (sum, army) => unchecked(sum + army.Troops)) : null;
                rows.Add(new(expedition.Id, groupForce, expedition.ProvinceId, name, expedition.SequenceNumber,
                    expedition.StartYear, expedition.StartMonth, expedition.IsWithdrawing, !expedition.IsWithdrawing,
                    expedition.IsWithdrawing ? 0x5828 : null, memberMissing is null ? group.Count : null,
                    total, group.AsReadOnly(), rowMissing));
            }
            return new(forceId, districtId, books, cost, after, rows.AsReadOnly(), pageMissing)
                { ConfirmationText = confirmationText };
        }
        catch (InvalidDataException error)
        { return new(forceId, districtId, books, cost, after, rows.AsReadOnly(), error.Message)
            { ConfirmationText = confirmationText }; }
    }

    private static T CampaignRetreatPageNeed<T>(T? value, string field) where T : struct =>
        value ?? throw CampaignRetreatPageMissing(field);
    private static InvalidDataException CampaignRetreatPageMissing(string field) => new("campaign.all-retreat.page." + field);
}
