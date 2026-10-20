using San14.Core;

namespace San14.Data;

/// <summary>Ordered semantic application work, staged with the numeric manager.
/// There is no simulated view, admission decision, application RNG or ACK.</summary>
internal sealed class ArmyTraitFeedback(GameState input, SourceWorldContext current,
    NativeArmyProgressStageMoment moment, List<CampaignEffectRequest>? orderedSink = null,
    Func<GameState>? currentState = null, Action? bindPersonnel = null)
{
    private readonly List<CampaignEffectRequest> requests = orderedSink ?? [];
    private bool personnelBound;

    internal void Refresh(ArmyUnitReference source)
        => requests.Add(new(null, null) { ArmyTraitViewRefresh = new(ArmyId(source)) });

    internal void RefreshAndFeedback(ArmyUnitReference source, PersonalityReference trait)
    {
        Refresh(source);
        if (!Need(current.ReadSkillPersonalityValidity(trait), "trait_validity")) return;
        int key = trait.PhysicalSlot ?? throw Missing("trait_identity");
        bool contextRequired = ArmyTraitFeedbackRequest.HasContext(key);
        if (!contextRequired && key != 20) return;
        int? context = null;
        if (contextRequired)
        {
            ushort raw = Need(current.ReadArmyUnitRawHex2A(source), "context_Army_Hex");
            var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("context_Hex_pointer");
            context = hex.PhysicalSlot.HasValue ? current.ReadHexPhysicalSlot(hex) ?? throw Missing("context_Hex_identity") : 0;
        }
        //1B4AB0 rejects invalid Armies before any Person/position getter.
        if (!Need(current.ReadArmyUnitNativeValidity(source), "speech_Army_validity")) return;
        if (!personnelBound)
        {
            if (bindPersonnel is not null) bindPersonnel();
            else
            {
                var latest = currentState?.Invoke() ?? input;
                if (latest.PersonnelCommands is null) throw Missing("personnel_manager");
                PersonReturnCommands.BindCurrent(latest, current);
            }
            personnelBound = true;
        }
        ushort rawCommander = Need(current.ReadArmyUnitCommanderId(source), "speech_Commander");
        var person = rawCommander <= 6000 ? current.ReadPersonLookupReference(rawCommander) : current.ReadTransferPersonTableReference(0);
        int physical = person?.PhysicalSlot ?? throw Missing("speech_Person_pointer");
        int position = PersonReturnCommands.ResolvePersonPosition(currentState?.Invoke() ?? input,
            current, physical, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
        int officer = Need(current.ReadPersonIdentity(physical), "speech_Person_identity");
        requests.Add(new(null, null) { ArmyTraitFeedback = new(ArmyId(source), officer, position, key, context) });
    }

    internal GameState Publish(GameState state)
    {
        if (personnelBound) state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw Missing("personnel_publication") };
        return requests.Count == 0 || orderedSink is not null ? state : state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw Missing("pending_owner"))
                .Append(CampaignEffectSource.ArmyTraitMaintenance, moment.CalendarCycleOrdinal, requests) };
    }
    private int ArmyId(ArmyUnitReference army)
    {
        int physical = army.PhysicalSlot ?? throw Missing("Army_pointer");
        var arena = (currentState?.Invoke() ?? input).ReadArmyLifecycle() ?? throw Missing("lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw Missing("Army_owner");
        return receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw Missing("Army_campaign_identity");
    }
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("army.trait-maintenance.feedback." + name);
}
