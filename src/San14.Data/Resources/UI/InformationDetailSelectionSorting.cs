namespace San14.Data;

/// <summary>The actual detail-header predicates over already prepared keys.
/// This defines comparison, not a claim about the original sort algorithm's
/// equal-row stability. No World/resource lookup occurs during comparison.</summary>
public static class InformationDetailSelectionSorting
{
    public static int Compare(InformationListKind kind,InformationDetailSelectionItem left,
        InformationDetailSelectionItem right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if(kind==InformationListKind.Districts)
        {
            int a=Need(left.SortForceId,left,"District_Force"),b=Need(right.SortForceId,right,"District_Force");
            if(a==b)return Need(left.SortDistrictNumber,left,"District_number").CompareTo(
                Need(right.SortDistrictNumber,right,"District_number"));
            bool ap=Need(left.SortForceIsPlayer,left,"current_player_Force"),bp=Need(right.SortForceIsPlayer,right,"current_player_Force");
            if(ap!=bp)return ap?-1:1;
            return a.CompareTo(b);
        }
        if(kind is not(InformationListKind.Cities or InformationListKind.Gates or InformationListKind.Areas or InformationListKind.Forces or InformationListKind.Officers or InformationListKind.Items))
            throw new ArgumentOutOfRangeException(nameof(kind));
        string x=left.SortText ?? throw Missing(left,"text_key"),y=right.SortText ?? throw Missing(right,"text_key");
        //282A60's wcsncmp(...,0x400): unsigned UTF16 units, stop on
        //the first null; equal at the limit remains equal, with no ID tie.
        for(int i=0;i<1024;i++)
        {
            char ac=i<x.Length?x[i]:'\0',bc=i<y.Length?y[i]:'\0';
            if(ac!=bc)return ac<bc?-1:1;
            if(ac=='\0')return 0;
        }
        return 0;
    }

    private static T Need<T>(T? value,InformationDetailSelectionItem row,string field)where T:struct
        =>value ?? throw Missing(row,field);
    private static InvalidDataException Missing(InformationDetailSelectionItem row,string field)
        =>new(row.SortUnresolvedOwner ?? "information-detail.selection.sort."+field);
}
