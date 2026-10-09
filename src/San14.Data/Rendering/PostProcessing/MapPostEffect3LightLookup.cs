namespace San14.Data;

/// <summary>Inert representation of one already published sorted primitive.
/// This evaluator does not establish prefix completeness or current ownership.</summary>
public sealed record MapPostEffect3LightLookupPrimitive(ulong PackedWord0,
    bool RecordPointerPresent,uint RecordMask78,MapSourceCreatedLightingPacket? Payload8);

public sealed record MapPostEffect3LightLookupResult(int NormalizedReadCount,
    int? MatchingPrimitiveIndex,MapSourceCreatedLightingPacket? Payload8);

public static class MapPostEffect3LightLookup
{
    /// <summary>Exact finite scan, including negative count normalization,
    /// signed packed-kind comparison and first matching null-payload return.
    /// The complete prefix must cover the resulting read count; no truncation
    /// or search beyond the first matching record is permitted.</summary>
    public static MapPostEffect3LightLookupResult ReadSortedPrefix(
        IReadOnlyList<MapPostEffect3LightLookupPrimitive> completePrefix,
        int actualReadCount19D4,int actualCapacity19D0,uint exclusionMask)
    {
        ArgumentNullException.ThrowIfNull(completePrefix);
        if(actualCapacity19D0<0)throw new InvalidDataException("Native sorted-prefix capacity is negative.");
        int count=actualReadCount19D4<0?Math.Min(~actualReadCount19D4,actualCapacity19D0):actualReadCount19D4;
        if(count<0 || count>completePrefix.Count)
            throw new InvalidDataException("Actual  read count exceeds its complete owned sorted prefix");
        for(int i=0;i<count;i++)
        {
            var p=completePrefix[i] ??throw new InvalidDataException("Owned sorted primitive pointer is absent.");
            if(unchecked((int)(p.PackedWord0>>28))>1)break;
            if(!p.RecordPointerPresent || (p.RecordMask78&exclusionMask)!=0)continue;
            return new(count,i,p.Payload8); // Null payload is a final result too.
        }
        return new(count,null,null);
    }
}
