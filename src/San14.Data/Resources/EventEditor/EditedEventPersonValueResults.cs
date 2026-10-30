using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EditedEventResultsCurrent
    {
        private int Parameter(IReadOnlyList<int> arguments)
        {
            int physical = ValuePerson(arguments[0], "parameter");
            if (!ValueEligible(physical, "parameter")) return 0;
            int kind = arguments[1];
            if (kind is < 1 or > 6) return 0;
            int before = rules.ReadDirectPersonParameterCurrent(State, current, physical, kind);
            int requested = EditedEventResultExecution.Change(before, arguments[3], arguments[2]);
            State = rules.ApplyDirectPersonParameterCurrent(State, current, physical, kind, requested);
            return 1;
        }

        private int Illness(IReadOnlyList<int> arguments)
        {
            int physical = ValuePerson(arguments[0], "illness");
            if (!ValueEligible(physical, "illness")) return 0;
            if (Need(current.ReadPersonStatus(physical), "illness_status") is 9 or 7 or 0) return 0;
            State = ApplyPersonIllnessCurrent(State, current, physical, arguments[1]);
            return 1;
        }

        private int Credit(IReadOnlyList<int> arguments)
        {
            if (arguments[1] <= 0) return 0;
            int physical = ValuePerson(arguments[0], "credit");
            if (!Need(current.ReadPersonNativeValidity(physical), "credit_Person_validity")) return 0;
            if (Need(current.ReadPersonStatus(physical), "credit_status") is 0 or 1 or 5 or 7 or 8 or 9 ||
                DuelAftermathSpecialPerson(current, physical)) return 0;
            if ((Need(current.ReadOwnedReturnPersonFlags(physical), "credit_Person15E") & 0x4000) != 0) return 0;
            State = rules.ApplyDirectPersonMeritCurrent(State, current, physical, arguments[1]);
            //3D5060's ordinary event-manager list is a normal result-summary
            //input, not a second merit writer or a rank-up queue. Edited
            //playback uses399FB0's definition page prepared by its owner.
            return 1;
        }

        private int ValuePerson(int selector, string owner) => Selectors.Person(selector);

        private bool ValueEligible(int physical, string owner)
        {
            if (!Need(current.ReadPersonNativeValidity(physical), owner + "_Person_validity")) return false;
            ushort id = Need(current.ReadPersonIdentity(physical), owner + "_Person10");
            if (unchecked((ushort)(id - 4001)) <= 1099) return false;
            ushort home = Need(current.ReadPersonFirstLocation(physical), owner + "_PersonE2");
            if ((home is < 1 or > 61) && DuelAftermathForce(current, physical) is >= 46 and <= 50) return false;
            return !DuelAftermathSpecialPerson(current, physical) &&
                (Need(current.ReadOwnedReturnPersonFlags(physical), owner + "_Person15E") & 0x4000) == 0;
        }
    }
}
