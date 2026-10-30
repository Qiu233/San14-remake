using System.Text;
using San14.Data;
using San14.Data.Formats;

/// <summary>The government caller's finite resource-message dialect.</summary>
internal static class GovernanceRosterMessages
{
    internal static string Format(GameResourceRepository repository,string locale,int id,string? department=null)
    {
        var record=repository.LoadMessages(locale,MessageKind.Game).Messages.Sections[id/10000].Records[id%10000];
        var bytes=record.RawBytes;var result=new StringBuilder();int at=0;
        while(at<bytes.Length)
        {
            if(at+3<=bytes.Length&&bytes[at]==7&&bytes[at+1]==7&&bytes[at+2]==1)
            {
                var literal=record.Literals.Single(value=>value.Offset==at+3);
                result.Append(literal.Text);at+=3+literal.ByteLength;
                if(at+3>bytes.Length||bytes[at]!=7||bytes[at+1]!=7||bytes[at+2]!=2)throw Invalid();
                at+=3;
            }
            else if(at+2<=bytes.Length&&bytes[at]==2)
            {
                byte operation=bytes[at+1];at+=2;
                if(operation==0x3C)result.Append(department??throw new InvalidDataException("Government message requires its department."));
                // 32/30 delimit the source's highlighted military-adviser word.
                // This notice surface retains its resource text in one font run.
                else if(operation is not (0x32 or 0x30))throw Invalid();
            }
            else if(at+3==bytes.Length&&bytes[at]==5&&bytes[at+1]==5&&bytes[at+2]==5)break;
            else throw Invalid();
        }
        return result.ToString();
        InvalidDataException Invalid()=>new($"Unsupported government message {id:X} at {at}.");
    }
    internal static string Numbers(string format,params int[] values)
    {
        int at=0;var result=new StringBuilder();int argument=0;
        while(at<format.Length)
        {
            if(format[at]=='%'&&at+1<format.Length&&format[at+1]=='d')
            {if(argument>=values.Length)throw new InvalidDataException("Missing government format argument.");result.Append(values[argument++]);at+=2;}
            else result.Append(format[at++]);
        }
        if(argument!=values.Length)throw new InvalidDataException("Unused government format argument.");
        return result.ToString();
    }
}
