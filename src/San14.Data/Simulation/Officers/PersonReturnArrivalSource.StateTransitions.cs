namespace San14.Data;

public static partial class PersonReturnArrivalSource
{
    //271458 calls233240(Person,0). The identity band only qualifies with
    //a Base home1..61 and a Force outside46..50. Qualified status7 members,
    //or qualified members whose Force differs from the destination, are
    //skipped by271320 rather than reset, unlinked or removed from the task.
    internal static bool ShouldApplyPersonArrivalCurrent(SourceWorldContext current,
        int person, BaseReference target, ushort identity)
    {
        if (SpecialId(identity))
        {
            ushort first = Known(current.ReadPersonFirstLocation(person), "233240.PersonE2");
            if (first is >= 1 and <= 61)
            {
                var force = current.ReadPersonForce(person) ?? throw Unknown("233240.Person_Force");
                int physical = Known(current.ReadForcePhysicalSlot(force), "233240.Force_ordinal");
                if (physical is not (>= 46 and <= 50))
                {
                    if (Known(current.ReadPersonStatus(person), "271469.PersonE6") == 7) return false;
                    //The caller performs fresh Force getters after233240.
                    var freshForce = current.ReadPersonForce(person) ?? throw Unknown("271479.Person_Force");
                    int targetSlot = Known(current.ReadBaseForceSlot(target), "271484.target_ForceB0");
                    var targetForce = current.ReadForceTableReference(targetSlot < 52 ? targetSlot : 0)
                        ?? throw Unknown("271484.target_Force_pointer");
                    if (Known(current.ReadForcePhysicalSlot(freshForce), "271479.Person_Force_pointer") !=
                        Known(current.ReadForcePhysicalSlot(targetForce), "271484.target_Force_pointer")) return false;
                }
            }
        }
        ushort flags = Known(current.ReadArmyReturnPersonFlags196(person), "current.Person_flags15E");
        //2714A7 explicitly passes status5. All other statuses continue,
        //including status0; this is not a general native-validity check.
        return (flags & 0x4000) == 0 || Known(current.ReadPersonStatus(person), "2714A7.PersonE6") != 5;
    }

    //271776 explicitly tests status5 after writing E2. Only a free person
    //uses22A0E0(target)->241F20(kind10,Area). Captive6 and all other statuses
    //retain their status and receive the ordinary direct Base location.
    internal static void ApplyArrivalSecondLocationCurrent(SourceWorldContext current,
        int person, BaseReference target, ushort destination, Action<string> record)
    {
        if (Known(current.ReadPersonStatus(person), "current.PersonE6_after_E2") != 5)
        {
            current.WritePersonSecondLocation(person, (ushort)Math.Clamp((int)destination, 0, 1061));
            record("2717B2.PersonE4");
            return;
        }
        //22D640 falls back through the real Hex0 pointer.22A0E0 tests that
        //pointer's native validity; every nonnull Hex is valid, including0.
        var hex = CurrentGateMapQueries.FindHex(target, current);
        int area = hex.PhysicalSlot.HasValue
            ? Known(current.ReadHexAreaSlot(hex), "22A0E0.target_Hex12") : 0;
        //241F20's kind10 branch accepts1..500; zero/invalid Area stores0.
        ushort second = area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0;
        current.WritePersonSecondLocation(person, second);
        record("271792.241F20_Area.PersonE4");
    }
}
