using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Page-owned ordinary local CLogData slots. Resetting a preparation
/// clears its count, not the constructor-owned windows or unrelated flag bits.
/// These records never enter the saved history ring.</summary>
public sealed class ActionReportLocalRecords
{
    public const int OrdinaryCapacity = 261;
    private readonly CampaignHistoryRecord[] slots = Enumerable.Range(0,OrdinaryCapacity)
        .Select(_=>CampaignHistoryRecord.Empty).ToArray();
    public int Count { get; private set; }
    public ImmutableArray<CampaignHistoryRecord> Records => slots.Take(Count).ToImmutableArray();

    private CampaignHistoryRecord aggregate = CampaignHistoryRecord.Empty;
    public bool HasArmySupplyAggregate { get; private set; }
    public CampaignHistoryRecord? ArmySupplyAggregate => HasArmySupplyAggregate ? aggregate : null;

    internal void BeginPreparation() { Count = 0; HasArmySupplyAggregate = false; }

    internal bool AppendArmySupplyAggregate(ushort speaker, string text)
    {
        if (HasArmySupplyAggregate) return false;
        HasArmySupplyAggregate = true;
        var primary = aggregate.Text.ToArray();
        var supplemental = aggregate.SupplementalText.ToArray();
        ReportPool.CopyTextWindow("", 55, (index, value) => primary[index] = value);
        ReportPool.CopyTextWindow(text, 85, (index, value) => supplemental[index] = value);
        aggregate = aggregate with { Category = 5, DistrictId = 0, HexId = 0, CampaignDay = 0,
            PersonId = (ushort)Math.Clamp((int)speaker, 1, 6000),
            Flags = (byte)(((aggregate.Flags & 0xbf) | 3) & 0xc7),
            Text = primary.ToImmutableArray(), SupplementalText = supplemental.ToImmutableArray() };
        return true;
    }

    //6773F0's two inline append paths and its final6769C0 call have these
    //same actual arguments. This is not a generic guessed local record writer.
    internal bool AppendAreaWarning(ushort speaker,int marker,string text)
        => AppendOrdinaryWarning(speaker,marker,text);

    //675510 has the identical Category1/flags/window writer. Its Army2A
    //marker and fresh speaker identity are supplied after actual formatting.
    internal bool AppendArmyTargetWarning(ushort speaker,int marker,string text)
        => AppendOrdinaryWarning(speaker,marker,text);

    //674DF0 writes the same ordinary Category1 window and flags after its
    //independent Base67 predicate, formatter and fresh22D880 marker reads.
    internal bool AppendBaseNearbyWarning(ushort speaker,int marker,string text)
        => AppendOrdinaryWarning(speaker,marker,text);

    //6749F0's ordinary local writer has the same windows/flags/capacity.
    internal bool AppendAllianceExpiryWarning(ushort speaker,int marker,string text)
        => AppendOrdinaryWarning(speaker,marker,text);

    internal bool AppendForceObjectiveWarning(ushort speaker, int marker, string text)
        => AppendOrdinaryWarning(speaker, marker, text);

    internal bool AppendAttackRequestExpiryWarning(ushort speaker, int marker, string text)
        => AppendOrdinaryWarning(speaker, marker, text);

    private bool AppendOrdinaryWarning(ushort speaker,int marker,string text)
    {
        if(Count>=OrdinaryCapacity)return false;
        int at=Count++;
        var old=slots[at];
        var primary=old.Text.ToArray();var supplemental=old.SupplementalText.ToArray();
        ReportPool.CopyTextWindow("",55,(index,value)=>primary[index]=value);
        ReportPool.CopyTextWindow(text,85,(index,value)=>supplemental[index]=value);
        slots[at]=old with{Category=1,DistrictId=0,HexId=(ushort)Math.Clamp(marker,0,48399),
            CampaignDay=0,PersonId=(ushort)Math.Clamp((int)speaker,1,6000),
            Flags=(byte)(((old.Flags&0xbf)|3)&0xd7|0x10),
            Text=primary.ToImmutableArray(),SupplementalText=supplemental.ToImmutableArray()};
        return true;
    }
}

/// <summary>The three real operand slots written before the Area formatter.
/// Person0 is the supplied speaker; Base0 is selected City's actual4E;
/// Person1 is the previously retained speaker Force's ruler10.</summary>
public sealed record AreaReportFormattingInputs(int MessageId,ushort SpeakerPersonOperand0,
    ushort CityBaseOperand0,ushort RulerPersonOperand1);
