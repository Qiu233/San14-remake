using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed record OriginalRemakeArmyFlagSourcePaneInputs(byte? SourceForceColorIndex49,
    string SelectedSourceText,string CallerForceSource,bool Kind2TextIsPrecedingLoopCarriedBuffer)
{
    public bool ExplicitOwnedFreshNulTextRoute {get;init;}
}

/// <summary>Coherent source-created one-glyph producer for the proven source
/// font metric cohort containing U+66F9. Native font, atlas, layout, packed colors, stock shaders and
/// blend are source-backed. Force fields/text remain explicit; kind2 requires
/// the preceding banner-loop carried text rather than its own force name.
/// Source glyphs outside this proved layout reject until their layout is ported.</summary>
public sealed class OriginalRemakeArmyFlagCaoPaneProducer:IAsyncDisposable
{
    private sealed record Pane(OriginalRemakeArmyFlagPaneImage Image,OriginalRemakeArmyFlagGlyphRenderer? Renderer,
        string InputsFingerprint,string AtlasHash);
    private sealed record Atlas(LoadedOriginalArmyFlagGlyphAtlasPrefix Prefix,OriginalRemakeArmyFlagGlyphAtlas Device);
    private readonly OriginalGameRepository _repository;
    private readonly string _locale;
    private readonly Dictionary<uint,Pane> _panes=[];
    private LoadedOriginalMapExposureSource? _stock;
    private LoadedOriginalArmyFlagFontSource? _font;
    private readonly Dictionary<ushort,Atlas> _atlases=[];
    private OriginalArmyFlagPaneColors? _colors;
    private Vkd3dShaderCompiler? _compiler;
    private readonly bool _ownsCompiler;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();private bool _retiring;private int _accepted;private TaskCompletionSource? _drained;private Task? _disposal;

    /// <summary>A supplied compiler is borrowed and must outlive the glyph
    /// renderer. Standalone producers lazily own one from the normal runtime
    /// resolver; the working directory never selects a different library.</summary>
    public OriginalRemakeArmyFlagCaoPaneProducer(OriginalGameRepository repository,string locale,
        Vkd3dShaderCompiler? borrowedCompiler=null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(locale is not("SC" or"TC"))throw new ArgumentException("Source pane locale must beSC/TC.",nameof(locale));
        _repository=repository;_locale=locale;_compiler=borrowedCompiler;_ownsCompiler=borrowedCompiler is null;
    }

    public Task<OriginalRemakeArmyFlagRuntimeTexture> PrepareAsync(LoadedOriginalArmyFlagMaterialVariation material,
        OriginalRemakeArmyFlagSourcePaneInputs input)
    {
        ArgumentNullException.ThrowIfNull(material);ArgumentNullException.ThrowIfNull(input);
        if(string.IsNullOrWhiteSpace(input.CallerForceSource)||input.SelectedSourceText is null||
            input.SelectedSourceText.Length!=0&&input.SelectedSourceText[0] is '\r' or '\n'||
            (input.SelectedSourceText.Length==0||input.SelectedSourceText[0]=='\0')&&!input.ExplicitOwnedFreshNulTextRoute||
            material.ManagerRow.ManagerKind==2&&!input.Kind2TextIsPrecedingLoopCarriedBuffer)
            throw new InvalidDataException("Source flag pane requires supported single-unit text, original force provenance and explicit kind2 preceding-loop carried buffer.");
        lock(_lifetime){if(_retiring)throw new ObjectDisposedException(nameof(OriginalRemakeArmyFlagCaoPaneProducer));_accepted++;}
        return PrepareAcceptedAsync(material,input);
    }

