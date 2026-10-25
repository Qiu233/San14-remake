using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Func<ArmyMapSpeechSpatialVoiceRequest, bool>? armyMapSpeechSpatialAdmission;
    private MainMapCameraTerrainSource? armyMapSpeechVoiceTerrain;

    /// <summary>The actual map/audio owner reserves a VoicePlayer slot and
    /// returns its native-equivalent admission result. The caller commits or
    /// aborts those reservations with the enclosing speech-start transaction.
    /// A null callback unbinds the owner. With a bound callback, explicit null
    /// terrain is the observed absent-Landscape1889A0 branch.</summary>
    public void ConfigureArmyMapSpeechSpatialVoice(Func<ArmyMapSpeechSpatialVoiceRequest, bool>? admit,
        MainMapCameraTerrainSource? terrain)
    {
        armyMapSpeechSpatialAdmission = admit;
        armyMapSpeechVoiceTerrain = terrain;
    }

    internal HistoryTextResult FormatArmyMapSpeechReportCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
    {
        return FormatReportMessage(state, current, root, operands, ReportMessageOrigin.ArmyMapSpeech, null,
            _personnelNarrationVoice, Position, Admit);
        NarrationSpatialVoicePosition? Position(int id) => ReadArmyMapSpeechVoicePositionCurrent(state, current, id);
        bool Admit(NarrationSpatialVoiceRequest request)
            => (armyMapSpeechSpatialAdmission ?? throw SpatialSpeechMissing("application_admission_owner"))(
                new(SaveVoice(request.Voice), request.SourceX, request.SourceY, request.SourceZ));
    }

    //1BC3C0 action25 calls405B90 before expanding C4DB. Unlike Function5,
    //this caller supplies the packet's retained Hex position directly.
    private HistoryTextResult PrepareArmyMapSpeechOpeningVoiceCurrent(GameState state,
        SourceWorldContext current, ArmyMapSpeechEntry entry)
    {
        var source = ArmyMapSpeechVoiceGeometry.QueryPacketPosition(entry.HexId, armyMapSpeechVoiceTerrain);
        if (armyMapSpeechSpatialAdmission is null) throw SpatialSpeechMissing("application_admission_owner");
        int physical = current.ReadPersonLookupReference(entry.OfficerId <= 6000 ? entry.OfficerId : 0)?.PhysicalSlot
            ?? throw SpatialSpeechMissing("opening_Person_pointer");
        bool valid = current.ReadPersonNativeValidity(physical) ?? throw SpatialSpeechMissing("opening_Person_validity");
        if (!valid) return new(state, "");
        var person = state.Officers.GetValueOrDefault(entry.OfficerId) ?? throw SpatialSpeechMissing("opening_Person_owner");
        var latest = state;
        var voices = new NarrationVoiceRequests(_personnelNarrationVoice ?? throw SpatialSpeechMissing("voice_settings"), Draw,
            spatialAdmission: request => armyMapSpeechSpatialAdmission(new(SaveVoice(request.Voice),
                request.SourceX, request.SourceY, request.SourceZ)));
        _ = voices.RequestSpatialAt(person, 2, source);
        return new(latest, "") { SpatialVoices = voices.PendingSpatial };

        int Draw(int bound)
        {
            if (bound < 2) return 0;
            var random = new SharedSourceRandom32((latest.CampaignAppRandom32 ?? throw SpatialSpeechMissing("App32")).State);
            int value = random.Next(bound); latest = latest with { CampaignAppRandom32 = new(random.State) }; return value;
        }
    }

    private NarrationSpatialVoicePosition? ReadArmyMapSpeechVoicePositionCurrent(GameState state,
        SourceWorldContext current, int id)
    {
        if (armyMapSpeechSpatialAdmission is null) throw SpatialSpeechMissing("application_admission_owner");
        int physical = current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0)?.PhysicalSlot
            ?? throw SpatialSpeechMissing("Person_pointer");
        bool valid = current.ReadPersonNativeValidity(physical) ?? throw SpatialSpeechMissing("Person_validity");
        if (!valid) return null;
        var hex = PersonReturnCommands.ResolvePersonPosition(state, current, physical,
            () => state.NativeMapSeason?.ThirdDate ?? throw SpatialSpeechMissing("current_date"));
        if (hex.PhysicalSlot is not int at) return null;
        if ((uint)at >= 48400) return null;
        var point = CeHexWorldCoordinates.Project(at);
        float x = (float)point.X, z = (float)point.Z, y = 0;
        if (armyMapSpeechVoiceTerrain is { } terrain)
        {
            float ground = terrain.Query(x, z);
            y = 445f <= ground ? ground : 445f;
        }
        return new(x, y, z);
    }

    private static InvalidDataException SpatialSpeechMissing(string field)
        => new("army-map-speech.spatial_voice." + field);
}
