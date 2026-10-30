using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    /// <summary>Resolve the source receiver before reading its current Object
    /// position. Home is independent of the person's moving/army location.</summary>
    public ushort CameraTargetHex(int code, int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        BaseReference receiver;
        if (code == 82) receiver = ResolveBaseSelector(selector);
        else
        {
            S14EventConditionPersonReference person;
            if (code == 83)
            {
                var force = ResolveForce(selector, readTargetForce) ?? throw Missing("camera Force0 pointer");
                ushort ruler = Known(Queries.ReadConditionForceRulerPersonId(force), "camera force ruler");
                person = PersonLookup(ruler);
            }
            else if (code == 84) person = ResolvePerson(selector, readTargetForce);
            else throw new ArgumentOutOfRangeException(nameof(code));
            receiver = Queries.ReadConditionPersonHomeBase(person) ?? throw Missing("camera person Home Base");
        }
        // Invalid native Object is the known source word48400. Do not turn
        // it into a missing camera or map it through the Hex lookup here.
        return inputs.ReadCameraBaseHex?.Invoke(receiver) ?? throw Missing("camera current Base Object Hex");
    }
}
