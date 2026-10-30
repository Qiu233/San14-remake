using San14.Core;

namespace San14.Data;

public sealed record BaseCaptureResourceWrite(string Field, int Before, int Scaled,
    int FirstCapacity, int FirstClamp, int SecondCapacity, int After);
public sealed record BaseCaptureResourceReceipt(int KeepPercent, int MoneyModifier46, int TroopModifier45,
    bool CommanderValid, IReadOnlyList<BaseCaptureResourceWrite> Writes);

/// <summary>The reached ordinary mode0 resource section26DF86..26E387.
/// Its caller owns District/Object transfer, people, land and publication.
/// Context is an operation-local draft; unknown inputs discard that draft.</summary>
public static class BaseCaptureResourceSource
{
    public static BaseCaptureResourceReceipt ApplyCurrent(GameState state,
        SourceWorldContext current, BaseReference target,
        TransferPersonReference? commander, IEffectiveOfficerAbilities abilities)
    {
        int? physical = commander?.PhysicalSlot;
        var writes = new List<BaseCaptureResourceWrite>();

        //The wall floor precedes resource scaling, with two fresh source
        //virtual60 reads. A valid commander preserves a higher current wall.
        int wallCap1 = BasePostLoad.ReadCapacities(target, current).Field46;
        int wallFloor = unchecked(wallCap1 * P(SimulationParameterKeys.BaseCapture.CapturedWallFloorPercent)) / 100;
        int wallCap2 = BasePostLoad.ReadCapacities(target, current).Field46;
        wallFloor = Math.Min(wallFloor, wallCap2);
        bool commanderValid = physical.HasValue && Known(current.ReadPersonNativeValidity(physical.Value), "Commander_validity");
        int wall = BasePostLoad.ReadLinkedField14(target, current);
        if (!commanderValid || wall < wallFloor) BasePostLoad.WriteLinkedField14(target, wallFloor, current);
        current.SetBasePostLoadFlag66(target, BasePostLoad.ReadFlag66(target, current));
        if (target.Kind == BaseKind.City && Known(current.ReadBaseNativeValidity(target), "City_validity"))
        {
            int cap = BasePostLoad.ReadCapacities(target, current).Field46;
            if (cap <= BasePostLoad.ReadLinkedField14(target, current))
                BasePostLoad.WriteLinkedField14(target, cap, current);
            current.SetBasePostLoadFlag66(target, BasePostLoad.ReadFlag66(target, current));
        }

        int keep = P(SimulationParameterKeys.BaseCapture.MinimumResourceKeepPercent);
        if (commanderValid)
        {
            int charm = Known(abilities.Resolve(state, Known(current.ReadPersonIdentity(physical!.Value), "Commander10"))?.Charm, "Commander_full_charm4");
            int divisor = P(SimulationParameterKeys.BaseCapture.CommanderCharmDivisor);
            if (divisor == 0 || charm == int.MinValue && divisor == -1) throw Missing("keep_IDIV");
            //26E097 CMP EAX,EDI;26E099 CMOVGE selects Charm/divisor.
            //The source uses maxSigned, not a ceiling on the charm bonus.
            keep = Math.Max(keep, charm / divisor);
        }
        var force = current.ReadForceTableReference(Known(current.ReadBaseForceSlot(target), "target_virtualB0"))
            ?? throw Missing("new_Force_pointer");
        bool forceValid = Known(current.ReadForceNativeValidity(force), "new_Force_validity");
        int money = forceValid ? Known(current.ReadBaseForceModifier(force, 46), "Force_modifier46") : 0;
        int troops = forceValid ? Known(current.ReadBaseForceModifier(force, 45), "Force_modifier45") : 0;

        if (target.Kind == BaseKind.City)
        {
            ScaleResource(BaseResourceKind.Gold, money, "34");
            ScaleResource(BaseResourceKind.Supplies, money, "38");
        }
        ScaleResource(BaseResourceKind.Troops, troops, "3C");
        int before40 = Known(current.ReadArmyReturnBaseWounded40(target), "Base40");
        var wounds = Scale(before40, troops, "40", () => BasePostLoad.ReadResourceCapacities(target, current).Troops);
        current.SetArmyReturnBaseWounded40(target, wounds.After); writes.Add(wounds);

        //26E2D4 obtains FindCity(target), including its source validity.
        //Only a valid Commander capturing a City reaches these money floors.
        if (commanderValid && target.Kind == BaseKind.City)
        {
            var receivingCity = CurrentGateMapQueries.FindCity(target, current);
            if (Known(current.ReadBaseNativeValidity(receivingCity), "FindCity_validity"))
            {
                Floor(BaseResourceKind.Gold, SimulationParameterKeys.BaseCapture.CapturedCityGoldFloor);
                Floor(BaseResourceKind.Supplies, SimulationParameterKeys.BaseCapture.CapturedCitySuppliesFloor);
            }
            void Floor(BaseResourceKind kind, string key)
            {
                int value = unchecked((money + 100) * P(key)) / 100;
                if (Known(current.ReadBaseResource(receivingCity, kind), "FindCity_resource") < value)
                    SetResource(receivingCity, kind, value);
            }
        }
        if (!commanderValid)
        {
            int troopCap = BasePostLoad.ReadResourceCapacities(target, current).Troops;
            current.SetBaseResource(target, BaseResourceKind.Troops, Math.Min(troopCap, 0));
            BasePostLoad.WriteMorale(target, 0, current);
            int woundCap = BasePostLoad.ReadResourceCapacities(target, current).Troops;
            current.SetArmyReturnBaseWounded40(target, Math.Min(woundCap, 0));
        }
        return new(keep, money, troops, commanderValid, writes.AsReadOnly());

        int P(string key) => Known(BaseCaptureResourceInputs.ReadCurrent(state, key), $"parameter_{key}");
        void ScaleResource(BaseResourceKind kind, int modifier, string field)
        {
            int before = Known(current.ReadBaseResource(target, kind), "Base" + field);
            var write = Scale(before, modifier, field, () => Capacity(target, kind));
            current.SetBaseResource(target, kind, write.After); writes.Add(write);
        }
        BaseCaptureResourceWrite Scale(int before, int modifier, string field, Func<int> readCap)
        {
            int first = readCap();
            int scaled = unchecked(unchecked(keep + modifier) * before) / 100;
            int candidate = Math.Min(first, scaled);
            int second = readCap();
            int after = candidate < 0 ? 0 : Math.Min(candidate, second);
            return new(field, before, scaled, first, candidate, second, after);
        }
        int Capacity(BaseReference reference, BaseResourceKind kind)
        {
            var cap = BasePostLoad.ReadResourceCapacities(reference, current);
            return kind == BaseResourceKind.Gold ? cap.Gold : kind == BaseResourceKind.Supplies ? cap.Supplies : cap.Troops;
        }
        void SetResource(BaseReference reference, BaseResourceKind kind, int value)
        {
            int cap = Capacity(reference, kind);
            current.SetBaseResource(reference, kind, value < 0 ? 0 : Math.Min(value, cap));
        }
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.capture26DA10." + field);
}
