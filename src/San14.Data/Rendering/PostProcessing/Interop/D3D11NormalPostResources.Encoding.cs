using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class D3D11NormalPostResources
{
    /// <summary>Write the one-time Native media bank. This emits no exposure,
    /// history, staging or indirect pixels and does not mark a pass complete.</summary>
    public void WriteNativeInputs(string inputRoot)
    {
        string initial=Path.Combine(inputRoot,"normal-post-next-input","prior-cpu");
        string continuous=Path.Combine(inputRoot,"normal-post-continuous-input");
        string final=Path.Combine(inputRoot,"normal-post-input","109-fxaa");
        Directory.CreateDirectory(initial);Directory.CreateDirectory(continuous);Directory.CreateDirectory(final);
        var histogram=MapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(value=>value.Parameter.Index==8));
        Text(initial,"exposure.txt",$"{exposure.Formats.ResourceFormat} {exposure.Formats.ShaderResourceFormat0} {exposure.Formats.UnorderedAccessFormat} {exposure.Width} {exposure.Height}");
        Text(initial,"histogram.txt",$"{histogram.ByteCount} {histogram.ViewFormat} {histogram.ViewElementCount} {histogram.BindFlags} {histogram.DeviceUsage} {histogram.CpuAccessFlags} {histogram.MiscFlags} {histogram.StructureByteStride}");
        string bloomRow=$"{bloom.ParameterIndex} {bloom.Width} {bloom.Height} {bloom.MipLevels} {bloom.Formats.ResourceFormat} {bloom.Formats.ShaderResourceFormat0} {bloom.Formats.RenderTargetFormat0}";
        Text(initial,"bloom.txt",bloomRow);Text(continuous,"bloom.txt",bloomRow);
        var shapeFormats=MapPostEffect3AllocationShapeReader.ReadFormats(shape.RequiredNativeFormat);
        Text(initial,"dof-shape.txt",$"{shape.ActualShapeTextureParameterIndex} {shape.RequiredWidth} {shape.RequiredHeight} {shape.RequiredMipCount} {shapeFormats.ResourceFormat} {shapeFormats.ShaderResourceFormat0} {shapeFormats.RenderTargetFormat0} {shapeFormats.UnorderedAccessFormat} 1 0 1 0");
        Lines(initial,"dof-textures.txt",textures.Order().Where(index=>index!=61&&index!=bloom.ParameterIndex&&index!=tone.ParameterIndex).Select(index=>
        {
            var value=allocation.TextureAllocations.Single(value=>value.Parameter.Index==index);var texture=TextureShape(index);
            if(texture.Dimension!=MapPostEffect3TextureDimension.Texture2D)
                throw new NotSupportedException("Normal DOF media requires another texture-dimension producer.");
            return $"{index} {texture.Width} {texture.Height} {texture.MipLevels} {texture.Formats.ResourceFormat} {texture.Formats.ShaderResourceFormat0} {texture.Formats.RenderTargetFormat0} {texture.Formats.UnorderedAccessFormat} {(texture.HasRenderTargetPath?1:0)} {(texture.AllowsUnorderedAccess?1:0)} {value.InstanceCount} {textureSides[index]}";
        }));
        Lines(initial,"dof-buffers.txt",buffers.Order().Where(index=>index!=8).Select(index=>
        {
            var buffer=MapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(value=>value.Parameter.Index==index));
            return $"{index} {buffer.ByteCount} {buffer.DeviceUsage} {buffer.CpuAccessFlags} {buffer.BindFlags} {buffer.MiscFlags} {buffer.StructureByteStride} {buffer.ViewFormat} {buffer.ViewElementCount} {buffer.ShaderResourceDimension} {buffer.ShaderResourceFlags} {buffer.UnorderedAccessFlags}";
        }));
        // Two controller-owned full32-byte staging copies. Their selected side
        // is independent of module texture/buffer selectors and CoC history.
        Lines(initial,"dof-staging.txt",Enumerable.Range(0,2).Select(side=>$"{side} 32 3 196608 0 0 0"));
        Lines(initial,"history-pairs.txt",MapPostEffect3PassStateReader.ReadHistoryFlip(allocation,true).Select(pair=>
        {
            uint name=pair.Table switch
            {
                MapPostEffect3BindingTable.TextureParameters=>allocation.TextureParameters[pair.ParameterIndex].NameHash,
                MapPostEffect3BindingTable.BufferParameters=>allocation.BufferParameters[pair.ParameterIndex].NameHash,
                _=>allocation.ConstantBufferParameters[pair.ParameterIndex].NameHash
            };
            int selected=(pair.Table switch
            {
                MapPostEffect3BindingTable.TextureParameters=>sides.TextureInstances,
                MapPostEffect3BindingTable.BufferParameters=>sides.BufferInstances,
                _=>sides.ConstantBufferInstances
            })[pair.ParameterIndex];
            return $"{pair.Table} {pair.ParameterIndex} {selected} {selected^1} {name:X8}";
        }));
        WriteTarget(initial,"108-tone",new(tone.Formats.ResourceFormat,tone.Formats.ShaderResourceFormat0,tone.Formats.RenderTargetFormat0));
        Text(final,"target.txt",$"{outputFormats.ResourceFormat} {outputFormats.ShaderViewFormat} {outputFormats.TargetViewFormat} {allocation.ContextWidth} {allocation.ContextHeight}");
        WriteHdr(Path.Combine(initial,"108-tone"));
        foreach(var pass in passes)WritePassMedia(continuous,pass);
    }

    private void WritePassMedia(string root,PassMedia pass)
    {
        string directory=Path.Combine(root,pass.Numeric.Name);Directory.CreateDirectory(directory);
        foreach(var program in pass.Programs)File.WriteAllBytes(Path.Combine(directory,program.Stage+".dxbc"),program.Bytes.ToArray());
        Lines(directory,"views.txt",pass.Views.Select(value=>$"{value.Stage} {value.Slot} {value.Resource} {value.Mip} 1"));
        if(pass.Dispatch is {} dispatch)
        {
            Lines(directory,"uavs.txt",pass.Unordered.Select(value=>$"{value.Slot} {value.Resource}"));
            Text(directory,"dispatch.txt",$"{dispatch.X} {dispatch.Y} {dispatch.Z}");return;
        }
        var target=pass.Targets.Single(value=>value.Slot==0);
        if(pass.Numeric.Name.EndsWith("-dof",StringComparison.Ordinal))
            Lines(directory,"targets.txt",pass.Targets.Select(value=>$"{value.Slot} {value.Resource} {value.Mip} {value.Formats.ResourceFormat} {value.Formats.ShaderViewFormat} {value.Formats.TargetViewFormat} {value.Width} {value.Height}"));
        else Text(directory,"target-view.txt",$"{target.Resource} {target.Mip}");
        Text(directory,"target.txt",$"{target.Formats.ResourceFormat} {target.Formats.ShaderViewFormat} {target.Formats.TargetViewFormat} {target.Width} {target.Height}");
        Text(directory,"viewport.txt",$"0 0 {target.Width} {target.Height} 0 1");
        Text(directory,"scissor.txt",$"{(pass.Scissor?1:0)} 0 0 {target.Width} {target.Height}");
        Text(directory,"raster.txt",$"{fresh.StockNativeCullMode} {(fresh.StockDepthTest?1:0)} {(fresh.ModuleDepthWrite?1:0)} {(pass.Blend[0].BlendEnabled?1:0)} {fresh.ModuleColorWriteMask}");
        Lines(directory,"blend.txt",pass.Blend.Select(value=>$"{(value.BlendEnabled?1:0)} {value.SourceColor} {value.DestinationColor} {value.ColorOperation} {value.SourceAlpha} {value.DestinationAlpha} {value.AlphaOperation} {value.RgbaWriteMask}"));
        if(pass.Clear)Text(directory,"clear.txt","15 0");
        if(pass.Indirect)
        {
            Text(directory,"indirect.txt",$"{pass.IndirectBuffer} {pass.IndirectOffset} {pass.StagingSide}");
            Text(directory,"indirect-method.txt",$"DrawInstancedIndirect {fresh.GhostD3DPrimitiveTopology}");
        }
        else Text(directory,"draw.txt","3 1 0 0");
        var samplers=new List<(int Slot,G1TextureSamplerRecipe Recipe)>{(7,linear),(8,point)};
        if(pass.MipSampler)samplers.Add((0,bloomSampler));
        Lines(directory,"samplers.txt",samplers.Select(value=>value.Slot.ToString(CultureInfo.InvariantCulture)));
        foreach(var sampler in samplers)File.WriteAllBytes(Path.Combine(directory,$"sampler{sampler.Slot}.bin"),sampler.Recipe.EncodeD3D11Descriptor());
    }

    private void WriteTarget(string root,string name,RenderTextureFormats formats)
    {
        string directory=Path.Combine(root,name);Directory.CreateDirectory(directory);
        Text(directory,"target.txt",$"{formats.ResourceFormat} {formats.ShaderViewFormat} {formats.TargetViewFormat} {allocation.ContextWidth} {allocation.ContextHeight}");
    }

    private void WriteHdr(string directory)
    {
        Text(directory,"hdr-resource.txt",$"{hdr.NativeResourceFormat} {hdr.NativeShaderViewFormat}");
        var image=hdr.Binding.Image;
        using var writer=new BinaryWriter(File.Create(Path.Combine(directory,"hdr-lut.tex")));
        writer.Write((uint)image.Width);writer.Write((uint)image.Height);writer.Write((uint)image.Depth);
        writer.Write((uint)image.MipCount);writer.Write(hdr.NativeShaderViewFormat);
        foreach(var mip in image.Mips)
        {
            var bytes=image.GetEncodedVolumeMip(hdr.Binding.Resource.Source.Bytes,mip.Level);
            int slice=bytes.Length/mip.Depth,row=slice/mip.Height;
            if(slice*mip.Depth!=bytes.Length||row*mip.Height!=slice)
                throw new InvalidDataException("Normal HDR volume subresource pitches are not exact.");
            writer.Write((uint)row);writer.Write((uint)slice);writer.Write((uint)bytes.Length);writer.Write(bytes);
        }
    }

    private static void Text(string directory,string name,FormattableString value)=>
        File.WriteAllText(Path.Combine(directory,name),value.ToString(CultureInfo.InvariantCulture)+"\n");
    private static void Text(string directory,string name,string value)=>File.WriteAllText(Path.Combine(directory,name),value+"\n");
    private static void Lines(string directory,string name,IEnumerable<string> values)=>File.WriteAllLines(Path.Combine(directory,name),values);
}
