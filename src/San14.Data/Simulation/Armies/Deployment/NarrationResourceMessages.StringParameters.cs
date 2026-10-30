namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    /// <summary>248 expands all five positive roots against the old string bank.
    /// The caller replaces the bank only after this complete result exists.</summary>
    internal string[] ResolveStoryStringParameters(INarrationCurrentContext context,
        Func<int,int> draw, IReadOnlyList<int> roots)
    {
        if(roots.Count!=5)throw new InvalidDataException("story.string_parameters.root_count");
        var values=new string[5];
        for(int index=0;index<values.Length;index++)
        {
            if(roots[index]<=0){values[index]=string.Empty;continue;}
            string text=ResolveStoryText(context,draw,roots[index]);
            int end=text.IndexOf('\0');
            if(end>=0)text=text[..end];
            //30DCF0 clears its full slot before1D2000 appends into a126-byte
            //capacity, including the UTF16 terminator. Oversize stays empty.
            values[index]=text.Length<=62?text:string.Empty;
        }
        return values;
    }
}
