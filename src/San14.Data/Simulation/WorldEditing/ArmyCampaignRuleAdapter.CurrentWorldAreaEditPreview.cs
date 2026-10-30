using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Evaluate one selected Area's draft through the normal detail
    /// readers. The returned page is a preview; no edited marker, command,
    /// random-state advance or world commit is produced. UI caches this result
    /// after a draft change rather than querying it for every frame.</summary>
    public AreaAdministrationQuery<AreaInformationPage> QueryCurrentWorldAreaEditPreview(
        GameState state, int id, CurrentWorldAreaEditValues values)
    {
        try
        {
            if (values is null) throw WorldEditMissing("area_preview_values");
            if (!AreaEditPreviewInRange(values.Commerce, 0, 1000) ||
                !AreaEditPreviewInRange(values.Agriculture, 0, 1000) ||
                !AreaEditPreviewInRange(values.Barracks, 0, 1000) ||
                !AreaEditPreviewInRange(values.Population, 5000, 60000) ||
                !AreaEditPreviewInRange(values.Security, 0, 100))
                throw WorldEditMissing("area_preview_range");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            _ = ReadCurrentWorldEditArea(state, current, id);
            var reference = new AreaReference(id);
            var writer = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
            Progress(CeAreaDevelopmentField.Commerce, values.Commerce);
            Progress(CeAreaDevelopmentField.Agriculture, values.Agriculture);
            Progress(CeAreaDevelopmentField.Barracks, values.Barracks);
            var preview = writer.State;
            if (values.Population is int population)
            {
                var area = preview.Areas[id];
                var aftermath = area.NativeNormalAftermath ?? throw WorldEditMissing("area_preview_population_owner");
                int before = WorldEditNeed(aftermath.Quantity52, "area_preview_population");
                if (before != population)
                    preview = preview with { Areas = new(preview.Areas) { [id] = area with
                        { NativeNormalAftermath = aftermath with { Quantity52 = (ushort)population } } } };
            }
            if (values.Security is int security)
            {
                var area = preview.Areas[id];
                int before = WorldEditNeed(area.Security, "area_preview_security");
                if (before != security)
                {
                    int raw = WorldEditNeed(current.ReadAreaCurrentForceSlot(reference), "area_preview_current_force");
                    var force = current.ReadForceTableReference(raw < 52 ? raw : 0)
                        ?? throw WorldEditMissing("area_preview_force_pointer");
                    int physical = WorldEditNeed(current.ReadForcePhysicalSlot(force), "area_preview_force_identity");
                    //The edit setter suppresses this byte store for tribes.
                    //Preview must show that actual result, not a guessed owner
                    //from the independent AreaState.ForceId projection.
                    if (physical is not (>= 46 and <= 50))
                        preview = preview with { Areas = new(preview.Areas) { [id] = area with { Security = security } } };
                }
            }
            return AreaAdministrationQuery<AreaInformationPage>.Known(QueryAreaInformationCurrent(preview, id, current));

            void Progress(CeAreaDevelopmentField field, int? requested)
            {
                if (requested is not int value) return;
                var development = writer.State.Areas[id].Development ?? throw WorldEditMissing("area_preview_development_owner");
                int before = WorldEditNeed(field switch
                {
                    CeAreaDevelopmentField.Commerce => development.Commerce,
                    CeAreaDevelopmentField.Agriculture => development.Agriculture,
                    _ => development.Barracks
                }, "area_preview_development_" + field);
                if (before == value) return;
                //Flag0 is the mature edit branch: it only writes this Area's
                //progress and never reads/changes Force level-change1BE.
                _normalAftermathInputs.Development.WriteCurrentProgress(reference, field, value, 0, writer);
            }
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<AreaInformationPage>.Unknown(error.Message); }
    }

    private static bool AreaEditPreviewInRange(int? value, int minimum, int maximum) =>
        value is null || value >= minimum && value <= maximum;
}
