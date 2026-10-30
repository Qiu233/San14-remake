using San14.Data.Formats;

namespace San14.Data;

/// <summary>Generic Tree2's actual direct dependency12, independently of the
/// authenticated class18 type16 creator. Matrix/material/texture imports reuse
/// the same normal Tree2 resource and current shared-wind ABI.</summary>
internal static class ReflectionTree2CaptureSource
{
    internal sealed record Packet(byte[] Vertex,ModelStageTexturePlan Textures,uint GroupFlags);

    internal static ModelShaderInterfaceSource ReadInterface(NamedModelShader owner)
    {
        if(owner.Object.ClassId!=0xAB9C4087 || owner.DependencyObjectHashes.Count<=17)
            throw new InvalidDataException("Root Tree2 requires its full direct dependency table.");
        uint hash=owner.DependencyObjectHashes[11];
        return new(owner,12,11,hash==0?null:ModelShaderResources.ReadDeclaredShader(owner.Objects,hash));
    }

    internal static ModelShaderSelectionSource ReadSelection(NamedModelShader provider,LoadedShaderContainer shader)
    {
        if(provider.Object.ClassId!=0xAB9C4087 || provider.ShaderFileHash!=0x79919452 ||
            shader.Source.FileId!=provider.ShaderFileHash || shader.Source.ResourceUri!=GameResourceId.Rdb("system",provider.ShaderFileHash).ToString())
            throw new NotSupportedException("Root Tree2 capture requires its actual generic79919452 renderer.");
        // AB9's generic dispatcher reaches the same source selection wrappers.
        // This does not authenticate AB9 as the separate class18 type16 creator.
        return new(provider,shader,shader.ReadSelectionDimensions());
    }

    internal static Packet Prepare(GameResourceRepository repository,MapStaticModelAsset asset,int drawIndex,
        NamedModelShader owner,LoadedShaderContainer capture,int vertexProgram,int pixelProgram,
        int combination,ModelMatrixBindingInput matrix,ModelGbufferMaterialSource material)
    {
        var normalProvider=ModelShaderResources.ReadDeclaredShader(owner.Objects,owner.DependencyObjectHashes[17]);
        var normal=ModelShaderResources.LoadDeclaredG1S(repository,normalProvider);
        var normalMatrix=ModelMatrixBindingSourceReader.Select(normal,matrix);
        var normalCombination=normalMatrix.ResolveCombination(new uint[6],0);
        int normalVs=normalCombination.Stages.Single(s=>s.StageCode==1).Entry!.ProgramIndex;
        var normalDefinitions=normal.ReadResourceDefinitions(normalVs);
        Tree2ModelVertexSource.RequireLayout(normalDefinitions);
        RequireVertex(capture.ReadResourceDefinitions(vertexProgram),normalDefinitions);
        var texture=ProceduralTree2TextureSource.Resolve(asset.TextureResources,drawIndex,
            normalProvider,normal,normalCombination.Index);
        RequireTextures(capture,pixelProgram,combination,normal,normalCombination.Index);
        uint group=matrix.UnboundGroupSource?.Flags ??
            throw new NotSupportedException("Root Tree2 capture requires its actual unbound matrix group.");
        if(((group>>1)&15)!=0)throw new NotSupportedException("Root Tree2 capture requires another shared wind selector.");
        byte[] normalVertex=new byte[272];
        ProceduralTree2MaterialSource.WriteStatic(normalVertex,ProceduralTree2MaterialSource.Read(material.Draw));
        byte[] vertex=new byte[368];
        // Both source programs share matrices0..159. Capture inserts exactly
        // six clip planes before the complete existing Tree2 material/wind tail.
        normalVertex.AsSpan(0,160).CopyTo(vertex);
        normalVertex.AsSpan(160,112).CopyTo(vertex.AsSpan(256));
        return new(vertex,texture,group);
    }

