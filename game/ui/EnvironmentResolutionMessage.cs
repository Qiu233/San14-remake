using San14.Data;
using San14.Data.Formats;
using System.Globalization;

/// <summary>The timed resolution question owns one integer argument. Its
/// two source literals and integer0 opcode are parsed once per actual dialog.</summary>
internal sealed class EnvironmentResolutionMessage
{
    private readonly string?[] parts;
    internal EnvironmentResolutionMessage(GameResourceRepository repository,string locale)
    {
        const int id=0x275B;
        var record=repository.LoadMessages(locale,MessageKind.Game).Messages.Sections[id/10000].Records[id%10000];
        byte[] bytes=record.RawBytes;
        var parsed=new List<string?>();
        int at=0;bool terminated=false;
        while(at<bytes.Length)
        {
            if(Has(7,7,1))
            {
                var literal=record.Literals.Single(row=>row.Offset==at+3);
                parsed.Add(literal.Text);at+=3+literal.ByteLength;
                if(!Has(7,7,2))throw Missing();
                at+=3;
            }
            else if(at+2<=bytes.Length&&bytes[at]==2&&bytes[at+1]==0x32)
            {parsed.Add(null);at+=2;}
            else if(Has(5,5,5))
            {at+=3;if(at!=bytes.Length)throw Missing();terminated=true;break;}
            else throw Missing();
        }
        if(!terminated||parsed.Count(part=>part is null)!=1)throw Missing();
        parts=parsed.ToArray();
        bool Has(byte a,byte b,byte c)=>at+3<=bytes.Length&&bytes[at]==a&&bytes[at+1]==b&&bytes[at+2]==c;
        InvalidDataException Missing()=>new("environment.resolution.confirmation_program."+at);
    }
    internal string Format(int seconds)=>string.Concat(parts.Select(part=>part??seconds.ToString(CultureInfo.InvariantCulture)));
}
