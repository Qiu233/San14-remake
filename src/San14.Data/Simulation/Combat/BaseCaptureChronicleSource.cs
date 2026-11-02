using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2851B0: after the nearby-Army pass and before26DA10 changes Base/Area
    //ownership. The actual player Army branch writes8C8D50, not ReportPool.
    internal GameState AppendBaseCaptureChronicleCurrent(GameState state, SourceWorldContext current,
        BaseReference target, ArmyUnitReference winner, ForceReference winnerDistrictForce,
        TacticSourceSamplerWorld? operationSampler)
    {
        int location = CaptureChronicleNeed(current.ReadBaseObjectLink(target), "Base4E");
        if ((uint)location >= 62 ||
            !CaptureChronicleNeed(current.ReadArmyUnitNativeValidity(winner), "winner_validity")) return state;
        var armyForce = current.ReadArmyUnitForceReference(winner)
            ?? throw CaptureChronicleMissing("winner_Force_pointer");
        if (!TalentForceIsPlayerCurrent(state, current, armyForce)) return state;

        // The active period owns its pre-writer sampler. A canonical
        // reconstruction requires a completed calendar boundary and cannot
        // replace this already supplied or continuation-restored owner.
        int day = operationSampler?.Day450 ?? ReadArmyDestructionSampler(state, current)?.Day450
            ?? throw CaptureChronicleMissing("World450");
        int rawOldForce = CaptureChronicleNeed(current.ReadBaseForceSlot(target), "old_Base_B0");
        var oldForce = current.ReadForceTableReference(rawOldForce < 52 ? rawOldForce : 0)
            ?? throw CaptureChronicleMissing("old_Force_pointer");
        string oldName = HistoryWriter.FallbackName(state, current, oldForce, CaptureChroniclePersonName);
        string winnerName = HistoryWriter.FallbackName(state, current, winnerDistrictForce, CaptureChroniclePersonName);
        int hex = current.ReadBaseCurrentHexIndex(target);
        string mainName = HistoryWriter.CustomFirstName(state, current, winnerDistrictForce, CaptureChroniclePersonName);
        byte color = CaptureChronicleNeed(current.ReadForceReportColor(winnerDistrictForce), "winner_Force49");
        int physical = CaptureChronicleNeed(target.PhysicalSlot, "Base_pointer");
        string baseName = HeadHistory((target.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name,
            BaseKind.Gate => state.Gates.GetValueOrDefault(physical + 51)?.Name,
            _ => null
        }) ?? throw CaptureChronicleMissing("Base_name12"));
        int currentOldForce = CaptureChronicleNeed(current.ReadBaseForceSlot(target), "entry_Base_B0");
        uint entryKind = currentOldForce != 0 ? 10u : 11u;
        return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
            new(0x40, 1, day, mainName, color, entryKind,
                ImmutableArray.Create(winnerName, baseName, oldName),
                ImmutableArray.Create(hex, 48400, 48400, 48400)),
            ReadApplicationGameplay()?.UserSelectionServicePresent);
    }

    private static string CaptureChroniclePersonName(OfficerState person, bool full) => HeadHistory(full
        ? person.Name ?? throw CaptureChronicleMissing("ruler_current_name")
        : person.CurrentSurname ?? throw CaptureChronicleMissing("ruler_current_surname"));
    private static T CaptureChronicleNeed<T>(T? value, string field) where T : struct
        => value ?? throw CaptureChronicleMissing(field);
    private static InvalidDataException CaptureChronicleMissing(string field)
        => new("Base-capture.chronicle." + field);
}
