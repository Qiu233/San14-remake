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
    //1162AC0 consumes the three six-group tables. It does not visit RT slots.
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
    //The two scope bytes really are both read from backend1BF at1146737.
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
            !parameter.RawBytes.Span.SequenceEqual(Convert.FromHexString(
                "FFFFFFFF3EB4396E01000000FFFF00000000000000000000FFFFFFFF8800000000000000"))||
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
        //E22C49 captures retained+10 before clearing borrowed+8. This is a
        //snapshot description only: native E22C64 reads+20 only after the
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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Functor1 frontend lifetime executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Functor1 frontend lifetime decoded text");
        foreach(var window in Functor1FrontendLifetimeWindows)
            { var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3Functor1FrontendLifetimeSource.cs",window.Rva,window.Size,window.Sha);
              VerifyHash(text.AsSpan(actual.Rva-start,window.Size),actual.Sha,"Functor1 frontend lifetime source window"); }
        if(!image.Raw(OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,"OriginalMapPostEffect3Functor1FrontendLifetimeSource.cs",0x16DC710),19).SequenceEqual("InputLensGhostInfo\0"u8))
            throw new InvalidDataException("Functor1 temporary buffer name source changed.");
        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+"@"+native.ExecutableSha256+
            ";TC:1162D70-EndFFFFFFFF/1162AC0-SRV-UAV-CB-dirty-flush;114AB40-pairs-before11462D0-outer;"+
            "E24F30-CPUregistry40B-loan/E22C49..E22C89-CPUrefs-release/E22C8E-A1;"+
            "E24B5E/94FAB0-controller828-owned-448B-stride28;source-recipe-only-no-device-or-release-execution",native.Locale);
    }
    private static readonly (int Rva,int Size,string Sha)[] Functor1FrontendLifetimeWindows=
    [
        (0x11466F0,401,"85310F484A3DFDC7EADC6D730715807AFEC4D515F4CECC96561343B647348784"),
        (0x1161C00,320,"7B5ED06ABB60CB3CC3119BCBADD4313FF8BF8C40E7767A10EC10B4D9F4C911D4"),
        (0x1162D70,778,"89CFF5F56C21626863F66154DEAA52AB7C78260203B62CEB9FED7F09263E03D5"),
        (0x1162AC0,412,"46E9460069251E5C46491B69D5FBB5237562CD5B3273C516CFDFCF7DD6D76FBC"),
        (0x1163A10,756,"D8121F0B0BA21EE3479791CB943262DC206649A8FF7952FF5DB2E323EAE87EC8"),
        (0x1163E80,340,"4F9A49C39E0135654FAF5DEAA449D4FEC6141949BF188624540629BBDD2976F6"),
        (0x876F30,258,"11B515D24EE746A19EF26B044F6455DE9419A34C318AA63B36DB2E3AEEE01FF3"),
        (0x95B930,180,"0DF8B73BA76A2C04351E6E0D90BCE42342CBE4E25441DEF44737531E55631176"),
        (0x114AB40,170,"A14D90049C410CB301A3457F4169C8C7CBF2EAA1E98DF120366EF7FF717213E6"),
        (0x11462D0,852,"F54BDF419F593973C7EFDF9A21E1BF0BA6FE39D5228D102A827D7DB3953A0CA3"),
        (0xE22860,1107,"725BF0BA2FDECED538B0BCCBFCE380097FCD760EBB0F330DD17CE42FD36C0C11"),
        (0xE24F30,100,"DF5A2685595345C51876DAAC77CA5FD05E6C5E3D27711E992550DCFC73A8C134"),
        (0xE24A80,234,"2F15E1A872C67337DF2BBE5D78AE0C69D2A206348DD2E1500C9ED557258D7ADC"),
        (0x114B3D0,122,"69EBAC29C1C1AFD4622BF5F14F63DB84CECAC2DBF61859DC9526131CC08C7F82"),
        (0x94FAB0,263,"1D7C23843B4CEFFEA62E06B03B4C13A535144D7FAE54D7C6226DBCC0BB2F00B4"),
        (0x114A640,228,"90A2B6C7157265DCFD42537638A10AC01C8CA862D12D9D3104518C2EBD7BEA7A"),
    ];
}