    private async Task<OriginalRemakeArmyFlagRuntimeTexture> PrepareAcceptedAsync(LoadedOriginalArmyFlagMaterialVariation material,
        OriginalRemakeArmyFlagSourcePaneInputs input)
    {
        await _gate.WaitAsync();
        try
        {
            _stock??=OriginalMapExposureSource.Load(_repository,_locale);
            _font??=OriginalArmyFlagFontSources.LoadFreshHostDefault(_repository,true,"owned fresh native Host/UIprefix selector+460=0; source-created flag pane");
            _colors??=OriginalArmyFlagPaneColors.LoadCurrent(_repository.SourceDirectory,_locale);
            if(input.SelectedSourceText.Length==0||input.SelectedSourceText[0]=='\0')
                return await PrepareEmptyAsync(material,input);
            ushort code=input.SelectedSourceText[0];
            if(!_atlases.TryGetValue(code,out var atlas))
            {
                var glyph=OriginalArmyFlagFontSources.ReadGlyph(_font,code,0,0);
                var prefix=OriginalArmyFlagGlyphAtlasPrefix.WriteFirst(_font,glyph,new byte[OriginalArmyFlagGlyphAtlasPrefix.AtlasByteLength],
                    "owned caller cleared completeatlas template; native first-insertion writes; no originalVRAMdefaultclaim",true);
                // Validate the supported layout before allocating GPU resources.
                var checkedColors=_colors.Resolve(input.SourceForceColorIndex49,input.CallerForceSource);
                var checkedText=OriginalArmyFlagPaneColors.CopySourceNameGlyph(input.SelectedSourceText,input.CallerForceSource);
                _=OriginalArmyFlagFreshPaneLayout.BuildSameMetricGlyph(_font,prefix,checkedText,checkedColors,
                    material.ManagerRow.ManagerKind,new byte[48],"owned complete glyph record template",true);
                var device=await OriginalRemakeArmyFlagGlyphAtlas.BuildAsync(prefix,_stock.Native);
                _atlases.Add(code,atlas=new(prefix,device));
            }
            var colors=_colors.Resolve(input.SourceForceColorIndex49,input.CallerForceSource);
            var text=OriginalArmyFlagPaneColors.CopySourceNameGlyph(input.SelectedSourceText,input.CallerForceSource+
                (material.ManagerRow.ManagerKind==2?"; sourcekind2 precedingbannerloop carriedbuffer":"; sourcebanner selectedForce/fallbackname"));
            var layout=OriginalArmyFlagFreshPaneLayout.BuildSameMetricGlyph(_font,atlas.Prefix,text,colors,material.ManagerRow.ManagerKind,
                new byte[48],"owned cleared completeglyphrecord template; opaquehandle separatelybound to ownedsourceatlas",true);
            var use=material.TextureUses.Single(t=>t.TextureRegister==0);
            string fingerprint=string.Join('/',material.SelectedMaterialObjectId,input.SourceForceColorIndex49,input.SelectedSourceText,
                input.CallerForceSource,input.Kind2TextIsPrecedingLoopCarriedBuffer,colors.BackgroundTint.PanePacked,
                colors.BannerGlyphColor.PanePacked,material.ManagerRow.ManagerKind,atlas.Prefix.WrittenAtlasSha256);
            if(_panes.TryGetValue(use.Slot.ProxyObjectId,out var cached)&&cached.InputsFingerprint==fingerprint&&cached.Image.CompleteProducerReady)
                return cached.Image.BorrowCompletedProducer(layout.NativeSourceProvenance+"; "+text.CallerSource);
            _compiler??=OriginalShaderCompilerRuntime.Open();
            bool ownsNewPane=cached is null;
            var image=cached?.Image??await OriginalRemakeArmyFlagPaneImage.BuildAsync(use,_stock.Native);
            bool replaceRenderer=cached is not null&&cached.AtlasHash!=atlas.Prefix.WrittenAtlasSha256;
            OriginalRemakeArmyFlagGlyphRenderer? renderer=replaceRenderer?null:cached?.Renderer;
            try
            {
                renderer??=await OriginalRemakeArmyFlagGlyphRenderer.BuildAsync(_stock,_compiler,image,atlas.Device);
                await image.ClearSourceTintAsync(colors.BackgroundTint.PanePacked,input.CallerForceSource+"; nativefirstpalette LUT/sourceclear");
                byte[] vs=new byte[32],ps=new byte[16];float[] values=[2f/128,-2f/64,-1,1,1,1,1,1];
                for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(vs.AsSpan(i*4,4),values[i]);
                BinaryPrimitives.WriteInt32LittleEndian(ps,unchecked((int)0xFF7FFFFF));
                await renderer.DrawFreshSourceLayoutAsync(layout,new(vs,ps,"owned sourcefresh stockscreen scale1/colour1/nullcustomVS/mode3/ctoralphathreshold; primarytarget128x64"));
                if(replaceRenderer&&cached!.Renderer is not null)await cached.Renderer.DisposeAsync();
                _panes[use.Slot.ProxyObjectId]=new(image,renderer,fingerprint,atlas.Prefix.WrittenAtlasSha256);
                return image.BorrowCompletedProducer(layout.NativeSourceProvenance+"; "+text.CallerSource);
            }
            catch{if(ownsNewPane||replaceRenderer){if(renderer is not null)await renderer.DisposeAsync();if(ownsNewPane)await image.DisposeAsync();}throw;}
        }
        finally{_gate.Release();lock(_lifetime){_accepted--;if(_retiring&&_accepted==0)_drained?.TrySetResult();}}
    }
    private async Task<OriginalRemakeArmyFlagRuntimeTexture> PrepareEmptyAsync(LoadedOriginalArmyFlagMaterialVariation material,
        OriginalRemakeArmyFlagSourcePaneInputs input)
    {
        var colors=_colors!.Resolve(input.SourceForceColorIndex49,input.CallerForceSource);
        var text=OriginalArmyFlagPaneColors.CopySourceNameGlyph(input.SelectedSourceText,input.CallerForceSource);
        var layout=OriginalArmyFlagEmptyPaneLayout.Build(_font!,text,colors,material.ManagerRow.ManagerKind,
            input.ExplicitOwnedFreshNulTextRoute);
        var use=material.TextureUses.Single(t=>t.TextureRegister==0);
        string fingerprint=string.Join('/',material.SelectedMaterialObjectId,input.SourceForceColorIndex49,input.CallerForceSource,
            input.Kind2TextIsPrecedingLoopCarriedBuffer,colors.BackgroundTint.PanePacked,material.ManagerRow.ManagerKind,"nativefreshNUL/zero-records");
        if(_panes.TryGetValue(use.Slot.ProxyObjectId,out var cached)&&cached.InputsFingerprint==fingerprint&&cached.Image.CompleteProducerReady)
            return cached.Image.BorrowCompletedProducer(layout.NativeSourceProvenance+"; "+text.CallerSource);
        var image=cached?.Image??await OriginalRemakeArmyFlagPaneImage.BuildAsync(use,_stock!.Native);
        try
        {
            await image.ClearSourceTintAsync(colors.BackgroundTint.PanePacked,input.CallerForceSource+"; native firstpalette/zero-glyph job");
            image.PublishCompletedSourceEmpty(layout);
            if(cached?.Renderer is not null)await cached.Renderer.DisposeAsync();
            _panes[use.Slot.ProxyObjectId]=new(image,null,fingerprint,"");
            return image.BorrowCompletedProducer(layout.NativeSourceProvenance+"; "+text.CallerSource);
        }
        catch{if(cached is null)await image.DisposeAsync();throw;}
    }
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        Task pending;lock(_lifetime)pending=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending;await _gate.WaitAsync();
        try
        {
            foreach(var pane in _panes.Values){if(pane.Renderer is not null)await pane.Renderer.DisposeAsync();await pane.Image.DisposeAsync();}_panes.Clear();
            foreach(var atlas in _atlases.Values)await atlas.Device.DisposeAsync();_atlases.Clear();
            if(_ownsCompiler)_compiler?.Dispose();_compiler=null;
        }
        finally{_gate.Release();}
    }
}
