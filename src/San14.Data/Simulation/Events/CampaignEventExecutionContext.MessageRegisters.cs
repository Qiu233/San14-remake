using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    //93/3B1880 changes only this execution's lazy dialogue Force and six
    //roles. It does not change the world player, selected actors or lists.
    public bool ChangeDialogueForce(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var force = ResolveForce(selector, readTargetForce);
        _dialogueCast = new(force, null, null, null, null, null, null);
        //The source tests the actual pointer, not native Force validity.
        return force is not null;
    }

    //81/3A1520 consumes only A0. Equality is the actual Person receiver,
    //not a character ID, and invalid Persons do not consume the list scan.
    public bool ComparePersonInList(IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var person = ResolvePerson(arguments[0], readTargetForce);
        return Known(Queries.ReadConditionPersonValidity(person), "Person-list comparison validity") &&
            _candidates.Contains(person);
    }

    //102/3A5EB0: literal item-table index, not an illustration number or
    //a dynamic Person/Force selector. Invalid entries write known zero.
    public int ReadItemMessageParameter(int selector)
    {
        if (selector < 0) throw Missing("negative Item message selector has no safe source-table interpretation");
        var item = Queries.ReadConditionItemTableSlot(selector < 202 ? selector : 0)
            ?? throw Missing("Item message table pointer");
        return Known(Queries.ReadConditionItemValidity(item), "Item message validity")
            ? Known(item.PhysicalSlot, "Item message physical identity22DF30") : 0;
    }

    //104/39F490. Its dispatcher must skip key0 entirely to retain that
    //Number register. Unknown nonzero source keys explicitly write zero.
    public int ReadGameNumberMessageParameter(int key)
    {
        if (key is >= 1 and <= 3)
        {
            var date = (Queries as IS14BasicEventConditionQueries)?.ReadConditionDate()
                ?? throw Missing("message Number current third date");
            return key switch { 1 => date.Year, 2 => date.Month, _ => date.Day };
        }
        if (key == 4)
        {
            var force = (Queries.ReadConditionCurrentPlayerForce()
                ?? throw Missing("message Number global player Force lookup")).Value
                ?? throw Missing("message Number global player Force pointer");
            return Known(Queries.ReadConditionForceExperience(force), "message Number global Force164");
        }
        if (key is >= 5 and <= 7)
        {
            //3E4700 uses the global player's ruler Home, mapped through
            //22A940 to its City. It does not use93's dialogue Force.
            var city = Queries.ReadConditionCurrentPlayerCity()
                ?? throw Missing("message Number player Home mapped City lookup");
            int physical = Known(city.PhysicalSlot, "message Number player Home mapped City pointer");
            var location = new BaseReference(BaseKind.City, physical);
            return key switch
            {
                5 => Known(Queries.ReadConditionBaseGold(location), "message Number City34 Gold"),
                6 => Known(Queries.ReadConditionBaseSupplies(location), "message Number City38 Supplies"),
                _ => Known(Queries.ReadConditionBaseTroops(location), "message Number City3C Troops")
            };
        }
        return 0;
    }
}
