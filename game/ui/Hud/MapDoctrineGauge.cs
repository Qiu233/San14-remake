using Godot;

/// <summary>The map header's real doctrine action, including its alpha tracks.</summary>
internal partial class MapDoctrineGauge : Control
{
    private UiSourceRadialGauge gauge=null!;
    internal void Configure(UiSourceLayout source,UiAssets assets,Shader shader)
    {
        Name="DoctrineGauge";MouseFilter=MouseFilterEnum.Ignore;Size=source.Rectangle(0).Size;
        gauge=new(){Name="DoctrineGaugeSource"};AddChild(gauge);
        gauge.Configure(source,assets,shader,"gauge",20,21,true);
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_action","gauge");Apply(null);
    }
    internal void Apply(int? percentage)
    {
        Visible=percentage is >=0 and <=100;SetMeta("progress_known",Visible);
        if(!Visible)return;
        float frame=percentage!.Value+gauge.StartFrame;gauge.ApplySourceFrame(frame);
        SetMeta("percentage",percentage.Value);SetMeta("source_frame",frame);
    }
}
