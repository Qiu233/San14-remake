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
            if(actualRecipe.SourceSha256!=allocation.Source.Sha256||actualRecipe.FrameNumber!=table.FrameNumber||
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

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+";render-rules="+native.RenderRuleIdentity+
            ";1163080->1163460-clears-six-stage16-SRV-pointers-and-dirty0101;1162AC0->1163A10-kind1-null->876F30-SRV0;"+
            "source-CS32/36-t2-has-no-counted-or-Before-setter;isolated-device-set-retains-no-other-pass-SRV",native.Locale);
    }
}
