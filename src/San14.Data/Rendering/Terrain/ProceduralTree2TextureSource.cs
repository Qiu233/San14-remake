using San14.Data.Formats;

namespace San14.Data;

/// <summary>Normal CE Tree2's numeric PB2 texture registrations. Its AB9 creator
/// F97A60/F93950 uses the same 97F630/97ED70 ordered type19 writes as PB2.
/// Wind t8/t9 are separate VS resources and never model texture-table entries.</summary>
public static class ProceduralTree2TextureSource
{
    public static ModelStageTexturePlan Resolve(LoadedModelTextureResources model,int drawIndex,
        NamedModelShader provider,LoadedShaderContainer shader,int combinationIndex)
    {
        if(provider.Object.ClassId!=0xAB9C4087 || provider.ShaderFileHash!=shader.Source.FileId ||
            shader.Source.FileId is not (0x20A8720A or 0x7FA757FD or 0xDBB601B0) || combinationIndex!=0)
            throw new NotSupportedException("Only the actual CE normal Tree2 instanced and noninstanced profiles are imported here.");
        var metadata=shader.ReadG1ResourceBindings(combinationIndex);
        var descriptor=G2ShaderTextureBaseReader.Read(shader.Source.Bytes);
        int pixel=metadata.Combination.Stages.Single(s=>s.StageCode==0).Entry!.ProgramIndex;
        var rdef=shader.ReadResourceDefinitions(pixel);
        var textures=rdef.Bindings.Where(b=>b.InputType==2).OrderBy(b=>b.BindPoint).ToArray();
        var samplers=rdef.Bindings.Where(b=>b.InputType==3).OrderBy(b=>b.BindPoint).ToArray();
        if(descriptor.ModeValue!=0 || descriptor.DescriptorFlags!=0x28 || descriptor.DescriptorTextureCount!=1 ||
            metadata.RawHeaderWord2!=2 || metadata.Entries.Count!=3 || textures.Length!=4 || samplers.Length!=4 ||
            !textures.Select(t=>t.Name.Text).SequenceEqual(new[]{"sStage0","sRefRough","sOcc","sNMap"}) ||
            !textures.Select(t=>t.BindPoint).SequenceEqual(new uint[]{0,1,2,3}) ||
            !samplers.Select(t=>t.BindPoint).SequenceEqual(new uint[]{0,1,2,3}) ||
            textures.Any(t=>t.BindCount!=1||t.Dimension!=4) || samplers.Any(t=>t.BindCount!=1))
            throw new InvalidDataException("Tree2 normal PS/descriptor profile differs from its source import.");
        foreach(var entry in metadata.Entries)
            if(entry.SlotIndex is <1 or >3 || entry.RawAuxiliary!=0 || entry.Name!=textures[entry.SlotIndex].Name.Text)
                throw new InvalidDataException("Tree2 numeric registration has no exact selected PS role.");
        var record=ModelTextureResources.ResolveDrawTextures(model,drawIndex).Binding.TextureRecord;
        if(record.UnknownValue8!=1 || unchecked((short)record.UnknownValue12)!=1 || record.TextureEntries.Count!=4)
            throw new InvalidDataException("Tree2 type19 source texture count/special index differs.");
        var primary=new Dictionary<int,G1ShaderResourceBinding>();var secondary=new Dictionary<int,G1ShaderResourceBinding>();
        foreach(var entry in metadata.Entries)
        {
            if(entry.PrimaryEncoded!=0)primary.Add(Encoded(entry.PrimaryEncoded),entry);
            if(entry.SecondarySamplerEncoded!=0 && entry.SecondarySamplerEncoded!=entry.PrimaryEncoded)
                secondary.Add(Encoded(entry.SecondarySamplerEncoded),entry);
        }
        var effects=new List<ModelStageTextureEffect>();var final=new Dictionary<int,G1TextureEntry>();
        void Write(G1TextureEntry entry,int slot,ModelStageTextureOperation operation,ushort encoded=0,G1ShaderResourceBinding? registration=null)
        {
            if(entry.RawValue0>=model.Slots.Count || slot is <0 or >16)throw new InvalidDataException("Tree2 source texture slot out of range.");
            effects.Add(new(effects.Count,operation,record.Index,entry.Index,entry.RawValue0,slot,encoded,registration?.Index,registration?.Name));
            if(slot<16)final[slot]=entry;
        }
        Write(record.TextureEntries[0],0,ModelStageTextureOperation.Primary);
        Write(record.TextureEntries[1],1,ModelStageTextureOperation.Special);
        foreach(var entry in record.TextureEntries.Skip(1))
        {
            primary.TryGetValue(Encoded(entry.RawValue4),out var p);secondary.TryGetValue(Encoded(entry.RawValue6),out var s);
            if(p is null || (s is null&&entry.RawValue6!=entry.RawValue4))throw new InvalidDataException("Tree2 encoded registration is absent.");
            Write(entry,p.SlotIndex,ModelStageTextureOperation.EncodedPrimary,entry.RawValue4,p);
            Write(entry,s?.SlotIndex??16,ModelStageTextureOperation.EncodedSecondary,entry.RawValue6,s);
        }
        if(!final.Keys.Order().SequenceEqual(new[]{0,1,2,3}))throw new InvalidDataException("Tree2 final model texture registers incomplete.");
        var bindings=new List<ModelStageTextureBinding>();
        for(int target=0;target<4;target++)
        {
            var e=final[target];var s=model.Slots[e.RawValue0];
            bindings.Add(new((uint)target,samplers[target].BindPoint,textures[target].Name.Text,samplers[target].Name.Text,
                record.Index,e.Index,s.Index,s.ProxyObjectId,s.TextureFileId,s.RawValue00200571,
                checked((int)(s.RawValue00200571&0x7FFFFFFF)),e.RawValue2,e.RawValue4,e.RawValue6,e.RawValue8,e.RawValue10));
        }
        return new(model,shader.Source.ResourceUri,shader.Source.DecodedSha256,provider.Object.ObjectId,drawIndex,combinationIndex,pixel,record.Index,1,1,effects,bindings);
    }
    private static int Encoded(int value)
    {
        int index=(value&7)+8*(((value>>3)&15)+10*(value>>7));
        if(value<0 || index>=88)throw new InvalidDataException("Tree2 texture encoded namespace exceeds source table.");
        return index;
    }
}
