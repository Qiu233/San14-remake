namespace San14.Data;

public sealed partial class D3D11NormalPostResources
{
    private List<MapPostEffect3Binding> Declared(int pass)=>allocation.Passes[pass].ActiveBindings.ToList();

    private void Bind(PassMedia pass,MapPostEffect3TextureParameterState table,IEnumerable<MapPostEffect3Binding> bindings)
    {
        var effective=bindings.ToArray();
        foreach(var binding in effective.Where(value=>value.Role==MapPostEffect3BindingRole.ShaderResourceRead)
            .SelectMany(value=>new[]{(Mask:1,Stage:"CS"),(Mask:2,Stage:"VS"),(Mask:4,Stage:"PS")}
                .Where(stage=>(value.StageMask&stage.Mask)!=0).Select(stage=>(stage.Stage,Binding:value)))
            .GroupBy(value=>(value.Stage,value.Binding.RegisterIndex)).Select(group=>group.Last()))
        {
            var value=binding.Binding;
            var resource=value.Table==MapPostEffect3BindingTable.BufferParameters
                ?(BufferName(value.ParameterIndex),0):Texture(table,value.ParameterIndex,Math.Max(0,(int)value.MipSelector));
            if(value.Table==MapPostEffect3BindingTable.BufferParameters)buffers.Add(value.ParameterIndex);
            pass.Views.Add(new(binding.Stage,value.RegisterIndex,resource.Item1,resource.Item2));
        }
        foreach(var binding in effective.Where(value=>value.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite)
            .GroupBy(value=>value.RegisterIndex).Select(group=>group.Last()))
        {
            string resource;
            if(binding.Table==MapPostEffect3BindingTable.BufferParameters)
            {resource=BufferName(binding.ParameterIndex);buffers.Add(binding.ParameterIndex);}
            else resource=Texture(table,binding.ParameterIndex).Name;
            pass.Unordered.Add((binding.RegisterIndex,resource));
        }
        foreach(var binding in effective.Where(value=>value.Role==MapPostEffect3BindingRole.RenderTargetWrite)
            .GroupBy(value=>value.RegisterIndex).Select(group=>group.Last()).OrderBy(value=>value.RegisterIndex))
        {
            var resource=Texture(table,binding.ParameterIndex,Math.Max(0,(int)binding.MipSelector));
            pass.Targets.Add(DescribeTarget(binding.RegisterIndex,resource.Name,resource.Mip));
        }
    }

    private static string BufferName(int index)=>index==8?"Histogram8":"Buffer"+index;

    private static MapPostEffect3Binding Dynamic(int index,int slot,bool target)=>new(-1,
        MapPostEffect3BindingTable.TextureParameters,index,(short)(target?3:1),(short)slot,-1,-1,4,
        target?MapPostEffect3BindingRole.RenderTargetWrite:MapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty);

    internal void AddDepthOfField(D3D11NormalPostInitialPass numeric,MapPostEffect3TextureParameterState table,
        MapPostEffect3DepthOfField2BeforeBindings before)
    {
        var pass=Import(numeric);var bindings=Declared(numeric.SourcePassIndex);
        foreach(var setter in before.OrderedDynamicBindings)
            bindings.Add(Dynamic(setter.ActualTexture.ParameterIndex,setter.NativeRegisterIndex,
                setter.Kind==MapPostEffect3DepthOfField2DynamicBindingKind.RenderTarget));
        Bind(pass,table,bindings);
        if(before.RequiresGenericComputeDispatch)
            pass.Dispatch=allocation.Passes[numeric.SourcePassIndex].DispatchExtent
                ??throw new NotSupportedException("Normal DOF compute pass has no declared dispatch.");
        else
        {
            pass.Indirect=before.RequiresNativeIndirectDraw;
            if(pass.Indirect)
            {
                pass.IndirectBuffer=before.NativeIndirectBufferParameterIndex!.Value;
                pass.IndirectOffset=before.NativeIndirectArgumentByteOffset;pass.StagingSide=before.ActualStagingSide28;
                buffers.Add(pass.IndirectBuffer);
            }
            pass.Clear=before.RequiresNativeClearMaskAndColorOverride;
            SetGraphics(pass,before.RequiresTemporaryBlendOverride||fresh.ModuleBlendEnabled,
                before.TemporaryNativeColorStateF4,before.RequiresNativeScissorOverride);
        }
    }

