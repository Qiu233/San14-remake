namespace San14.Data;

public sealed record OriginalMapPostEffect3FrontendCountedSetter(OriginalMapPostEffect3Binding Binding,int NativeStageOrdinal);

/// <summary>Original Select/Reset/Finalize CPU operations. Availability must
/// come from an actual program catalog; this source supplies no device proof.</summary>
public sealed class LoadedOriginalMapPostEffect3Functor1FrontendCursorSource
{
    internal LoadedOriginalMapPostEffect3Functor1FrontendCursorSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public int FreshNativeMode1C04=>-1;
    public int FirstSourcePass=>45;
    public int LastSourcePass=>116;
    public uint NativeCsGetterRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1FrontendCursorSource.cs",0x11618B0));
    public uint NativePsGetterRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1FrontendCursorSource.cs",0x11619D0));
    public IReadOnlyList<OriginalMapPostEffect3FrontendCountedSetter> ReadCountedSetters(
        OriginalMapPostEffect3AllocationPlan allocation,int pass)
    {
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(pass is <45 or >116)throw new InvalidDataException("Cursor setter is outside the source Functor1 directory.");
        var selected=allocation.Passes[pass];var result=new List<OriginalMapPostEffect3FrontendCountedSetter>();
        //1161EA0 texture loop,116211D buffer loop,11622E3 CB loop; within
        //each original row11620E7/11622B0/116237C visits six stage bits.
        foreach(var span in selected.Spans)
        foreach(var row in span.Bindings)
        for(int stage=0;stage<6;stage++)
            if((row.StageMask&selected.NativeActiveStageMask&(1<<stage))!=0)
            {
                if(row.RegisterIndex<0||row.RegisterIndex>=(row.Role==OriginalMapPostEffect3BindingRole.RenderTargetWrite?4:16))
                    throw new InvalidDataException("Counted setter exceeds its actual native cache group.");
                result.Add(new(row,stage));
            }
        return result.AsReadOnly();
    }
    public byte ReadReturningBefore(OriginalMapPostEffect3Functor1WrapperRecipe recipe,
        OriginalMapPostEffect3EyeAdaptation2Frame? eye=null,OriginalMapPostEffect3EyeBeforeBindings? eyeBefore=null)
    {
        if(recipe.Locale!=Locale)throw new InvalidDataException("Cursor and wrapper source locale differ.");
        if(recipe.NativeWrapperLoopBound==0)throw new InvalidDataException("An empty native repeat must not invoke Before.");
        if(!recipe.MaskAdmitsBefore)return 0;
        if(recipe.FactoryIndex!=8)return recipe.UnconditionalSourceBeforeReturnAl
            ??throw new InvalidDataException("Selected source Before is not its proved literal body.");
        if(eye is null||eyeBefore is null||!ReferenceEquals(recipe.SourcePreparedIdentity,eye)||
            !eye.OrderedIterations.Contains(eyeBefore.Iteration)||eyeBefore.Iteration.SourcePassIndex!=recipe.SourcePassIndex)
            throw new InvalidDataException("Returning Eye Before needs its exact actual CPU writer packet.");
        eye.RequireCurrent();
        if(eye.State.FindScalarIndex("Enable4KCBFix") is null||eye.State.RequireValue("Enable4KCBFix").Length!=4)
            throw new InvalidDataException("Eye native capability>=3 named scalar lookup did not return its source value.");
        if(eyeBefore.ActualSourceWidth40!=0||eyeBefore.ActualSourceHeight42!=0)
        {
            var cb=eyeBefore.BloomLoopConstants;
            if(cb is null||cb.Locale!=Locale||cb.WriterRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1FrontendCursorSource.cs",0x11592F0))||cb.Declaration.Index!=20||cb.Bytes.Length!=32)
                throw new InvalidDataException("Eye source loop writer did not return its exact complete CB20 packet.");
        }
        return 1; //1157D6C/78 scalar getter bit0 &115801F returned writer AL.
    }
    public byte ReturnFinalize(OriginalMapPostEffect3Functor1WrapperRecipe recipe)
    {if(recipe.Locale!=Locale||recipe.SourceFinalizeRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1FrontendCursorSource.cs",0xBF9280)))throw new InvalidDataException("Foreign v58 callback.");return recipe.SourceFinalizeReturnAl;}
    public byte ReadBindReturnAfterReturning()=>1; //1162A93 B0 01, after actual Bind and flush return.
}

public static class OriginalMapPostEffect3Functor1FrontendCursorSource
{
    public static LoadedOriginalMapPostEffect3Functor1FrontendCursorSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadFunctor1FrontendCursorSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}
public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3Functor1FrontendCursorSource ReadFunctor1FrontendCursorSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 cursor source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+";render-rules="+native.RenderRuleIdentity+
            ";11618B0=CS/pass2C/tableA0;11619D0=PS/pass30/tableB0;1161D40-CS-first-else-PS;"+
            "1145EF7-mode=-1/292-null-borrowed-retained-dirty0/type-byte0=FF/unwritten-selector-bytes1/2;Select-counted-texture-buffer-CB/stage0..5;"+
            "1163460-groupSRV-UAV-CB-for-each-stage-thenRT/flush1162AC0;1162A93-Bind-return-literal1-after-flush;1161B60-clear-descriptor;v58=BF9280-literal1",native.Locale);
    }
}
