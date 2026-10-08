using San14.Data.Formats;

namespace San14.Data;

/// <summary>Verified SC/TC per-pass backend SRV reset source. The remake's isolated
/// uniform sets represent the actual source reset plus this pass's ordered
/// setters. This receipt does not infer an omitted named texture assignment.</summary>
public sealed class LoadedOriginalMapPostEffect3PassRegisterResetSource
{
    internal LoadedOriginalMapPostEffect3PassRegisterResetSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}

    /// <summary>Only original CS32/36 t2, with no counted setter or actual Lens
    /// Before writer after the per-pass reset. There is no ParameterIndex.</summary>
    public IReadOnlyList<OriginalMapPostEffect3NullShaderResource> CaptureUnassignedLensReads(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3LensExecutionRecipe actualRecipe)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(actualRecipe);
        if(actualRecipe.SourcePassIndex is not(35 or 39))return Array.Empty<OriginalMapPostEffect3NullShaderResource>();
        int pass=actualRecipe.SourcePassIndex,programIndex=pass==35?32:36;
        void Validate()
        {
            OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
            var program=allocation.Source.GetProgram(5,programIndex);
            if(program.Sha256!="DB9FE7FFDC11813E9513991299128253C8679841F43DB00F9A12D54A6626AF4B"||
                actualRecipe.SourceSha256!=allocation.Source.Sha256||actualRecipe.FrameNumber!=table.FrameNumber||
                actualRecipe.ContextOwnerIdentity!=table.ContextOwnerIdentity||table.ContextOwnerIdentity!=allocation.ContextOwnerIdentity||
                !actualRecipe.ExecutesPass||actualRecipe.SourcePassIndex!=pass||
                !allocation.Passes[pass].Pass.ProgramReferences.Any(p=>p.StageCode==5&&p.StageIndex==programIndex)||
                allocation.Passes[pass].ActiveBindings.Any(IsTextureRegister2)||
                actualRecipe.OrderedEffectiveBindings.Any(IsTextureRegister2)||
                actualRecipe.Before.OrderedTextureSubmissions.Any(s=>s.NativeStageCode==0&&s.RegisterIndex==2)||
                !DxbcResourceDefinitionReader.Read(program.Bytes.Span).Bindings.Any(b=>
                    b.InputType==2&&b.Dimension==4&&b.BindPoint==2&&b.BindCount==1))
                throw new InvalidDataException("Original reset-null t2 requires the exact CS32/36 source and no counted/Before setter.");
        }
        bool IsTextureRegister2(OriginalMapPostEffect3Binding row)=>row.RegisterIndex==2&&(row.StageMask&1)!=0&&
            row.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead;
        Validate();
        return Array.AsReadOnly(new[]{new OriginalMapPostEffect3NullShaderResource(table,pass,5,2,Validate)});
    }
}

public static class OriginalMapPostEffect3PassRegisterResetSource
{
    public static LoadedOriginalMapPostEffect3PassRegisterResetSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostEffect3PassRegisterResetSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3PassRegisterResetSource ReadPostEffect3PassRegisterResetSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);
        VerifyHash(exe,native.ExecutableSha256,"Per-pass register reset executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int start);
        VerifyHash(code,native.DecodedTextSha256,"Per-pass register reset text");
        foreach(var w in new (int Rva,int Count,string Sha)[]{
            (0x1163080,28,"98DED7630F7B1079FB54BAD0024AC6EF05F24ADC41481D36BEF7135CED74AC80"),
            (0x1163460,426,"26428584698B46F67D86E7816F3EC7897A27BA3284E80895B037F7538B8A17A7"),
            (0x1162AC0,412,"46E9460069251E5C46491B69D5FBB5237562CD5B3273C516CFDFCF7DD6D76FBC"),
            (0x1163A10,756,"D8121F0B0BA21EE3479791CB943262DC206649A8FF7952FF5DB2E323EAE87EC8")})
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3PassRegisterResetSource.cs",w.Rva,w.Count,w.Sha);
            VerifyHash(code.AsSpan(actual.Rva-start,w.Count),actual.Sha,"Per-pass register reset operation");
        }
        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+"@"+native.ExecutableSha256+
            ";1163080->1163460-clears-six-stage16-SRV-pointers-and-dirty0101;1162AC0->1163A10-kind1-null->876F30-SRV0;"+
            "source-CS32/36-t2-has-no-counted-or-Before-setter;isolated-device-set-retains-no-other-pass-SRV",native.Locale);
    }
}
