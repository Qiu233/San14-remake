using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original integer calculator. Only its working value changes
/// until the caller receives one confirmed result.</summary>
internal partial class DeploymentCalculatorDialog : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!;
    private UiSourceText input=null!;
    private uint minimum,maximum,working;
    private bool hasDigit;
    private Action<int?>? close;
    internal int WorkingValue=>(int)working;

    internal void Configure(GameResourceRepository repository,PreviewText text,int value,int minimum,int maximum,
        Action<int?> closed,Vector2? panelPosition=null,int? titleMessage=null)
    {
        if(minimum<0||maximum<minimum)throw new ArgumentOutOfRangeException(nameof(minimum));
        this.minimum=(uint)minimum;this.maximum=(uint)maximum;close=closed;
        Name="DeploymentCalculator";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new Control {Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        canvas.AddChild(new BackBufferCopy {CopyMode=BackBufferCopy.CopyModeEnum.Viewport});
        var panel=new Control {Name="CalculatorPanel",Position=panelPosition??new(1280,600),Size=new(302,372),MouseFilter=MouseFilterEnum.Stop};
        canvas.AddChild(panel);
        var source=new UiSourceLayout(repository,0x0AF34C58);
        source.Blur(panel,1,source.Rectangle(1));
        source.Art(panel,2,source.Rectangle(2),assets,shader);
        source.Art(panel,112,source.Rectangle(112),assets,shader);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing calculator message.");
        var presets=MapTextPresets.Create();
        if(titleMessage is int caption)
        {
            var header=new UiSourceLayout(repository,0xDC8AC8AA);
            header.Art(canvas,1,header.Rectangle(1),assets,shader);
            header.Text(canvas,2,default,Message(caption),font,presets[56]);
        }
        Text(108,Message(0x05E1),false,presets[13]);
        Text(109,Number(this.minimum),true);
        Text(110,Message(0x060B),false);
        Text(111,Number(this.maximum),true);
        input=Text(113,"",true);
        int[] nodes=[75,81,3,21,27,33,39,45,51,57,63,69,9,15,94,88,100,106];
        string[] captions=[Message(0x060A),Message(0x0609),"0","1","2","3","4","5","6","7","8","9",
            "00","000",Message(0x005B),Message(0x056D),Message(0x026D),""];
        for(int i=0;i<nodes.Length;i++)
        {
            int action=i;Rect2 box=source.Rectangle(nodes[i]);
            var button=new UiSourceCalculatorButton {Name="CalculatorAction"+i,Position=box.Position,Size=box.Size};
            panel.AddChild(button);button.Configure(repository,assets,shader,font,captions[i],i==17,()=>ApplyAction(action));
            button.SetMeta("source_replacement_node",nodes[i]);
        }
        SetWorking(unchecked((uint)Math.Clamp(value,minimum,maximum)));hasDigit=false;
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        Resized+=Fit;Fit();

        UiSourceText Text(int node,string label,bool numeric,MapTextPreset? preset=null)
        {
            var control=source.Text(panel,node,default,label,font);
            var profile=MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri) with
                {RawAlign=numeric?(byte)0x21:(byte)0x11};
            if(!numeric)profile=profile with{RawLineArrange=2};
            control.Configure(font,preset is null?profile:profile.WithPreset(preset));return control;
        }
    }
    private static string Number(uint value)=>value.ToString(CultureInfo.InvariantCulture);
    private void SetWorking(uint value){working=Math.Min(value,99_999_999u);input.Text=Number(working);}
    private void Digit(int digit)
    {
        if(!hasDigit){working=0;hasDigit=true;}
        SetWorking(unchecked(working*10+(uint)digit));
    }
    internal void ApplyAction(int action)
    {
        if(close is null)return;
        switch(action)
        {
            case 0:SetWorking(minimum);break;
            case 1:SetWorking(maximum);break;
            case >=2 and <=11:Digit(action-2);break;
            case 12:SetWorking(unchecked(working*100));break;
            case 13:SetWorking(unchecked(working*1000));break;
            case 14:SetWorking(0);break;
            case 15:SetWorking(working/10);break;
            case 16:Finish((int)Math.Max(Math.Min(working,maximum),minimum));break;
            case 17:RequestCancel();break;
        }
    }
    internal void RequestCancel()=>Finish(null);
    internal void Abandon(){close=null;Hide();}
    private void Finish(int? result)
    {
        var callback=close;if(callback is null)return;close=null;Hide();callback(result);
    }
    public override void _Input(InputEvent inputEvent)
    {
        if(close is null||!IsVisibleInTree())return;
        // A decision callback may detach this dialog immediately. The input
        // still belongs to the viewport which dispatched it.
        var viewport=GetViewport();
        if(inputEvent is InputEventMouseButton {ButtonIndex:MouseButton.Right,Pressed:true})RequestCancel();
        else if(inputEvent is InputEventKey key)
        {
            if(key.Pressed&&!key.Echo)
            {
                Key code=key.Keycode;
                if(code==Key.Escape)RequestCancel();
                else if(code is Key.Enter or Key.KpEnter)ApplyAction(16);
                else if(code is Key.Backspace or Key.Delete)ApplyAction(15);
                else if(code>=Key.Key0&&code<=Key.Key9)ApplyAction((int)(code-Key.Key0)+2);
                else if(code>=Key.Kp0&&code<=Key.Kp9)ApplyAction((int)(code-Key.Kp0)+2);
            }
        }
        else return;
        viewport.SetInputAsHandled();
    }
    private void Fit()
    {
        float factor=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*factor;
        canvas.Position=(Size-new Vector2(1920,1080)*factor)/2;
    }
    public override void _ExitTree(){font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
