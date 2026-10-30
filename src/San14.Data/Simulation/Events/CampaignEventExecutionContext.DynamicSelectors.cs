using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    public S14EventConditionPersonReference ResolvePerson(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        S14EventConditionPersonReference? person;
        if (selector is >= -14 and <= -10)
            person = _actors.GetValueOrDefault(selector);
        else if (selector == -9)
            person = ReadPersonPointer(Known(Queries.ReadConditionEmperorPersonId(), "emperor identity"));
        else if (selector is >= -8 and <= -5)
        {
            // Person -8..-5 and Force -5..-2 name the same four target
            // references. Source checks Force validity before its ruler.
            var force = readTargetForce(selector + 3);
            person = force is not null && Known(Queries.ReadConditionForceValidity(force), "target ruler Force validity")
                ? ReadPersonPointer(Known(Queries.ReadConditionForceRulerPersonId(force), "target Force ruler identity"))
                : null;
        }
        else if (selector == -4)
            person = (Queries.ReadConditionCurrentPlayerPerson() ?? throw Missing("current player Person pointer")).Value;
        else if (selector >= 0)
            person = ReadPersonPointer(selector);
        else if (selector is >= -3 and <= -1 or >= -17 and <= -15)
            person = ReadDialogueRole(selector);
        else person = null; // The source default for remaining negatives selects Person0.
        return person ?? PersonZero();
    }

    private S14EventConditionPersonReference? ReadPersonPointer(int identity)
    {
        var pointer = identity <= 6000 ? Queries.ReadConditionPersonTableSlot(identity) : Queries.ReadConditionBasePerson();
        return (pointer ?? throw Missing("Person table pointer")).Value;
    }

    private S14EventConditionPersonReference PersonLookup(int identity) => ReadPersonPointer(identity) ?? PersonZero();

    private S14EventConditionPersonReference PersonZero() =>
        (Queries.ReadConditionPersonTableSlot(0) ?? throw Missing("Person0 lookup")).Value
        ?? throw Missing("Person0 pointer");

    private S14EventConditionForceReference? ResolveForce(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        S14EventConditionForceReference? force = null;
        if (selector is >= -10 and <= -6)
        {
            // This selector family numbers the same cast four slots above
            // the ordinary Person selectors: Force -6 reads Person -10.
            var actor = _actors.GetValueOrDefault(selector - 4);
            if (actor is not null && Known(Queries.ReadConditionPersonValidity(actor), "cast Force Person validity"))
                force = (Queries.ReadConditionPersonForce(actor) ?? throw Missing("cast Person Force pointer")).Value;
        }
        else if (selector is >= -5 and <= -2)
            force = readTargetForce(selector);
        else if (selector == -1)
            force = (Queries.ReadConditionCurrentPlayerForce() ?? throw Missing("current player Force pointer")).Value;
        else if (selector >= 0)
        {
            var person = ReadPersonPointer(selector);
            if (person is not null && Known(Queries.ReadConditionPersonValidity(person), "ruler Force Person validity") &&
                Known(Queries.ReadConditionPersonStatus(person), "ruler Force Person status") == 1)
                force = (Queries.ReadConditionPersonForce(person) ?? throw Missing("ruler Person Force pointer")).Value;
        }
        // The source's remaining negative values and reset null pointers
        // explicitly select Force0. Missing queries never use that branch.
        return force ?? (Queries.ReadConditionForceTableSlot(0) ?? throw Missing("Force0 lookup")).Value;
    }

    private BaseReference ResolveBaseSelector(int selector)
    {
        BaseReference? receiver;
        if (selector == -1)
        {
            var city = Queries.ReadConditionCurrentPlayerCity() ?? throw Missing("mapped player City pointer");
            receiver = new(BaseKind.City, city.PhysicalSlot);
        }
        else if (selector == -2)
        {
            // Direct registered-Base lookup of the current ruler's Home
            // preserves a Gate. In contrast, -1 maps it to its owning City.
            var ruler = (Queries.ReadConditionCurrentPlayerPerson() ?? throw Missing("City current ruler pointer")).Value
                ?? PersonZero();
            receiver = Queries.ReadConditionPersonHomeBase(ruler) ?? throw Missing("current ruler Home Base");
        }
        else if (selector is >= -9 and <= -5)
        {
            //214 writes the same five source pointers used by both the
            // registered-Base and City selectors. They are always City receivers.
            var city = ReadStoryTargetCityPointer(selector);
            receiver = new(BaseKind.City, city.PhysicalSlot);
        }
        else if (selector is -4 or -3)
        {
            var city = ReadStoryMoveCityPointer(selector == -4 ? 0 : 1);
            receiver = new(BaseKind.City, city.PhysicalSlot);
        }
        else if (selector < 0) throw Missing("Base selector");
        else receiver = Queries.ReadConditionBaseSelector(selector) ?? throw Missing("registered Base selector");
        return receiver.PhysicalSlot is not null ? receiver :
            Queries.ReadConditionBaseSelector(0) ?? throw Missing("Base0 lookup");
    }
}
