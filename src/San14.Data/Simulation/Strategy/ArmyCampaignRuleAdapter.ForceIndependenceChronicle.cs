using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IForceIndependenceChronicleRules
{
    /// <summary>The old intent represents only the pre-creation record. Its
    /// absent tail outcome and retained source slot are never reconstructed.</summary>
    public CampaignChronicleState? RecordForceIndependenceChronicle(GameState state,
        ForceIndependenceSceneRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        var current = CaptureWritableTransportRouteCurrent(state);
        return BeginForceIndependenceChronicleCurrent(state, current, request).State.CampaignChronicle;
    }

    // Before creation: event255/family1/kind28, original Force name/colour,
    // rebel/original Force/Home labels, and no map snapshot yet.
    private CampaignChronicleWriteResult BeginForceIndependenceChronicleCurrent(GameState state,
        SourceWorldContext current, ForceIndependenceSceneRequest request)
    {
        if (request.OriginalForceId is < 0 or > 51 || request.RulerOfficerId is < 0 or > 6000 ||
            request.HomeLocationId is < 0 or > 61)
            throw ForceIndependenceChronicleMissing("source_identity");
        var force = current.ReadForceTableReference(request.OriginalForceId)
            ?? throw ForceIndependenceChronicleMissing("original_Force_pointer");
        if (current.ReadForceNativeValidity(force) is not true)
            throw ForceIndependenceChronicleMissing("original_Force_validity");
        var rebel = state.Officers.GetValueOrDefault(request.RulerOfficerId)
            ?? throw ForceIndependenceChronicleMissing("rebel_saved_owner");
        var home = BaseLookup.Find(request.HomeLocationId, current);
        if (current.ReadBaseNativeValidity(home) is not true ||
            current.ReadBaseObjectLink(home) != request.HomeLocationId || home.PhysicalSlot is not int physical)
            throw ForceIndependenceChronicleMissing("original_Home_binding");
        string homeName = home.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name,
            BaseKind.Gate => state.Gates.GetValueOrDefault(physical + 51)?.Name,
            _ => null
        } ?? throw ForceIndependenceChronicleMissing("original_Home_name12");
        string mainName = HistoryWriter.CustomFirstName(state, current, force, ForceIndependenceChroniclePersonName);
        byte color = current.ReadForceReportColor(force) ?? throw ForceIndependenceChronicleMissing("original_Force49");
        string fullForceName = HistoryWriter.FallbackName(state, current, force, ForceIndependenceChroniclePersonName);
        var command = new CampaignChronicleCommand(255, 1, request.SourceDay, mainName, color, 28,
            ImmutableArray.Create(ForceIndependenceChroniclePersonName(rebel, true), fullForceName, HeadHistory(homeName)),
            ImmutableArray.Create(current.ReadBaseCurrentHexIndex(home), 48400, 48400, 48400));
        return new CampaignChronicleWriter(HistoryWriter).WriteTracked(state, current, command,
            captureSnapshot: false, ReadApplicationGameplay()?.UserSelectionServicePresent);
    }

    // Reached only after successful creation, follower/District maintenance
    // and bilateral relations. Keep the actual returned physical record slot.
    private GameState FinishForceIndependenceChronicleCurrent(GameState state, SourceWorldContext current,
        CampaignChronicleEntryReference? retained)
    {
        var writer = new CampaignChronicleWriter(HistoryWriter);
        if (retained is { } entry) state = writer.RefreshEntrySnapshot(state, current, entry);
        return writer.RefreshLatestPrimarySnapshotIfEvent(state, current, 300);
    }

    private static string ForceIndependenceChroniclePersonName(OfficerState person, bool full) => HeadHistory(full
        ? person.Name ?? throw ForceIndependenceChronicleMissing("current_person_name")
        : person.CurrentSurname ?? throw ForceIndependenceChronicleMissing("current_person_surname"));
    private static InvalidDataException ForceIndependenceChronicleMissing(string field) =>
        new("force-independence.chronicle." + field);
}
