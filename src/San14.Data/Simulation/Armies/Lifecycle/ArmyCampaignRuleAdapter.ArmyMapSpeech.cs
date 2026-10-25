using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyMapSpeechRules
{
    private Func<GameState, int, bool?>? armyTraitSpeechView;

    /// <summary>Actual1A3D70 source-Army view availability. An unbound or
    /// unknown result retains the typed intention; it never means ready.</summary>
    public void ConfigureArmyTraitSpeechView(Func<GameState, int, bool?>? availability) => armyTraitSpeechView = availability;

    //1B4720: capacity and live-speaker exclusion precede the real admission
    //draw. A temporary admitted packet draws its delay even when an older
    //packet for that speaker wins the subsequent priority comparison.
    internal GameState QueueSceneryControlArmySpeechCurrent(GameState state, SourceWorldContext current,
        int armyId, int officerId, int hex, int scenery)
        => QueueArmyMapSpeechCurrent(state, current, armyId, officerId, 31, hex, sceneryId: scenery);

    internal GameState QueueArmyMapSpeechCurrent(GameState state, SourceWorldContext current,
        int armyId, int officerId, int actionCode, int hex, int? contextArgument = null, int? sceneryId = null,
        int? contextTraitId = null, int? contextAreaId = null)
    {
        //TC1B4030 / static1A490C8=100 and1A490D4=8. This is the ordinary
        //battle/scenery subset; unsupported actions retain their own intent.
        int probability = actionCode switch { 10 or 11 or 14 or >= 19 and <= 26 or 31 => 100, 18 or 27 => 8,
            _ => throw MapSpeechMissing("admission_action_" + actionCode) };
        var owner = state.ArmyMapSpeech ?? throw MapSpeechMissing("queue_owner");
        if (owner.Entries.Length >= 100 || owner.Entries.Any(entry => entry.OfficerId == officerId && entry.TextAtSource is not null)) return state;
        if ((actionCode is 26 or 27) && NarrationResourceMessages.ArmyTraitActivationRoot(actionCode,
            contextTraitId ?? throw MapSpeechMissing("admission_trait_operand")) is null) return state;
        //1B4030/1B4720 call the independent CRT rand(), not3FCF40's
        //App stream used later by text/voice selection. Both are saved owners.
        uint crt = (state.CampaignCrtRandom ?? throw MapSpeechMissing("CRT_random")).State;
        bool admitted = Draw(100) < probability;
        if (!admitted) return state with { CampaignCrtRandom = new(crt) };
        byte delay = checked((byte)Draw(10));
        var entries = owner.Entries.ToBuilder();
        int old = -1;
        for (int index = 0; index < entries.Count; index++)
            if (entries[index].OfficerId == officerId) { old = index; break; }
        long next = owner.NextSequence;
        if (old >= 0)
        {
            var retained = entries[old];
            if (actionCode < retained.ActionCode)
                entries[old] = retained with { ArmyId = armyId, ActionCode = actionCode, HexId = hex,
                    SceneryId = sceneryId, ContextBaseLocationId = actionCode is 11 or 18 ? contextArgument : null,
                    ContextTraitId = contextTraitId, ContextAreaId = contextAreaId };
            //The temporary delayed entry is discarded even if the new packet
            //does not win. The existing delay/lifetime are never restarted.
        }
        else
        {
            entries.Add(new(next, armyId, officerId, actionCode, hex, sceneryId, delay, 0, null)
                { ContextBaseLocationId = actionCode is 11 or 18 ? contextArgument : null,
                    ContextTraitId = contextTraitId, ContextAreaId = contextAreaId });
            next = checked(next + 1);
        }
        return state with { CampaignCrtRandom = new(crt), ArmyMapSpeech = new(next, entries.ToImmutable()) };

        int Draw(int modulus)
        {
            crt = unchecked(crt * 0x343FDu + 0x269EC3u);
            return (int)((crt >> 16) & 0x7FFFu) % modulus;
        }
    }

    public ArmyMapSpeechStartResult Start(GameState state, long sequence, bool armyLayoutAvailable)
    {
        try
        {
            var owner = state.ArmyMapSpeech ?? throw MapSpeechMissing("start_owner");
            var entry = owner.Entries.FirstOrDefault(item => item.Sequence == sequence) ?? throw MapSpeechMissing("start_entry");
            if (entry.TextAtSource is not null) return new(state, true);
            if (entry.DelayFrames != 0) throw MapSpeechMissing("start_delay_pending");
            if (owner.Entries.Any(item => item.Sequence != sequence && item.OfficerId == entry.OfficerId && item.TextAtSource is not null) ||
                !armyLayoutAvailable) return new(Remove(state, sequence), false);
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            //1B2E50 rejects a reset Army even if the application still owns a
            //retiring render node. A delayed defeat line never postpones the
            //actual pending70/282580 lifecycle just to keep a speech layout.
            if (entry.ActionCode is not (11 or 14))
            {
                if (ArmyDestructionQueue.ResolveUnitSlot(state, entry.ArmyId) is not int armyPhysical)
                    return new(Remove(state, sequence), false);
                if (!MapSpeechNeed(current.ReadArmyUnitNativeValidity(new(armyPhysical)), "start_Army_validity"))
                    return new(Remove(state, sequence), false);
            }
            var opening = entry.ActionCode == 25 ? PrepareArmyMapSpeechOpeningVoiceCurrent(state, current, entry)
                : new HistoryTextResult(state, "");
            int person = MapSpeechNeed(current.ReadPersonLookupReference(entry.OfficerId <= 6000 ? entry.OfficerId : 0)?.PhysicalSlot, "speaker_lookup");
            if (!MapSpeechNeed(current.ReadPersonNativeValidity(person), "speaker_validity")) return new(Remove(state, sequence), false);
            int root;
            var operands = new HistoryMessageOperands(Person0: entry.OfficerId);
            switch (entry.ActionCode)
            {
                case 10: root = 0xC47F; break;
                case 11: root = 0xC4E8; operands = operands with
                    { Base0 = checked((ushort)MapSpeechNeed(entry.ContextBaseLocationId, "capture_Base_operand")) }; break;
                case 14: root = 0xC4AB; break;
                case 18: root = 0xC4E9; operands = operands with
                    { Base0 = checked((ushort)MapSpeechNeed(entry.ContextBaseLocationId, "lost_Base_operand")) }; break;
                case 19: root = 0xC4F4; break;
                case 20: root = 0xC7C3; break;
                case 21: root = 0xC7CD; break;
                case 22: root = 0xC7D7; break;
                case 23: root = 0xC4C3; break;
                case 24: root = 0xC4CF; break;
                case 25: root = 0xC4DB; break;
                case 26:
                case 27:
                    int trait = MapSpeechNeed(entry.ContextTraitId, "trait_activation_operand");
                    root = NarrationResourceMessages.ArmyTraitActivationRoot(entry.ActionCode, trait)
                        ?? throw MapSpeechMissing("trait_activation_root");
                    if (entry.ActionCode == 26 && trait == 65)
                        operands = operands with { Area0 = MapSpeechNeed(entry.ContextAreaId, "trait_activation_Area") };
                    break;
                case 31:
                    root = 0xC829;
                    current.MaterializeFixedScenery(_landmarkDefinitions);
                    int raw = entry.SceneryId ?? throw MapSpeechMissing("Scenery_argument");
                    var scenery = current.ReadSceneryTableReference(raw is >= 0 and <= 100 ? raw : 0) ?? throw MapSpeechMissing("Scenery_lookup");
                    if (!MapSpeechNeed(current.ReadSceneryNativeValidity(scenery), "Scenery_validity")) return new(Remove(state, sequence), false);
                    int physical = MapSpeechNeed(scenery.PhysicalSlot, "Scenery_physical");
                    operands = operands with { String0 = current.SceneryDefinitions?.Sceneries[physical].Name
                        ?? throw MapSpeechMissing("Scenery_name") };
                    break;
                default: throw MapSpeechMissing("action_consumer_" + entry.ActionCode);
            }
            //1BC3C0 selects text only when the real Army layout is available.
            //This is the only once-selected text/App transition.
            var text = FormatArmyMapSpeechReportCurrent(opening.State, current, root, operands);
            if (text.Text.Length == 0) return new(Remove(text.State, sequence), false);
            var entries = owner.Entries.ToBuilder();
            int index = entries.IndexOf(entry);
            entries[index] = entry with { TextAtSource = text.Text, ElapsedSeconds = 0,
                SpatialVoices = opening.SpatialVoices.Concat(text.SpatialVoices).Select(request => new ArmyMapSpeechSpatialVoiceRequest(
                    SaveVoice(request.Voice), request.SourceX, request.SourceY, request.SourceZ)).ToImmutableArray(),
                SpatialVoicesAccepted = false };
            return new(text.State with { ArmyMapSpeech = owner with { Entries = entries.ToImmutable() } }, true);
        }
        catch (InvalidDataException error) { return new(state, false, error.Message); }
    }

    //A trait writer borrows its enclosing operation's latest state. Handled
    //means the real admission ran (including a legitimate rejection), not that
    //a view or speech has already played. Other action families stay pending.
    internal Func<ArmyTraitFeedbackRequest, bool> CreateArmyTraitSpeechConsumer(SourceWorldContext current,
        Func<GameState> latest, Action<GameState> publish) => request =>
    {
        if (request.Origin == ArmyTraitFeedbackOrigin.Personality)
        {
            int action = ArmyTraitFeedbackRequest.HasContext(request.TraitId) ? 26 : request.TraitId == 20 ? 27 : 0;
            if (action == 0) return true; //The actual1B3DA0 support gate rejected both actions.
            if (armyTraitSpeechView?.Invoke(latest(), request.ArmyId) is not bool available) return false;
            if (!available) return true; //Known no source view:1A3D70 never calls speech admission.
            publish(QueueArmyMapSpeechCurrent(latest(), current, request.ArmyId, request.OfficerId,
                action, request.SpeakerHexId, contextTraitId: request.TraitId,
                contextAreaId: request.ContextHexId));
        }
        else
        {
            if (request.Origin != ArmyTraitFeedbackOrigin.Action || request.TraitId is < 19 or > 25) return false;
            publish(QueueArmyMapSpeechCurrent(latest(), current, request.ArmyId, request.OfficerId,
                request.TraitId, request.SpeakerHexId));
        }
        return true;
    };

    private static GameState Remove(GameState state, long sequence)
    {
        var owner = state.ArmyMapSpeech ?? throw MapSpeechMissing("remove_owner");
        return state with { ArmyMapSpeech = owner with { Entries = owner.Entries.Where(entry => entry.Sequence != sequence).ToImmutableArray() } };
    }
    private static T MapSpeechNeed<T>(T? value, string field) where T : struct => value ?? throw MapSpeechMissing(field);
    private static InvalidDataException MapSpeechMissing(string field) => new("army-map-speech." + field);
}
