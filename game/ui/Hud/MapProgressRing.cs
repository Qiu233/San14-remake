using Godot;
using San14.Data;

/// <summary>The progress button's authored base and two animated UV mask halves.</summary>
internal partial class MapProgressRing : Control
{
    private UiSourceRadialGauge gauge=null!;
    private int frame=-1;
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader)
    {
        Name="MapPeriodProgressRing";MouseFilter=MouseFilterEnum.Ignore;
        var source=new UiSourceLayout(repository,0xC7725FCB);
        Size=source.Rectangle(0).Size;
        source.Art(this,2,source.Rectangle(2),assets,shader);
        gauge=new UiSourceRadialGauge {Name="PeriodProgressGauge"};AddChild(gauge);
        gauge.ConfigureProgressButton(source,assets,shader);ApplyFrame(0);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void ApplyFrame(int value)
    {
        if((uint)value>100)throw new ArgumentOutOfRangeException(nameof(value));
        if(frame==value)return;
        frame=value;gauge.ApplySourceFrame(gauge.StartFrame+frame);
        SetMeta("source_progress_frame",frame);
    }
}
