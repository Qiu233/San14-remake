namespace San14.Data;

public sealed record MapPostEffect3FrontendCountedSetter(MapPostEffect3Binding Binding,int NativeStageOrdinal);

/// <summary>Original Select/Reset/Finalize CPU operations. Availability must
/// come from an actual program catalog; this source supplies no device proof.</summary>
public sealed class LoadedMapPostEffect3Functor1FrontendCursorSource
{
    internal LoadedMapPostEffect3Functor1FrontendCursorSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public int FreshNativeMode1C04=>-1;
    public int FirstSourcePass=>45;
    public int LastSourcePass=>116;
    public IReadOnlyList<MapPostEffect3FrontendCountedSetter> ReadCountedSetters(
        MapPostEffect3AllocationPlan allocation,int pass)
    {
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(pass is <45 or >116)throw new InvalidDataException("Cursor setter is outside the source Functor1 directory.");
        var selected=allocation.Passes[pass];var result=new List<MapPostEffect3FrontendCountedSetter>();
        // texture loop, buffer loop, CB loop; within
        //each original row// visits six stage bits.
        foreach(var span in selected.Spans)
        foreach(var row in span.Bindings)
        for(int stage=0;stage<6;stage++)
            if((row.StageMask&selected.NativeActiveStageMask&(1<<stage))!=0)
            {
                if(row.RegisterIndex<0||row.RegisterIndex>=(row.Role==MapPostEffect3BindingRole.RenderTargetWrite?4:16))
                    throw new InvalidDataException("Counted setter exceeds its actual native cache group.");
                result.Add(new(row,stage));
            }
        return result.AsReadOnly();
    }
    public byte ReadReturningBefore(MapPostEffect3Functor1WrapperRecipe recipe,
        MapPostEffect3EyeAdaptation2Frame? eye=null,MapPostEffect3EyeBeforeBindings? eyeBefore=null)
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
            if(cb is null||cb.Locale!=Locale||cb.Writer!=MapPostEffect3ConstantBufferWriter.BloomLoop||cb.Declaration.Index!=20||cb.Bytes.Length!=32)
                throw new InvalidDataException("Eye source loop writer did not return its exact complete CB20 packet.");
        }
        return 1; ///78 scalar getter bit0 & returned writer AL.
    }
    public byte ReturnFinalize(MapPostEffect3Functor1WrapperRecipe recipe)
    {if(recipe.Locale!=Locale||recipe.FinalizeHook!=MapPostEffect3Hook.AlwaysTrue)throw new InvalidDataException("Foreign v58 callback.");return recipe.SourceFinalizeReturnAl;}
    public byte ReadBindReturnAfterReturning()=>1; // B0 01, after actual Bind and flush return.
}

public static class MapPostEffect3Functor1FrontendCursorSource
{
    public static LoadedMapPostEffect3Functor1FrontendCursorSource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadFunctor1FrontendCursorSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
}
public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3Functor1FrontendCursorSource ReadFunctor1FrontendCursorSource(LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 cursor source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";render-rules="+native.RenderRuleIdentity+
            "=CS/pass2C/tableA0;=PS/pass30/tableB0;-CS-first-else-PS"+
            "-mode=-1/292-null-borrowed-retained-dirty0/type-byte0=FF/unwritten-selector-bytes1/2;Select-counted-texture-buffer-CB/stage0;5"+
            "-groupSRV-UAV-CB-for-each-stage-thenRT/flush;-Bind-return-literal1-after-flush;-clear-descriptor;v58=-literal1",native.Locale);
    }
}
