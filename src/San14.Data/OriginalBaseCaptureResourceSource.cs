using San14.Core;

namespace San14.Data;

public sealed record OriginalBaseCaptureResourceWrite(string Field, int Before, int Scaled,
    int FirstCapacity, int FirstClamp, int SecondCapacity, int After);
public sealed record OriginalBaseCaptureResourceReceipt(int KeepPercent, int MoneyModifier46, int TroopModifier45,
    bool CommanderValid, IReadOnlyList<OriginalBaseCaptureResourceWrite> Writes);

/// <summary>The reached ordinary mode0 resource section26DF86..26E387.
/// Its caller owns District/Object transfer, people, land and publication.
/// Context is an operation-local draft; unknown inputs discard that draft.</summary>
public static class OriginalBaseCaptureResourceSource
{
    public static OriginalBaseCaptureResourceReceipt ApplyCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalBaseReference target,
        OriginalTransferPersonReference commander, IEffectiveOfficerAbilities abilities)
    {
        int physical = commander.PhysicalSlot ?? throw Missing("Commander_pointer");
        var writes = new List<OriginalBaseCaptureResourceWrite>();

        //The wall floor precedes resource scaling, with two fresh source
        //virtual60 reads. A valid commander preserves a higher current wall.
        int wallCap1 = OriginalBasePostLoad.ReadCapacities(target, current).Field46;
        int wallFloor = unchecked(wallCap1 * P(0x1A4A7CC)) / 100;
        int wallCap2 = OriginalBasePostLoad.ReadCapacities(target, current).Field46;
        wallFloor = Math.Min(wallFloor, wallCap2);
        bool commanderValid = Known(current.ReadPersonNativeValidity(physical), "Commander_validity");
        int wall = OriginalBasePostLoad.ReadLinkedField14(target, current);
        if (!commanderValid || wall < wallFloor) OriginalBasePostLoad.WriteLinkedField14(target, wallFloor, current);
        current.SetBasePostLoadFlag66(target, OriginalBasePostLoad.ReadFlag66(target, current));
        if (target.Kind == OriginalBaseKind.City && Known(current.ReadBaseNativeValidity(target), "City_validity"))
        {
            int cap = OriginalBasePostLoad.ReadCapacities(target, current).Field46;
            if (cap <= OriginalBasePostLoad.ReadLinkedField14(target, current))
                OriginalBasePostLoad.WriteLinkedField14(target, cap, current);
            current.SetBasePostLoadFlag66(target, OriginalBasePostLoad.ReadFlag66(target, current));
        }

        int keep = P(0x1A4A784);
        if (commanderValid)
        {
            int charm = Known(abilities.Resolve(state, Known(current.ReadPersonIdentity(physical), "Commander10"))?.Charm, "Commander_full_charm4");
            int divisor = P(0x1A4A790);
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

        if (target.Kind == OriginalBaseKind.City)
        {
            ScaleResource(OriginalBaseResourceKind.Gold, money, "34");
            ScaleResource(OriginalBaseResourceKind.Supplies, money, "38");
        }
        ScaleResource(OriginalBaseResourceKind.Troops, troops, "3C");
        int before40 = Known(current.ReadArmyReturnBaseWounded40(target), "Base40");
        var wounds = Scale(before40, troops, "40", () => OriginalBasePostLoad.ReadResourceCapacities(target, current).Troops);
        current.SetArmyReturnBaseWounded40(target, wounds.After); writes.Add(wounds);

        //26E2D4 obtains FindCity(target), including its source validity.
        //Only a valid Commander capturing a City reaches these money floors.
        if (commanderValid && target.Kind == OriginalBaseKind.City)
        {
            var receivingCity = OriginalCurrentGateMapQueries.FindCity(target, current);
            if (Known(current.ReadBaseNativeValidity(receivingCity), "FindCity_validity"))
            {
                Floor(OriginalBaseResourceKind.Gold, 0x1A4A79C);
                Floor(OriginalBaseResourceKind.Supplies, 0x1A4A7B4);
            }
            void Floor(OriginalBaseResourceKind kind, int rva)
            {
                int value = unchecked((money + 100) * P(rva)) / 100;
                if (Known(current.ReadBaseResource(receivingCity, kind), "FindCity_resource") < value)
                    SetResource(receivingCity, kind, value);
            }
        }
        if (!commanderValid)
        {
            int troopCap = OriginalBasePostLoad.ReadResourceCapacities(target, current).Troops;
            current.SetBaseResource(target, OriginalBaseResourceKind.Troops, Math.Min(troopCap, 0));
            OriginalBasePostLoad.WriteMorale(target, 0, current);
            int woundCap = OriginalBasePostLoad.ReadResourceCapacities(target, current).Troops;
            current.SetArmyReturnBaseWounded40(target, Math.Min(woundCap, 0));
        }
        return new(keep, money, troops, commanderValid, writes.AsReadOnly());

        int P(int rva) => Known(OriginalBaseCaptureResourceInputs.ReadCurrentTc(state, rva), $"parameter_{rva:X}");
        void ScaleResource(OriginalBaseResourceKind kind, int modifier, string field)
        {
            int before = Known(current.ReadBaseResource(target, kind), "Base" + field);
            var write = Scale(before, modifier, field, () => Capacity(target, kind));
            current.SetBaseResource(target, kind, write.After); writes.Add(write);
        }
        OriginalBaseCaptureResourceWrite Scale(int before, int modifier, string field, Func<int> readCap)
        {
            int first = readCap();
            int scaled = unchecked(unchecked(keep + modifier) * before) / 100;
            int candidate = Math.Min(first, scaled);
            int second = readCap();
            int after = candidate < 0 ? 0 : Math.Min(candidate, second);
            return new(field, before, scaled, first, candidate, second, after);
        }
        int Capacity(OriginalBaseReference reference, OriginalBaseResourceKind kind)
        {
            var cap = OriginalBasePostLoad.ReadResourceCapacities(reference, current);
            return kind == OriginalBaseResourceKind.Gold ? cap.Gold : kind == OriginalBaseResourceKind.Supplies ? cap.Supplies : cap.Troops;
        }
        void SetResource(OriginalBaseReference reference, OriginalBaseResourceKind kind, int value)
        {
            int cap = Capacity(reference, kind);
            current.SetBaseResource(reference, kind, value < 0 ? 0 : Math.Min(value, cap));
        }
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.capture26DA10." + field);
}
