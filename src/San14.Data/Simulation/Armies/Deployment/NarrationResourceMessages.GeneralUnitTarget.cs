namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal NarrationResourceText ResolveGeneralUnitTarget(INarrationCurrentContext people,
        Func<int,int> presentationRandom, bool executeVoiceRequest = false)
    {
        _prefix = "deployment.general_narration";
        //TC and SC5757 are an actual unconditional Jump5668. Preserve this
        //entry contract and reuse its established Voice37 text family and
        //reached Function3 draws; the alias itself has no sampler or operands.
        var alias = Get(0x5757);
        if (alias.Statements is not [Reference { Id:0x5668,Jump:true }])
            throw Failure("resource.5757_alias");
        return Resolve(people,presentationRandom,NarrationResourceFamily.Deployment,executeVoiceRequest)
            with { RootMessageId = 0x5757 };
    }
}
