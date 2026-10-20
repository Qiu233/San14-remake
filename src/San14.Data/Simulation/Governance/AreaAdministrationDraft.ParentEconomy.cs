namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    /// <summary>Two independent fields from this same current projection.
    /// The complete city's upkeep is unrelated to changed rows or order cost.</summary>
    public AreaAdministrationQuery<AreaAdministrationParentEconomy> QueryParentEconomy()
    {
        try
        {
            EnsureOpen();
            return AreaAdministrationQuery<AreaAdministrationParentEconomy>.Known(
                new(_cityId, Revision, ParentCityGold(), ParentCurrentUpkeep()));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationParentEconomy>.Unknown(unknown.Message); }
    }

    private AreaAdministrationQuery<int?> ParentCityGold()
    {
        int? value = _current.ReadBaseResource(new(BaseKind.City, _cityId), BaseResourceKind.Gold);
        return value is int gold ? AreaAdministrationQuery<int?>.Known(gold) : Unknown("city_gold");
    }

    private AreaAdministrationQuery<int?> ParentCurrentUpkeep()
    {
        try
        {
            //CCityAreaIterator's sole membership predicate is Area35 == the
            //physical City identity1..51. Ownership/validity/held are not filters.
            if (_cityId is < 1 or > 51) return AreaAdministrationQuery<int?>.Known(0);
            int total = 0;
            AreaAdministrationUpkeepInputs? inputs = null;
            foreach (int area in RegisteredAreas(_current))
            {
                if (K(_current.ReadAreaCitySlot(new(area)), "upkeep_area_city") != _cityId) continue;
                int person = PersonAreaAppointmentSource.Resolve(_current, Person(area));
                if (!K(_current.ReadPersonNativeValidity(person), "upkeep_person_validity")) continue;
                inputs ??= AreaAdministrationUpkeep.ReadCurrent(_state);
                var force = ParentAreaForce(area);
                int policy = 0;
                if (K(_current.ReadForceNativeValidity(force), "upkeep_force_validity"))
                {
                    int forceId = K(_current.ReadForcePhysicalSlot(force), "upkeep_force_identity");
                    var owner = _state.Forces.GetValueOrDefault(forceId) ?? throw Missing("upkeep_force_owner");
                    policy = K(BattleRemakeCurrentForcePolicies.ReadForceEffect(_state, owner, 0x26, _current), "upkeep_policy26");
                }
                int personalities = K(TacticRemakeCurrentTraits.PersonContribution(_current, person, 0x27), "upkeep_personality27");
                total = unchecked(total + AreaAdministrationUpkeep.Calculate(inputs.Value, policy, personalities));
            }
            return AreaAdministrationQuery<int?>.Known(total);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<int?>.Unknown(unknown.Message); }
    }
}
