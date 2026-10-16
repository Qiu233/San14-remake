namespace San14.Data;

public sealed partial class ContactOcclusionResources
{
    public const string NativeDirectory="sub-shadow-input";

    public void WriteNativeInputs(string inputRoot)
    {
        string directory=Path.Combine(inputRoot,NativeDirectory);Directory.CreateDirectory(directory);
        using(var writer=Create(directory,"programs.bin",0x3150434F)) // OCP1
        {
            writer.Write((uint)programs.Length);
            foreach(var program in programs)
            {
                writer.Write((uint)program.Index);writer.Write(program.StageCode);Bytes(writer,program.Bytes.Span);
                var definitions=program.Definitions;
                var buffers=definitions.Bindings.Where(value=>value.InputType==0).ToArray();
                writer.Write((uint)buffers.Length);
                foreach(var binding in buffers)
                {
                    var buffer=definitions.Buffers.Single(value=>value.Name.Text==binding.Name.Text);
                    writer.Write(binding.BindPoint);writer.Write((uint)buffer.ByteSize);writer.Write((uint)buffer.Variables.Count);
                    foreach(var variable in buffer.Variables)
                    {
                        writer.Write(Semantic(variable.Name.Text));writer.Write((uint)variable.StartOffset);writer.Write((uint)variable.ByteSize);
                    }
                }
            }
        }
        using(var writer=Create(directory,"variants.bin",0x3156434F)) // OCV1
        {
            var bindings=PrimaryBindings;
            if(bindings.Variants.RecordWidth!=16*sizeof(uint))
                throw new NotSupportedException("Contact occlusion variant keys require another format adapter.");
            if(bindings.RawDimensionCounts.Take(6).Any(value=>value>1))
                throw new NotSupportedException("Contact occlusion needs a producer for additional shader selection dimensions.");
            writer.Write((uint)bindings.Variants.Records.Count);writer.Write(bindings.PassCount);
            foreach(var variant in bindings.Variants.Records)
            {
                writer.Write(variant.RawBytes.Span);
                for(uint pass=0;pass<bindings.PassCount;pass++)
                {
                    var combination=bindings.ResolveCombination([0,0,0,0,0,0,(uint)variant.Index],pass);
                    uint Program(uint stage)=>combination.Stages.Single(value=>value.StageCode==stage).Entry is {} entry
                        ?(uint)entry.ProgramIndex:uint.MaxValue;
                    writer.Write(Program(1));writer.Write(Program(0));
                    var textures=PrimaryShader.ReadG1ResourceBindings(combination.Index).Entries
                        .Where(value=>value.PrimaryEncoded>=128&&value.SlotIndex>=0).ToArray();
                    writer.Write((uint)textures.Length);
                    foreach(var texture in textures)
                    {writer.Write((uint)texture.SlotIndex);writer.Write((uint)texture.PrimaryEncoded);}
                }
            }
        }
        using(var writer=Create(directory,"samplers.bin",0x3153434F)) // OCS1
        {writer.Write((uint)samplers.Length);foreach(var sampler in samplers)writer.Write(sampler);}
        using(var writer=Create(directory,"targets.bin",0x3154434F)) // OCT1
        {
            writer.Write((uint)targetCandidates.Length);
            foreach(var candidates in targetCandidates)
            {
                writer.Write((uint)candidates.Length);
                foreach(var value in candidates)
                {writer.Write(value.ResourceFormat);writer.Write(value.ShaderViewFormat);writer.Write(value.TargetViewFormat);}
            }
        }
        using(var writer=Create(directory,"noise.bin",0x314E434F)) // OCN1
        {
            var formats=RenderTextureFormatMath.Resolve(1,0,0);
            writer.Write(formats.ResourceFormat);writer.Write(formats.ShaderViewFormat);writer.Write(4u);writer.Write(4u);
            Bytes(writer,noise);
        }
        using(var writer=Create(directory,"stock.bin",0x314B434F)) // OCK1
        {
            Bytes(writer,ProjectStockShaders.Vertex.Dxbc);Bytes(writer,ProjectStockShaders.Default3.Dxbc);
            Bytes(writer,ProjectStockShaders.Common138.Dxbc);Bytes(writer,ProjectStockShaders.Common140.Dxbc);
            writer.Write(5u); // Composition sampler's index in the static sampler bank.
            var blend=compositionBlend;
            writer.Write(blend.BlendEnabled?1u:0u);writer.Write(blend.SourceColor);writer.Write(blend.DestinationColor);
            writer.Write(blend.ColorOperation);writer.Write(blend.SourceAlpha);writer.Write(blend.DestinationAlpha);
            writer.Write(blend.AlphaOperation);writer.Write((uint)blend.RgbaWriteMask);
        }
    }

    // These are shader-format semantic identifiers, shared with the Native
    // packer. A newly selected unknown field fails there instead of becoming zero.
    private static uint Semantic(string name)=>name switch
    {
        "vResolutionParam"=>0,"vDepthParam"=>1,"vViewParam"=>2,"vRadiusParam"=>3,
        "vRayParam"=>4,"vOccParam"=>5,"vBlurParam"=>6,"nSwitchRef"=>7,"vRadiusParam2"=>8,
        _=>uint.MaxValue
    };
    private static BinaryWriter Create(string directory,string name,uint magic)
    {var writer=new BinaryWriter(File.Create(Path.Combine(directory,name)));writer.Write(magic);return writer;}
    private static void Bytes(BinaryWriter writer,ReadOnlySpan<byte> bytes)
    {writer.Write((uint)bytes.Length);writer.Write(bytes);}
}
