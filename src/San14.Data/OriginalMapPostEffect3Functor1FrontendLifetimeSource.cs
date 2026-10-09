using System.Buffers.Binary;

namespace San14.Data;

/// <summary>These are CPU frontend binding-cache groups, distinct from the
/// allocation's Texture/Buffer/CB parameter registries and GPU buffer bytes.</summary>
public enum OriginalMapPostEffect3FrontendBindingGroupKind
{ ShaderResources,UnorderedAccess,ConstantBuffers,RenderTargets }

public sealed record OriginalMapPostEffect3FrontendEndBindingGroup(
    OriginalMapPostEffect3FrontendBindingGroupKind Kind,int BaseOffset,int GroupCount,int SlotsPerGroup)
{
    public int RecordByteCount=>24;
    public int BorrowedPointerOffset=>8;
    public int RetainedReferenceOffset=>16;
    public int DirtyWordOffset=>3;
    public ushort DirtyWordWrittenWhenBorrowedPointerWasNonNull=>0x0101;
    // consumes the three six-group tables. It does not visit RT slots.
    public bool FlushConsumesDirtyWord=>Kind!=OriginalMapPostEffect3FrontendBindingGroupKind.RenderTargets;
    public int NativeRecordOffset(int group,int slot)
    {
        if((uint)group>=GroupCount||(uint)slot>=SlotsPerGroup)throw new ArgumentOutOfRangeException();
        return checked(BaseOffset+(group*SlotsPerGroup+slot)*RecordByteCount);
    }
}

public enum OriginalMapPostEffect3TemporaryRegistryCleanupOperation
{ ClearBorrowedPointer,ReleaseRetainedNativeReference,ClearRetainedPointer,ClearDirtyByte }

/// <summary>Numeric native pointer identity only. This is never a Godot RID,
/// GPU byte offset, initialized input lease or an executed-release receipt.</summary>
public sealed record OriginalMapPostEffect3TemporaryRegistryCleanupStep(
    OriginalMapPostEffect3TemporaryRegistryCleanupOperation Operation,int RecordOffset,ulong NativePointerIdentity);

public sealed class OriginalMapPostEffect3TemporaryRegistryCleanupPlan
{
    internal OriginalMapPostEffect3TemporaryRegistryCleanupPlan(
        IReadOnlyList<OriginalMapPostEffect3TemporaryRegistryCleanupStep> steps,string evidence)
        =>(OrderedSteps,SourceEvidence)=(steps,evidence);
    public int ParameterIndex=>4;
    public int NativeRecordByteCount=>40;
    public IReadOnlyList<OriginalMapPostEffect3TemporaryRegistryCleanupStep> OrderedSteps{get;}
    public bool ProvesActualTemporaryBufferCleanup=>false;
    public bool WritesGpuBufferBytes=>false;
    public bool ReleasesControllerOwnedBuffer828=>false;
    public bool ClearsControllerA1AfterRegistryCleanup=>true;
    public bool SecondRetainedReferenceIsReadAfterFirstReleaseCallback=>true;
    public string SourceEvidence{get;}
}

/// <summary>Exact source operation shape for the F1 device owner to implement.
/// The loader and numeric plans execute no frontend, GPU or lifetime release.</summary>
public sealed class LoadedOriginalMapPostEffect3Functor1FrontendLifetimeSource
{
    internal LoadedOriginalMapPostEffect3Functor1FrontendLifetimeSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public uint NativeEndArgument=>uint.MaxValue;
    public bool EndRunsOptionalFinalTargetDraw=>false;
    public bool EndRestoresBeginAttachmentBytes=>true;
    public bool EndRestoresOuterScope=>false;
    public bool EndFlipsHistorySelectors=>false;
    public bool ProvesActualFrontendEnd=>false;
    public int PendingBackendFlagOffset=>0x2A98;
    public uint ConditionalFirstNativeCommandOpcode=>0x36;
    public int ControllerOwnedGhostBufferPointerOffset=>0x828;
    public int ControllerGhostBufferByteCapacity=>448;
    public int ControllerGhostBufferStructureStride=>28;
    public uint TemporaryBufferNameHash=>0x6E39B43E;
    public IReadOnlyList<OriginalMapPostEffect3FrontendEndBindingGroup> BindingGroups{get;}=Array.AsReadOnly(new[]{
        new OriginalMapPostEffect3FrontendEndBindingGroup(OriginalMapPostEffect3FrontendBindingGroupKind.ShaderResources,0x50,6,16),
        new OriginalMapPostEffect3FrontendEndBindingGroup(OriginalMapPostEffect3FrontendBindingGroupKind.UnorderedAccess,0x950,6,16),
        new OriginalMapPostEffect3FrontendEndBindingGroup(OriginalMapPostEffect3FrontendBindingGroupKind.ConstantBuffers,0x1250,6,16),
        new OriginalMapPostEffect3FrontendEndBindingGroup(OriginalMapPostEffect3FrontendBindingGroupKind.RenderTargets,0x1B50,1,4)});
    public IReadOnlyList<int> BeginSavedAttachmentBackendOffsets{get;}=Array.AsReadOnly(new[]{0x1C8,0x1D0,0x1C0});
    public IReadOnlyList<int> BeginSavedAttachmentFrontendOffsets{get;}=Array.AsReadOnly(new[]{0x1C08,0x1C0C,0x1C10});
    public int BeginSavedAlphaBackendOffset=>0x1BE;
    public int BeginSavedAlphaFrontendOffset=>0x1C14;
    //The two scope bytes really are both read from backend1BF at.
    //Do not replace this native copy with an idealized snapshot of1D8.
    public int OuterSaved14SourceBackendOffset=>0x1BF;
    public int OuterRestored14DestinationBackendOffset=>0x1D8;
    public int OuterSaved15SourceBackendOffset=>0x1BF;
    public int OuterRestored15DestinationBackendOffset=>0x1BF;

