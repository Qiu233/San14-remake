using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<AreaAdministrationDraft> OpenAreaAdministrationDraft(GameState state, int cityId)
    {
        try
        {
            if (Seed.Language is not ("TC" or "SC")) throw new InvalidDataException("area-draft.source_language");
            if (state.CampaignEventWorld?.CurrentUserIndex != -1 &&
                ReadApplicationGameplay()?.UserSelectionServicePresent != false)
                throw new InvalidDataException("area-draft.tutorial_current_user");
            if (!state.Cities.TryGetValue(cityId, out var city)) throw new InvalidDataException("area-draft.city");
            var indexes = state.ReadArmyLifecycle()?.Indexes ?? throw new InvalidDataException("area-draft.saved_indexes");
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            _ = WorldIndexContext.ImportOwnedState(current, indexes, state);
            PersonReturnCommands.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            // The isolated draft keeps its existing Area/People owner. Bind
            //the same already-loaded Scenery44 definitions and saved47 values
            //used by the real map current; unknown saved flags remain unknown.
            current.MaterializeFixedScenery(_landmarkDefinitions);
            for (int physical = 0; physical < 101; physical++)
                current.SetSceneryFlags47(physical, state.LandmarkInvestments?.GetValueOrDefault(physical)?.Flags);
            var card = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario, Seed.Language, _baseCapacityInputs, _repository, _governorInputs,
                ReadApplicationGameplay()?.UserSelectionServicePresent);
            var narration = new AreaAdministrationNarrationSource(state, current, _definitions, _scenario, Seed.Language, _repository, _baseCapacityInputs);
            bool goldCapacityInputsBound = _baseCapacityInputs.BindCurrent(state, current);
            var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, goldCapacityInputsBound);
            return AreaAdministrationQuery<AreaAdministrationDraft>.Known(new(state, current, city, _scenario.Snapshot.Areas, card, narration, preview));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationDraft>.Unknown(unknown.Message); }
    }
}
