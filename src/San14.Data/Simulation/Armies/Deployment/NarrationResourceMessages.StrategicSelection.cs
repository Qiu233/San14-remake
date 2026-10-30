namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveStrategicSelectionGuide(int message)
    {
        if(message is not(0x5A3E or 0x5A3F or 0x5A5A or 0x5A4F or 0x5A49 or 0x5C04))
            throw Failure("strategic_selection_guide_root");
        _prefix="strategic.selection.guide";
        bool previous=_report;_report=true;
        try
        {
            // These source setters supply a literal Game guide, with no
            // target substitutions, World, random or argument state.
            return ExecuteMessage(StrategicSelectionGuideArguments.Instance,
                _=>throw Failure("random_argument"),message,Get(message,true),
                _=>false,true,null,_=>false,
                (group,code)=>(group,code) switch
                { ('C','J')=>"^02",('C','L')=>"^05",
                    _=>throw Failure($"unresolved_escape_{group:X2}_{code:X2}") },true).Text;
        }
        finally{_report=previous;}
    }
    private sealed class StrategicSelectionGuideArguments:INarrationCurrentContext
    {
        internal static readonly StrategicSelectionGuideArguments Instance=new();
        private static InvalidDataException Missing()=>new("strategic.selection.guide.argument");
        public int PersonValue(int argument,int field)=>throw Missing();
        public string PersonText(int argument,int field)=>throw Missing();
        public int Relationship(int field,int firstPersonId,int secondPersonId)=>throw Missing();
        public string AreaText(int argument,int field)=>throw Missing();
    }
}
