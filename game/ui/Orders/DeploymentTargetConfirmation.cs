using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum DeploymentTargetConfirmationKind { Orders, LowMorale, Discard }

/// <summary>The three ordinary target-page Yes/No messages. The source's
/// explicit LF layout is drawn with original G1N lines; no system font or
/// guessed word wrapping is introduced. The caller owns the resulting action.</summary>
internal partial class DeploymentTargetConfirmation : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private bool ownsResources;
    private Control canvas=null!;
    private Action<bool>? close;
    private UiSourceParagraph? wrappedMessage;
    internal void ApplyWrappedMessage(string message)
    { if(close is not null) wrappedMessage?.Apply(message); }
    internal void RequestCancel()=>Finish(false);
    internal void Abandon(){close=null;Hide();}

    internal void Configure(GameResourceRepository repository,PreviewText text,
        DeploymentTargetConfirmationKind kind,Action<bool> closed,AdministrationPageChrome.Resources? resources=null)
    {
        int record=kind switch
        {DeploymentTargetConfirmationKind.Orders=>0x08A8,DeploymentTargetConfirmationKind.LowMorale=>0x08A9,
            DeploymentTargetConfirmationKind.Discard=>0x08AA,_=>throw new ArgumentOutOfRangeException(nameof(kind))};
        string message=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2].Records[record].Literals.Single().Text;
        ConfigureMessage(repository,text,message,closed,resources);
        SetMeta("message_record",record);
    }

    internal void ConfigureMessage(GameResourceRepository repository,PreviewText text,
        string message,Action<bool> closed,AdministrationPageChrome.Resources? resources=null)
        => ConfigureMessageCore(repository,text,message,closed,false,resources);

    internal void ConfigureWrappedMessage(GameResourceRepository repository,PreviewText text,
        string message,Action<bool> closed,AdministrationPageChrome.Resources? resources=null)
        => ConfigureMessageCore(repository,text,message,closed,true,resources);

    private void ConfigureMessageCore(GameResourceRepository repository,PreviewText text,
        string message,Action<bool> closed,bool wrap,AdministrationPageChrome.Resources? resources)
    {
        Name="DeploymentTargetConfirmation";ZIndex=10;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);close=closed;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new Control{Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var source=assets.SourceLayout(0xDB48324F);Vector2 mount=new(610,672);
        var mask = source.Art(canvas,1,source.Rectangle(1,mount),assets,shader,
            blend:UiSourceSprite.Blend.AlphaMask);
        Rect2 blur = source.Rectangle(2,mount);
        source.Blur(mask,2,new(blur.Position-mask.Position,blur.Size));
        source.Art(canvas,3,source.Rectangle(3,mount),assets,shader);
        Rect2 body=source.Rectangle(4,mount);
        var profile=MapFontProfile.FromNode(source.Nodes[4],source.Source.Source.ResourceUri) with
            {WidthFitMode=3,RawCharacterMode=2};
        if(wrap)
        {
            var paragraph=new UiSourceParagraph{Name="ConfirmationMessage",Position=body.Position,Size=body.Size};
            canvas.AddChild(paragraph);paragraph.Configure(font,profile);paragraph.Apply(message);
            wrappedMessage=paragraph;
        }
        else
        {
            string[] lines=message.Split('\n');
            var metrics=lines.Select(line=>font.Measure(line,profile)).ToArray();
            if(lines.Any(line=>line.Length==0)||metrics.Any(value=>value.Width>body.Size.X))
                throw new InvalidDataException("Deployment confirmation requires its source wrapping branch.");
            int[] extents=metrics.Select(value=>value.Height+profile.VerticalSpace).ToArray();
            // AD5F20 centres sum(line extents)-trailing space using signed float
            // truncation. These audited literals use two or three full-height lines.
            int top=-(int)(-.5f*(body.Size.Y+profile.VerticalSpace-extents.Sum()));
            var lineProfile=profile with{RawAlign=(byte)(profile.RawAlign&0xF0),VerticalSpace=0};
            for(int index=0;index<lines.Length;index++)
            {
                var line=new UiSourceText{Name="MessageLine"+index,Position=body.Position+new Vector2(0,top),
                    Size=new(body.Size.X,metrics[index].Height)};canvas.AddChild(line);
                line.Configure(font,lineProfile);line.Text=lines[index];top+=extents[index];
            }
        }
        Button("ConfirmYes",5,"000_cmn_txt_decision_yes",()=>Finish(true));
        Button("ConfirmNo",13,"000_cmn_txt_decision_no",RequestCancel,cue:UiAudioCue.Cancel);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        Resized+=Fit;Fit();

        void Button(string name,int slot,string caption,Action released,UiAudioCue cue=UiAudioCue.ButtonSelect)
        {
            var button=new UiSourceRoundButton{Name=name,Position=source.Rectangle(slot,mount).Position,
                ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(button);
            button.Configure(repository,assets,shader,text.SourceLocale,caption,released,cue);
        }
    }

    internal void ConfigureGameMessage(GameResourceRepository repository, PreviewText text,
        int messageId, Action<bool> closed)
    {
        var record = repository.LoadMessages(text.SourceLocale, MessageKind.Game)
            .Messages.Sections[messageId / 10000].Records[messageId % 10000];
        // This caller supplies a complete plain literal, with its original LF.
        // A changed program must not silently lose references or controls.
        byte[] bytes = record.RawBytes;
        if (record.Literals is not [{ Offset: 3 } literal] || bytes.Length != literal.ByteLength + 9 ||
            !bytes.AsSpan(0, 3).SequenceEqual(new byte[] { 7, 7, 1 }) ||
            !bytes.AsSpan(bytes.Length - 6).SequenceEqual(new byte[] { 7, 7, 2, 5, 5, 5 }))
            throw new InvalidDataException("Confirmation requires a complete literal message.");
        ConfigureMessage(repository, text, literal.Text, closed);
        SetMeta("message_global_id", messageId);
    }

    private void Finish(bool accepted)
    {
        var callback=close;if(callback is null)return;close=null;Hide();callback(accepted);
    }

    private void Fit()
    {
        if(canvas is null)return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;
        canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree(){if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}}
}
