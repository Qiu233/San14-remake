using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IForceEmergenceChronicleRules
{
    /// <summary>An older pending request still performs the real saved-record
    /// writer; it is neither a completed notification nor a fabricated CG.</summary>
    public CampaignChronicleState RecordForceEmergenceChronicle(GameState state, ForceEmergenceSceneRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        var current = CaptureWritableTransportRouteCurrent(state);
        return WriteForceEmergenceChronicleCurrent(state, current, request).CampaignChronicle
            ?? throw ForceEmergenceChronicleMissing("saved_owner");
    }

    // The emergence tail writes the permanent record before camera restore:
    // event247/family1, kind27, actual name/colour/day/Home and full snapshot.
    // It does not invoke a scene renderer or emit another presentation intent.
    private GameState WriteForceEmergenceChronicleCurrent(GameState state, SourceWorldContext current,
        ForceEmergenceSceneRequest request)
    {
        if (request.ForceId is < 0 or > 51 || request.RulerOfficerId is < 0 or > 6000 ||
            request.HomeLocationId is < 0 or > 61)
            throw ForceEmergenceChronicleMissing("source_identity");
        var force = current.ReadForceTableReference(request.ForceId)
            ?? throw ForceEmergenceChronicleMissing("Force_pointer");
        if (current.ReadForceNativeValidity(force) is not true)
            throw ForceEmergenceChronicleMissing("Force_validity");
        var ruler = state.Officers.GetValueOrDefault(request.RulerOfficerId)
            ?? throw ForceEmergenceChronicleMissing("ruler_saved_owner");
        var home = BaseLookup.Find(request.HomeLocationId, current);
        if (current.ReadBaseNativeValidity(home) is not true ||
            current.ReadBaseObjectLink(home) != request.HomeLocationId || home.PhysicalSlot is not int physical)
            throw ForceEmergenceChronicleMissing("Home_binding");
        string homeName = home.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name,
            BaseKind.Gate => state.Gates.GetValueOrDefault(physical + 51)?.Name,
            _ => null
        } ?? throw ForceEmergenceChronicleMissing("Home_name12");
        string mainName = HistoryWriter.CustomFirstName(state, current, force, ForceEmergenceChroniclePersonName);
        byte color = current.ReadForceReportColor(force) ?? throw ForceEmergenceChronicleMissing("Force49");
        int hex = current.ReadBaseCurrentHexIndex(home);
        return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
            new(247, 1, request.SourceDay, mainName, color, 27,
                ImmutableArray.Create(ForceEmergenceChroniclePersonName(ruler, true), HeadHistory(homeName), ""),
                ImmutableArray.Create(hex, 48400, 48400, 48400)),
            ReadApplicationGameplay()?.UserSelectionServicePresent);
    }

    private static string ForceEmergenceChroniclePersonName(OfficerState person, bool full) => HeadHistory(full
        ? person.Name ?? throw ForceEmergenceChronicleMissing("ruler_current_name")
        : person.CurrentSurname ?? throw ForceEmergenceChronicleMissing("ruler_current_surname"));
    private static InvalidDataException ForceEmergenceChronicleMissing(string field) =>
        new("force-emergence.chronicle." + field);
}
