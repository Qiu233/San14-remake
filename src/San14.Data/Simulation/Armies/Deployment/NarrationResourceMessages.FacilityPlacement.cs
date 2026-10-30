namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveFacilityPlacementHint(string name,bool finalEndpoint)
    {
        int root=finalEndpoint?0x56BE:0x56BD;
        _prefix="facility.placement.hint";
        return ExecuteMessage(InformationExplanationArguments.Instance,
            _=>throw Failure("random_argument"),root,Get(root,true),_=>false,true,()=>name,
            _=>false,allowOpaqueEscapes:true).Text;
    }
}
