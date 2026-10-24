using Godot;
using San14.Core;
using San14.Data;

/// <summary>Ordered records after the report's state-owned selection and empty
/// fallback initialization. The local fallback is never a saved history write.</summary>
internal sealed record ActionReportListContent(IReadOnlyList<CampaignHistoryRecord> Records,bool UsesEmptyFallback);

/// <summary>The original report's pixel viewport and circular row pool. Each
/// category owns its scrollbar and offset for the lifetime of the report.</summary>
internal partial class ActionReportList : Control
{
    private Control clip=null!;
    private Control[] rows=[];
    private int[] recordIndices=[];
    private CampaignHistoryRecord[] records=[];
    private UiSourceScrollbar scrollbar=null!;
    private Func<CampaignHistoryRecord,ActionReportDetailRowContent>? resolveDetails;
    private Action<int,CampaignHistoryRecord?,int>? dispatch;
    private int stride;

    internal bool UsesEmptyFallback {get;private set;}
    internal int ScrollOffset=>scrollbar.Value;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        string locale,bool allRecords,ActionReportListContent content,Rect2 scrollbarMount,int scrollSpeedSetting,
        Func<int>? currentScrollSpeed,Func<CampaignHistoryRecord,ActionReportDetailRowContent> resolveDetails,
        Action<int,CampaignHistoryRecord?,int> dispatch)
    {
        if(content.Records.Count==0||content.UsesEmptyFallback&&content.Records.Count!=1)
            throw new ArgumentException("A prepared report list requires its actual local fallback when empty.",nameof(content));
        this.resolveDetails=resolveDetails;this.dispatch=dispatch;
        records=content.Records.ToArray();UsesEmptyFallback=content.UsesEmptyFallback;
        MouseFilter=MouseFilterEnum.Pass;
        clip=new Control{Name="ReportListClip",Size=Size,ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        AddChild(clip);
        var first=NewRow(0);stride=(int)first.Size.Y;
        int count=(int)Size.Y/stride+2;
        rows=new Control[count];recordIndices=new int[count];rows[0]=first;
        for(int i=1;i<count;i++)rows[i]=NewRow(i);
        scrollbar=new UiSourceScrollbar{Name="ReportScrollbar",Position=scrollbarMount.Position,Size=scrollbarMount.Size};
        AddChild(scrollbar);
        scrollbar.Configure(repository,assets,shader,Math.Max(checked(records.Length*stride)-(int)Size.Y,0),
            scrollSpeedSetting,_=>PositionRows(),currentScrollSpeed:currentScrollSpeed);
        GuiInput+=scrollbar.HandleWheel;
        clip.GuiInput+=scrollbar.HandleWheel;
        PositionRows();

        Control NewRow(int poolIndex)
        {
            if(allRecords)
            {
                var row=new ActionReportAllRow{Name="AllRecordRow"+poolIndex};clip.AddChild(row);
                row.Configure(repository,assets,shader,font);return row;
            }
            var detail=new ActionReportDetailRow{Name="DetailRecordRow"+poolIndex};clip.AddChild(detail);
            detail.Configure(repository,assets,shader,font,locale,(sender,code)=>
                this.dispatch?.Invoke(recordIndices[poolIndex],sender.Record,code));
            return detail;
        }
    }

    private void PositionRows()
    {
        int scroll=scrollbar.Value,first=scroll/stride,poolStart=first%rows.Length;
        for(int i=0;i<rows.Length;i++)
        {
            int slot=(poolStart+i)%rows.Length,index=first+i;
            var row=rows[slot];
            row.Position=new(row.Position.X,i*stride+first*stride-scroll);
            recordIndices[slot]=index;
            row.Visible=index<records.Length;
            if(!row.Visible)continue;
            var record=records[index];
            if(row is ActionReportAllRow all)all.Apply(record);
            else
            {
                var value=resolveDetails!(record);
                if(!ReferenceEquals(value.Record,record))
                    throw new InvalidDataException("Report detail identity differs from its ordered record.");
                ((ActionReportDetailRow)row).Apply(value);
            }
        }
    }

    public override void _ExitTree(){dispatch=null;resolveDetails=null;}
}
