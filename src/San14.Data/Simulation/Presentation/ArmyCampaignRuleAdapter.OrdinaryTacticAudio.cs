using San14.Core;

namespace San14.Data;

/// <summary>Unadopted application-random state and source-selected requests.
/// The actual audio batch owns reservations until the caller adopts State's
/// App32 through the ordinary-node begin transaction, then commits the batch.</summary>
public sealed record OrdinaryTacticAudioPreparation(GameState State,
    IReadOnlyList<ArmyMapSpeechSpatialVoiceRequest> PendingSpatial, string? UnresolvedOwner = null)
{
    public bool Available => UnresolvedOwner is null;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>172C80/172D50's initial voices, primary then linked casters.
    /// These are actor voices, independent of either instruction's victims.
    /// Neither this preparation nor a successful admission starts a node or
    /// adopts random state; failures require the caller to abort its batch.</summary>
    public OrdinaryTacticAudioPreparation PrepareOrdinaryTacticAudio(GameState state,
        OrdinaryTacticPresentationPublication presentation)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(presentation);
        try
        {
            var manager = state.ManualTacticIntervention ?? throw TacticAudioMissing("manager");
            int next = manager.OrdinaryPlaybacks.Length;
            if (manager.OrdinaryPlaybacks.IsDefault || manager.Effects is null || next >= manager.Effects.Length ||
                manager.Effects[next].Sequence != presentation.Sequence ||
                manager.Effects[next].Kind != TacticPresentationKind.OrdinaryEffects)
                throw TacticAudioMissing("next_unstarted_manager_node");
            if (presentation.Actors.Count is < 1 or > 11)
                throw TacticAudioMissing("primary_and_ten_linked_actors");

            var latest = state;
            SourceWorldContext? current = null;
            ArmyLifecycleSourceView? lifecycle = null;
            NarrationVoiceRequests? voices = null;
            foreach (var entry in presentation.Actors)
            {
                //A Base5/6 header has no Army commander and makes no405AC0 call.
                if (entry.CasterSpatialVoiceId is not int voiceId) continue;
                var context = current ??= CaptureOwnedArmyReturnWorld(state).Context;
                lifecycle ??= state.ReadArmyLifecycle() ?? throw TacticAudioMissing("Army_receiver_owner");
                int physical = lifecycle.Slots.FirstOrDefault(slot => slot.ReceiverId == entry.Actor.Actor)?.PhysicalSlot
                    ?? throw TacticAudioMissing("Army_receiver_slot");
                var army = new ArmyUnitReference(physical);
                if (!(context.ReadArmyUnitNativeValidity(army) ?? throw TacticAudioMissing("Army_validity"))) continue;
                var tactic = context.ReadSkillTacticReference(entry.Actor.TacticId)
                    ?? throw TacticAudioMissing("Tactic_pointer");
                if (!(context.ReadSkillTacticValidity(tactic) ?? throw TacticAudioMissing("Tactic_validity"))) continue;
                int speaker = context.ReadArmyUnitCommanderId(army) ?? throw TacticAudioMissing("Army_commander");
                int personPhysical = context.ReadPersonLookupReference(speaker <= 6000 ? speaker : 0)?.PhysicalSlot
                    ?? throw TacticAudioMissing("Person_pointer");
                if (!(context.ReadPersonNativeValidity(personPhysical) ?? throw TacticAudioMissing("Person_validity"))) continue;
                int personId = context.ReadPersonIdentity(personPhysical) ?? throw TacticAudioMissing("Person_identity");
                var person = state.Officers.GetValueOrDefault(personId) ?? throw TacticAudioMissing("Person_owner");
                voices ??= new NarrationVoiceRequests(
                    _personnelNarrationVoice ?? throw TacticAudioMissing("voice_settings"), Draw,
                    spatialPosition: id => ReadArmyMapSpeechVoicePositionCurrent(latest, context, id),
                    spatialAdmission: request => (armyMapSpeechSpatialAdmission ?? throw TacticAudioMissing("application_admission_owner"))(
                        new(SaveVoice(request.Voice), request.SourceX, request.SourceY, request.SourceZ)));
                _ = voices.RequestTacticSpatial(person, voiceId);
            }
            IReadOnlyList<ArmyMapSpeechSpatialVoiceRequest> pending = voices is null ? [] :
                Array.AsReadOnly(voices.PendingSpatial.Select(request => new ArmyMapSpeechSpatialVoiceRequest(
                    SaveVoice(request.Voice), request.SourceX, request.SourceY, request.SourceZ)).ToArray());
            return new(latest, pending);

            int Draw(int bound)
            {
                if (bound < 2) return 0;
                var random = new SharedSourceRandom32((latest.CampaignAppRandom32 ?? throw TacticAudioMissing("App32")).State);
                int value = random.Next(bound);
                latest = latest with { CampaignAppRandom32 = new(random.State) };
                return value;
            }
        }
        catch (InvalidDataException error) { return new(state, [], error.Message); }
    }

    private static InvalidDataException TacticAudioMissing(string owner)
        => new("tactic.spatial-voice." + owner);
}
