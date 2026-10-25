using San14.Core;

namespace San14.Data;

public sealed partial class CaptureReturningPersonHomeSource
{
    //2851B0 has written actual status4. E2 is still the person's own current
    //Home, independently of the holding Base in E4. Share260310's complete
    //candidate/Primary/Return body without the old captured-Home UI contract.
    internal ReturningPersonHomeResult TryApplyReleasedCaptiveCurrent(GameState state,
        SourceWorldContext current, int person, NativeMapSeasonDate? date,
        ArmyReturnDistrictOperationQueries operation, List<CampaignEffectRequest> effects)
    {
        try
        {
            int id = Need(current.ReadPersonIdentity(person), "released_captive_Person10");
            if (Need(current.ReadPersonStatus(person), "released_captive_E6") != 4)
                throw Missing("released_captive_actual_status4");
            bool busy = PersonCurrentBusySource.Read(current, person, owner => Missing("released_captive_232370." + owner));
            int home = Need(current.ReadPersonFirstLocation(person), "released_captive_E2");
            int force = Need(current.ReadPersonOldForceSlot(person), "released_captive_Force");
            return ApplyBeforeTransfer(state, home, force, id, idleResident: !busy,
                operationThirdDate: date, operationCurrent: current, currentBusy: busy,
                districtOperation: operation, feedback: effects, releasedCaptive: true);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}
