using San14.Core;

namespace San14.Data;

/// <summary>Checks compatibility links at resource/save admission. Ordinary
/// receiver lifecycle rules only use campaign identities and phases.</summary>
internal static class ArmyReceiverSourceValidation
{
    internal static void Validate(GameState state)
    {
        var source = CampaignSourceKnowledge.From(state.ResourceState).ArmyLifecycleSource;
        if (source is null) return;
        var lifecycle = state.ReadArmyLifecycle()
            ?? throw new InvalidDataException("Army source knowledge has no campaign lifecycle owner.");
        if (string.IsNullOrWhiteSpace(source.Revision) || source.Language is not ("SC" or "TC") ||
            string.IsNullOrWhiteSpace(source.RuleVersion) || source.SourceScenarioSha256 is not { Length: 64 } hash ||
            !hash.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(source.SourceScenarioUri) ||
            state.ContentHashes.GetValueOrDefault(source.SourceScenarioUri) != hash ||
            state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != source.Language ||
            parameters.RuleVersion != source.RuleVersion || source.Slots.Count == 0 ||
            source.ArmyFlags150 is not null && (source.Slots.Count != 501 || source.ArmyFlags150.Count != 501))
            throw new InvalidDataException("Army source ownership is missing or incompatible.");
        if (lifecycle.Slots.Count != lifecycle.Receivers.Entries.Count ||
            lifecycle.Slots.Select(slot => slot.ReceiverId).Distinct().Count() != lifecycle.Slots.Count)
            throw new InvalidDataException("Army source receiver directory disagrees with the campaign roster.");
        int ordinal = 0;
        foreach (var slot in lifecycle.ReceiverSlots())
        {
            if (slot.PhysicalSlot != ordinal++ ||
                slot.AuxiliaryAC is { Count: not 36 } || slot.AuxiliaryF4 is { Count: not 36 })
                throw new InvalidDataException("Army source receiver fields are malformed.");
            if (slot.Receiver.CanAssign != (slot.PhysicalSlot != 0))
                throw new InvalidDataException("Army source placeholder cannot be assigned.");
            var unit = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
            if (unit is not null)
            {
                if (unit.NativeArmyOrigin?.NativeSlotIndex != slot.PhysicalSlot ||
                    slot.Commander12 != unit.OfficerId || slot.Status10 != unit.NativeTacticRuntime?.Status10)
                    throw new InvalidDataException("Army source header does not identify its owned campaign unit.");
            }
            else if (slot.Receiver.Phase != ArmyLifecycleReceiverAdapter.DecodeEmptyPhase(slot.Commander12, slot.Status10))
                throw new InvalidDataException("Army source readiness contradicts the imported campaign receiver.");
        }
    }
}
