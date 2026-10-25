using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyMapSpeechRules
{
    //1B4720's action31 uses the same capacity, live-speaker exclusion and
    //priority merge as other Army speeches. No presentation refresh calls it.
    internal GameState QueueSceneryControlArmySpeechCurrent(GameState state, SourceWorldContext current,
        int armyId, int officerId, int hex, int scenery)
    {
        var owner = state.ArmyMapSpeech ?? throw MapSpeechMissing("queue_owner");
        if (owner.Entries.Length >= 100 || owner.Entries.Any(entry => entry.OfficerId == officerId && entry.TextAtSource is not null)) return state;
        var random = new SharedSourceRandom32((state.CampaignAppRandom32 ?? throw MapSpeechMissing("App32")).State);
        //Do not optimize the threshold100 branch away: it still draws App.
        bool admitted = random.Next(100) < 100;
        if (!admitted) return state with { CampaignAppRandom32 = new(random.State) };
        byte delay = checked((byte)random.Next(10));
        var entries = owner.Entries.ToBuilder();
        int old = -1;
        for (int index = 0; index < entries.Count; index++)
            if (entries[index].OfficerId == officerId) { old = index; break; }
        long next = owner.NextSequence;
        if (old >= 0)
        {
            var retained = entries[old];
            if (31 < retained.ActionCode)
                entries[old] = retained with { ArmyId = armyId, ActionCode = 31, HexId = hex, SceneryId = scenery };
            //The temporary delayed entry is discarded even if the new packet
            //does not win. The existing delay/lifetime are never restarted.
        }
        else
        {
            entries.Add(new(next, armyId, officerId, 31, hex, scenery, delay, 0, null));
            next = checked(next + 1);
        }
        return state with { CampaignAppRandom32 = new(random.State), ArmyMapSpeech = new(next, entries.ToImmutable()) };
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
            if (entry.ActionCode != 31) throw MapSpeechMissing("action_consumer_" + entry.ActionCode);
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            current.MaterializeFixedScenery(_landmarkDefinitions);
            int person = MapSpeechNeed(current.ReadPersonLookupReference(entry.OfficerId <= 6000 ? entry.OfficerId : 0)?.PhysicalSlot, "speaker_lookup");
            if (!MapSpeechNeed(current.ReadPersonNativeValidity(person), "speaker_validity")) return new(Remove(state, sequence), false);
            int raw = entry.SceneryId ?? throw MapSpeechMissing("Scenery_argument");
            var scenery = current.ReadSceneryTableReference(raw is >= 0 and <= 100 ? raw : 0) ?? throw MapSpeechMissing("Scenery_lookup");
            if (!MapSpeechNeed(current.ReadSceneryNativeValidity(scenery), "Scenery_validity")) return new(Remove(state, sequence), false);
            int physical = MapSpeechNeed(scenery.PhysicalSlot, "Scenery_physical");
            string name = current.SceneryDefinitions?.Sceneries[physical].Name ?? throw MapSpeechMissing("Scenery_name");
            //1BC3C0 selects text only when the real Army layout is available.
            //This is the only once-selected C829 text/App transition.
            var text = FormatReportMessage(state, current, 0xC829,
                new(Person0: entry.OfficerId, String0: name), ReportMessageOrigin.ArmyMapSpeech, null);
            if (text.Text.Length == 0) return new(Remove(text.State, sequence), false);
            var entries = owner.Entries.ToBuilder();
            int index = entries.IndexOf(entry);
            entries[index] = entry with { TextAtSource = text.Text, ElapsedSeconds = 0 };
            return new(text.State with { ArmyMapSpeech = owner with { Entries = entries.ToImmutable() } }, true);
        }
        catch (InvalidDataException error) { return new(state, false, error.Message); }
    }

    private static GameState Remove(GameState state, long sequence)
    {
        var owner = state.ArmyMapSpeech ?? throw MapSpeechMissing("remove_owner");
        return state with { ArmyMapSpeech = owner with { Entries = owner.Entries.Where(entry => entry.Sequence != sequence).ToImmutableArray() } };
    }
    private static T MapSpeechNeed<T>(T? value, string field) where T : struct => value ?? throw MapSpeechMissing(field);
    private static InvalidDataException MapSpeechMissing(string field) => new("army-map-speech." + field);
}
