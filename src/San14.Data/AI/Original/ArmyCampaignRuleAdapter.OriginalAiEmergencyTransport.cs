using San14.Core;

namespace San14.Data;

/// <summary>The actual Force post-processing transport request. Its source
/// is a City or Gate, never a Gate identity disguised as a City.</summary>
internal sealed record OriginalAiEmergencyTransportRequest(int ForceId, int SourceLocationId,
    int OfficerId, int DistrictId, int DestinationLocationId, int Troops, int Gold, int Food,
    int NavalFormationId);

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState CreateOriginalAiEmergencyTransportCurrent(GameState state, SourceWorldContext current,
        OriginalAiEmergencyTransportRequest request)
    {
        var before = state;
        var packet = TransportArmyPacketSource.TryComposeEmergency(state, current, request);
        if (packet.Packet is not { } selected) throw AiForceStrategyMissing(packet.UnresolvedOwner ?? "transport_packet");
        state = ArmyIdentityAllocation.Initialize(state);
        var source = selected.SourceBase;
        int start = current.ReadBaseCurrentHexIndex(source);
        var target = BaseLookup.Find(request.DestinationLocationId, current);
        int goal = current.ReadBaseCurrentHexIndex(target);
        byte morale = AiForceStrategyKnown(current.ReadBaseMorale44(source), "transport_source_morale");
        int id = state.ArmyIdentities!.Candidate;
        var projection = new UnitState(id, request.ForceId, request.OfficerId, start, goal,
            request.Troops, request.Food, new(morale, null))
        {
            SourceLocationId = request.SourceLocationId,
            Mission = new(UnitMissionKind.Transport, target.Kind == BaseKind.City ? request.DestinationLocationId : null, request.Gold)
        };
        string? missing = null;
        bool noAssignableReceiver = false;
        var created = CreateTransportCore(state, request.OfficerId, request.SourceLocationId, projection,
            next => TransportArmyPacketSource.TryComposeEmergency(state, next, request), value => missing = value,
            () => noAssignableReceiver = true);
        if (noAssignableReceiver) return before;
        var accepted = created?.State ?? throw AiForceStrategyMissing(missing ?? "transport_creation");
        //The ordinary creation writer debits its selected District domain
        //owner. The Force host's next source read observes that same byte14.
        int books = accepted.Districts.GetValueOrDefault(request.DistrictId)?.OrdersRemaining
            ?? throw AiForceStrategyMissing("transport_payment_books");
        var saved = accepted.NativeForceUnregisterCurrent ?? throw AiForceStrategyMissing("transport_payment_owner");
        var window = saved.DistrictWindows10.GetValueOrDefault(request.DistrictId)?.ToArray()
            ?? throw AiForceStrategyMissing("transport_payment_window");
        if (window.Length <= 4) throw AiForceStrategyMissing("transport_payment_window_format");
        window[4] = checked((byte)books);
        return accepted with { NativeForceUnregisterCurrent = saved with
            { DistrictWindows10 = new(saved.DistrictWindows10) { [request.DistrictId] = window } } };
    }
}

public static partial class TransportArmyPacketSource
{
    internal static TransportArmyPacketResult TryComposeEmergency(GameState state, SourceWorldContext current,
        OriginalAiEmergencyTransportRequest request)
    {
        try
        {
            if (request.Troops is < 0 or > ushort.MaxValue || request.Gold is < 0 or > 100000 ||
                request.Food is < 0 or > 5000000 || request.NavalFormationId is < 0 or > 20 ||
                request.Troops == 0 && request.Gold == 0 && request.Food == 0)
                return Missing("original_ai.force_strategy.transport_quantities");
            int person = current.ReadPersonLookupReference(request.OfficerId)?.PhysicalSlot
                ?? throw new InvalidDataException("original_ai.force_strategy.transport_person_pointer");
            if (current.ReadPersonNativeValidity(person) != true || current.ReadPersonStatus(person) is not (>= 1 and <= 4) ||
                current.ReadPersonFirstLocation(person) != request.SourceLocationId ||
                current.ReadPersonDistrictSlot(person) != request.DistrictId)
                return Missing("original_ai.force_strategy.transport_person_selection");
            var force = current.ReadPersonForce(person)
                ?? throw new InvalidDataException("original_ai.force_strategy.transport_force_pointer");
            if (force.PhysicalSlot != request.ForceId || current.ReadForceNativeValidity(force) != true)
                return Missing("original_ai.force_strategy.transport_force_scope");
            var source = BaseLookup.Find(request.SourceLocationId, current);
            var target = BaseLookup.Find(request.DestinationLocationId, current);
            if (current.ReadBaseNativeValidity(source) != true || current.ReadBaseNativeValidity(target) != true ||
                current.ReadBaseForceSlot(source) != request.ForceId || current.ReadBaseForceSlot(target) != request.ForceId || source == target)
                return Missing("original_ai.force_strategy.transport_actual_bases");
            var currency = BaseCurrencyAdditionSource.CurrencyCity(source, current);
            int currencyId = currency.PhysicalSlot
                ?? throw new InvalidDataException("original_ai.force_strategy.transport_currency_city");
            int targetId = target.PhysicalSlot
                ?? throw new InvalidDataException("original_ai.force_strategy.transport_target_pointer");
            return new(new(request, person, source, currencyId, target.Kind == BaseKind.City ? (byte)2 : (byte)3,
                target.Kind == BaseKind.City ? (byte)5 : (byte)6, targetId), null);
        }
        catch (InvalidDataException error) { return Missing(error.Message); }
    }
}

internal static partial class ArmyRouteSearch
{
    //0C21E0: actual Force, null Army descriptor, mode1, flags8, pool2.
    //Temporary native search storage has no rule or persistent identity here.
    internal static bool OriginalAiEmergencyTransportCanReach(GameState state, SourceWorldContext current,
        int force, int sourceHex, int targetHex)
    {
        if ((uint)sourceHex >= Count || (uint)targetHex >= Count) return false;
        var query = new Queries(state, current, new CampaignMovement(force), sourceHex, targetHex,
            0, 0, null, 8, hasArmyDescriptor: false);
        try { return Search(query, out _, out _, hasArmyDescriptor: false).Length != 0; }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }
}
