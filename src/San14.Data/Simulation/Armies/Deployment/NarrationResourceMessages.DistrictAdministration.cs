namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveDistrictAdministrationGuide(int message)
    {
        //Only actual closed district UI roots. All are no-argument guide
        //programs; an unexpected current/random operand stays unresolved.
        if (message is not (0x2733 or 0x569D or 0x590E or 0x590F or 0x5910 or 0x5911 or 0x5912 or
            0x5914 or 0x5916 or 0x5917 or 0x5918 or 0x591A or 0x591C or 0x591D or
            >= 0x5921 and <= 0x592D or >= 0x5930 and <= 0x5937 or 0x5939 or 0x593A or
            0x593B or 0x593D or 0x593E or 0x593F or >= 0x5940 and <= 0x5945 or 0x5948 or
            >= 0x594A and <= 0x5951 or 0x5953 or >= 0x595A and <= 0x595D or >= 0x595F and <= 0x5962))
            throw Failure("district.guide_root");
        _prefix = "district.guide";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(PersonnelAdministrationGuideArguments.Instance,
                _ => throw Failure("random_argument"),message,Get(message,true),
                _ => false,true,null,_ => false,
                (group,code) => (group,code) switch
                { ('C','J') => "^02", ('C','L') => "^05", _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") },true).Text;
        }
        finally { _report = previous; }
    }
}
