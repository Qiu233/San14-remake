using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The existing2B44E0(District,null,0) query. It may use24B130's actual
    //candidate temporary and comparator, but never appoints Force52. The
    //capture/transfer caller retains its own live current and latest state.
    internal TransferPersonReference ReadCaptureAdviserCurrent(GameState state,
        SourceWorldContext current, DistrictReference district)
    {
        var pool = current.ReadOwnedCurrentListPool()
            ?? throw new InvalidDataException("capture.adviser.current_pool");
        return new PersonDeathHistorySelection(state, current, pool, SearchAbilities(current),
            _governorInputs, (person, effect) => _baseCapacityInputs.ReadPanelModifier(state, person, effect))
            .Select(district);
    }
}