    private static void RequireVertex(DxbcResourceDefinitions capture,DxbcResourceDefinitions normal)
    {
        var source=normal.Buffers.Single();var target=capture.Buffers.Single();
        if(target.Name.Text!="$Globals"||target.ByteSize!=368||target.Variables.Count!=source.Variables.Count+1)
            throw new InvalidDataException("Root Tree2 capture requires its actual368-byte vertex ABI.");
        foreach(var field in source.Variables)
        {
            var actual=target.Variables.Single(v=>v.Name.Text==field.Name.Text);
            var expectedType=normal.Types.Single(t=>t.Offset==field.TypeOffset);
            var actualType=capture.Types.Single(t=>t.Offset==actual.TypeOffset);
            if(actual.StartOffset!=field.StartOffset+(field.StartOffset>=160?96:0)||actual.ByteSize!=field.ByteSize||
                actualType.Class!=expectedType.Class||actualType.BaseType!=expectedType.BaseType||actualType.Rows!=expectedType.Rows||
                actualType.Columns!=expectedType.Columns||actualType.Elements!=expectedType.Elements||actualType.Members.Count!=0)
                throw new InvalidDataException("Root Tree2 capture wind/matrix layout differs from its normal source.");
        }
        var clip=target.Variables.Single(v=>v.Name.Text=="ksgClipPlane");
        var type=capture.Types.Single(t=>t.Offset==clip.TypeOffset);
        if(clip.StartOffset!=160||clip.ByteSize!=96||type is not {Class:1,BaseType:3,Rows:1,Columns:4,Elements:6}||type.Members.Count!=0||
            capture.Bindings.Count!=normal.Bindings.Count||normal.Bindings.Any(expected=>!capture.Bindings.Any(actual=>
                actual.Name.Text==expected.Name.Text&&actual.InputType==expected.InputType&&actual.BindPoint==expected.BindPoint&&
                actual.BindCount==expected.BindCount&&actual.Dimension==expected.Dimension)))
            throw new InvalidDataException("Root Tree2 capture current clip/resource ABI differs.");
    }

    private static void RequireTextures(LoadedShaderContainer capture,int pixelProgram,int combination,
        LoadedShaderContainer normal,int normalCombination)
    {
        var definitions=capture.ReadResourceDefinitions(pixelProgram);
        var textures=definitions.Bindings.Where(b=>b.InputType==2).OrderBy(b=>b.BindPoint).ToArray();
        var samplers=definitions.Bindings.Where(b=>b.InputType==3).OrderBy(b=>b.BindPoint).ToArray();
        string[] names=["sStage0","sRefRough","sOcc","sNMap","sAmbDif"];
        uint[] registers=[0,1,2,3,5];
        if(!textures.Select(b=>b.Name.Text).SequenceEqual(names)||!textures.Select(b=>b.BindPoint).SequenceEqual(registers)||
            !samplers.Select(b=>b.BindPoint).SequenceEqual(registers)||textures.Any(b=>b.BindCount!=1)||samplers.Any(b=>b.BindCount!=1)||
            textures.Take(4).Any(b=>b.Dimension!=4)||textures[4].Dimension!=9)
            throw new NotSupportedException("Root Tree2 capture requires its selected directional material and native-null Cube profile.");
        var metadata=capture.ReadG1ResourceBindings(combination);
        var normalMetadata=normal.ReadG1ResourceBindings(normalCombination);
        if(metadata.RawHeaderWord2!=5||metadata.Entries.Count!=6)
            throw new InvalidDataException("Root Tree2 capture numeric resource registrations differ.");
        foreach(var entry in normalMetadata.Entries)
        {
            var actual=metadata.Entries.Single(v=>v.Name==entry.Name);
            if(actual.PrimaryEncoded!=entry.PrimaryEncoded||actual.SecondarySamplerEncoded!=entry.SecondarySamplerEncoded||
                actual.SlotIndex!=entry.SlotIndex||actual.RawAuxiliary!=entry.RawAuxiliary)
                throw new InvalidDataException("Root Tree2 capture changed a normal material texture registration.");
        }
        (string Name,short Register,short Encoded)[] additional=[("sGxAmbLut",4,54),("sAmbDif",5,26),("sAmbSpc",6,27)];
        foreach(var field in additional)
            if(!metadata.Entries.Any(e=>e.Name==field.Name&&e.SlotIndex==field.Register&&e.PrimaryEncoded==field.Encoded&&
                e.SecondarySamplerEncoded==0&&e.RawAuxiliary==8))
                throw new InvalidDataException("Root Tree2 capture empty-resource-array routing differs.");
        // Actual EC7100's21-entry explicit-null parameter array is no-write.
        // Thus extra P26 remains the genuine fresh native null, rather than a
        // fake environment texture; only the four real model images are exported.
    }
}
