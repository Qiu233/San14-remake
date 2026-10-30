using San14.Data.Formats;

namespace San14.Data;

/// <summary>A real EventView input wait. It has no duration, text formatting,
/// sound or world mutation. The presentation owner supplies its actual input,
/// automatic-message setting and fast/skip lifecycle.</summary>
public sealed record CampaignEventInputWait(int OperationIndex, int SourceOffset)
    : CampaignEventCommand(OperationIndex, SourceOffset, 241);

internal sealed partial class CampaignEventExecutionContext
{
    /// <summary>242..244 use this same invocation's real dynamic Person
    /// selectors, cast and target-Force bank. No result classification is read
    /// or manufactured. The supplied latches are the existing condition owner.</summary>
    internal int ExecuteInvocationControl(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce,
        Action<int, S14EventConditionForceReference?> storeTargetForce, Action block)
    {
        if (arguments.Count != 8) throw Missing("invocation control operands");
        switch (code)
        {
            case 242:
            {
                var person = ResolvePerson(arguments[0], readTargetForce);
                if (!Known(Queries.ReadConditionPersonValidity(person), "target ruler Person validity")) return 0;
                var forces = Queries.ReadRegisteredConditionForcesInNativeOrder() ?? throw Missing("target ruler Force registry");
                foreach (var force in forces)
                {
                    if (force is null) throw Missing("target ruler Force payload");
                    int id = Known(Queries.ReadConditionForceRulerPersonId(force), "target ruler identity");
                    var ruler = ReadPersonPointer(id);
                    //The original compares resolved payload pointers, not raw
                    //IDs, Force sort order or a second ruler validity gate.
                    if (ruler != person) continue;
                    storeTargetForce(arguments[1], force);
                    return 1;
                }
                return 0;
            }
            case 243:
            {
                var person = ResolvePerson(arguments[0], readTargetForce);
                if (!Known(Queries.ReadConditionPersonValidity(person), "target affiliation Person validity")) return 0;
                var force = (Queries.ReadConditionPersonForce(person) ?? throw Missing("target affiliation Force lookup")).Value;
                if (force is null || !Known(Queries.ReadConditionForceValidity(force), "target affiliation Force validity")) return 0;
                storeTargetForce(arguments[1], force);
                return 1;
            }
            case 244:
                //Resolve in order and stop at the first live Person. Unvisited
                //operands do not trigger the lazy dialogue/actor producers.
                foreach (int selector in arguments.Take(5))
                {
                    var person = ResolvePerson(selector, readTargetForce);
                    bool valid = Known(Queries.ReadConditionPersonValidity(person), "dead-test Person validity");
                    if (selector != 0 && !valid) block();
                    if (valid && Known(Queries.ReadConditionPersonStatus(person), "dead-test Person status") != 9) return 0;
                }
                return 1;
            default: throw Missing("invocation control opcode");
        }
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>847080/3343E0 bypass only while the attached game is actually
    /// leaving for the title. Unknown application lifecycle stays unknown;
    /// Collection/edited packet modes are not this source gate.</summary>
    public bool? ReadStoryInputWaitBypassCurrent() => ReadApplicationReturnToTitlePending();
}