    public void ValidateFor(OriginalMapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        var module=allocation.Source;
        if(module.Functors[1].FirstPass!=45||module.Functors[1].PassCount!=72||
            module.Functors[1].RawValue16!=uint.MaxValue||allocation.BufferParameters.Count!=9)
            throw new InvalidDataException("Frontend End source needs the exact full Functor1 and native buffer registry.");
        var parameter=allocation.BufferParameters[4];
        if(parameter.Index!=4||parameter.NameHash!=TemporaryBufferNameHash||
            parameter.Ownership!=OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot||parameter.HasTwoRuntimeInstances||
            allocation.BufferAllocations.Any(a=>a.Parameter.Index==4))
            throw new InvalidDataException("InputLensGhostInfo is the original external CPU registry slot, not a bank-owned GPU allocation.");
    }

    /// <summary>Builds the exact post-return CPU row operation plan from an
    /// explicit numeric snapshot. The caller must retain the actual registry
    /// and object owners, execute these operations, and prove their lifetime.</summary>
    public OriginalMapPostEffect3TemporaryRegistryCleanupPlan DescribeTemporaryBufferCleanup(
        OriginalMapPostEffect3AllocationPlan allocation,ReadOnlySpan<byte> nativeRecord40)
    {
        ValidateFor(allocation);
        if(nativeRecord40.Length!=40)throw new InvalidDataException("Temporary cleanup needs the actual complete40B native registry row.");
        var steps=new List<OriginalMapPostEffect3TemporaryRegistryCleanupStep>();
        void Clear(OriginalMapPostEffect3TemporaryRegistryCleanupOperation operation,int offset)
            =>steps.Add(new(operation,offset,0));
        // captures retained+10 before clearing borrowed+8. This is a
        //snapshot description only: native reads+20 only after the
        //first possible callback. An actual executor must make that later
        //read/current-owner check, rather than treating this numeric pointer
        //snapshot as authority to release an old object after a mutation.
        ulong first=BinaryPrimitives.ReadUInt64LittleEndian(nativeRecord40.Slice(16,8)),
            second=BinaryPrimitives.ReadUInt64LittleEndian(nativeRecord40.Slice(32,8));
        Clear(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ClearBorrowedPointer,8);
        if(first!=0)steps.Add(new(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ReleaseRetainedNativeReference,16,first));
        Clear(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ClearRetainedPointer,16);
        Clear(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ClearBorrowedPointer,24);
        if(second!=0)
        {
            steps.Add(new(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ReleaseRetainedNativeReference,32,second));
            Clear(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ClearRetainedPointer,32);
        }
        Clear(OriginalMapPostEffect3TemporaryRegistryCleanupOperation.ClearDirtyByte,1);
        return new(Array.AsReadOnly(steps.ToArray()),SourceEvidence+";explicit-numeric-registry-row-plan;not-an-executed-cleanup");
    }
}

public static class OriginalMapPostEffect3Functor1FrontendLifetimeSource
{
    public static LoadedOriginalMapPostEffect3Functor1FrontendLifetimeSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadFunctor1FrontendLifetimeSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    public static LoadedOriginalMapPostEffect3Functor1FrontendLifetimeSource ReadFunctor1FrontendLifetimeSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 frontend lifetime source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";render-rules="+native.RenderRuleIdentity+
            ";EndFFFFFFFF/-SRV-UAV-CB-dirty-flush;-pairs-before11462D0-outer"+
            "-CPUregistry40B-loan;-CPUrefs-release/-A1"+
            "-controller828-owned-448B-stride28;source-recipe-only-no-device-or-release-execution",native.Locale);
    }

}