    internal void AddAccumulateCoc(D3D11NormalPostInitialPass numeric,MapPostEffect3TextureParameterState table,
        MapPostEffect3ParameterState state)
    {
        // The subsequent CoC template is not a fabricated current Before67.
        // Its actual program/declarations are static; the supported later branch
        // overwrites PS1 with PrefilteredCoc and targets the current CoC history.
        int Named(string name)=>state.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name,
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
            ??throw new InvalidDataException("Normal CoC template source is absent: "+name);
        var initialized=imported.Values.Single(pass=>pass.Numeric.SourcePassIndex==66);
        var target=initialized.Targets.Single(value=>value.Slot==0);
        var pass=Import(numeric);var bindings=Declared(numeric.SourcePassIndex);
        passes.Remove(pass);passes.Insert(passes.IndexOf(initialized)+1,pass);
        bindings.RemoveAll(value=>value.Role==MapPostEffect3BindingRole.RenderTargetWrite&&value.RegisterIndex==0);
        bindings.Add(Dynamic(Named("DOF2PrefilteredCoc"),1,false));
        Bind(pass,table,bindings);pass.Targets.Add(target);
        foreach(string name in new[]{"DOF2CocHistory0","DOF2CocHistory1"})_ = Texture(table,Named(name));
        SetGraphics(pass,fresh.ModuleBlendEnabled,null,true);
    }

    internal void AddBloom(D3D11NormalPostInitialPass numeric,MapPostEffect3TextureParameterState table,
        MapPostEffect3EyeBeforeBindings before)
    {
        var pass=Import(numeric);var bindings=Declared(numeric.SourcePassIndex);
        if(before.TargetTextureParameterIndex is not null)
            bindings.RemoveAll(value=>value.Role==MapPostEffect3BindingRole.RenderTargetWrite&&value.RegisterIndex==before.NativeTargetRegister);
        if(before.SourceTextureParameterIndex is not null)
            bindings.RemoveAll(value=>value.Role==MapPostEffect3BindingRole.ShaderResourceRead&&value.RegisterIndex==before.NativePixelTextureRegister&&(value.StageMask&4)!=0);
        Bind(pass,table,bindings);
        if(before.SourceTextureParameterIndex is int source)
        {
            pass.Views.RemoveAll(value=>value.Stage=="PS"&&value.Slot==before.NativePixelTextureRegister);
            var resource=Texture(table,source,before.SourceMipLevel??0);
            pass.Views.Add(new("PS",before.NativePixelTextureRegister,resource.Name,resource.Mip));
        }
        if(before.TargetTextureParameterIndex is int target)
        {
            pass.Targets.RemoveAll(value=>value.Slot==before.NativeTargetRegister);
            var resource=Texture(table,target,before.TargetMipLevel??0);
            pass.Targets.Add(DescribeTarget(before.NativeTargetRegister,resource.Name,resource.Mip));
        }
        pass.MipSampler=before.SourceMipLevel is not null&&numeric.SourcePassIndex is 86 or 88 or 90;
        SetGraphics(pass,before.RequiresTemporaryBlendOverride||fresh.ModuleBlendEnabled,
            before.TemporaryNativeColorStateF4,before.RequiresNativeScissorOverride);
    }

    internal void AddCompute(D3D11NormalPostInitialPass numeric,MapPostEffect3TextureParameterState table)
    {
        var pass=Import(numeric);Bind(pass,table,Declared(numeric.SourcePassIndex));
        pass.Dispatch=allocation.Passes[numeric.SourcePassIndex].DispatchExtent
            ??throw new NotSupportedException("Normal Eye compute pass has no declared dispatch.");
    }

    internal void AddComposite(D3D11NormalPostInitialPass numeric,MapPostEffect3TextureParameterState table,
        MapPostEffect3Composite2BeforeBindings before)
    {
        var pass=Import(numeric);var bindings=Declared(numeric.SourcePassIndex);
        if(numeric.SourcePassIndex is 108 or 109)bindings.RemoveAll(value=>value.Role==MapPostEffect3BindingRole.RenderTargetWrite);
        if(numeric.SourcePassIndex==108)bindings.RemoveAll(value=>value.Role==MapPostEffect3BindingRole.ShaderResourceRead&&value.RegisterIndex==3&&(value.StageMask&4)!=0);
        Bind(pass,table,bindings);
        if(numeric.SourcePassIndex==108)
        {
            // The current HDR Static is an external resource supplied by the
            // accepted LUT lookup, independently of native allocation slots.
            pass.Views.RemoveAll(value=>value.Stage=="PS"&&value.Slot==3);
            pass.Views.Add(new("PS",3,"hdr-lut.tex",0));
        }
        if(numeric.SourcePassIndex is 108 or 109)
        {
            // Before clears the cached RT0 pointer, not its pixels. An empty
            // cache inherits the actual caller output instead of Tone108.
            string target=numeric.SourcePassIndex==108&&!before.ClearsNativeTarget0?"Tone108":"Final109";
            pass.Targets.Clear();pass.Targets.Add(DescribeTarget(0,target,0));
        }
        SetGraphics(pass,before.ChangesBlend||fresh.ModuleBlendEnabled,before.TemporaryNativeColorStateF4,before.ChangesScissor);
    }
}
