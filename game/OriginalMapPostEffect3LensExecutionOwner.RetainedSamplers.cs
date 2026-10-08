using San14.Data;

public sealed partial class OriginalMapPostEffect3LensExecutionOwner
{
    private RetainedSamplerWrite? retainedComputeSamplerWrite;
    private sealed record RetainedSamplerWrite(OriginalMapPostEffect3LensExecutionRecipe Recipe,
        OriginalMapPostEffect3ComputeCompletedPass? Compute,OriginalMapPostEffect3LensGhostCompletedDraw? Ghost,
        IReadOnlyList<OriginalMapPostEffect3LensFlareBeforeTextureSubmission> Submissions);

    // Called only after our actual executor published its private successful
    // submission. Merely preparing Before or compiling an RDEF is insufficient.
    private void RetainCompletedNativeSamplerWrites(OriginalMapPostEffect3LensExecutionRecipe recipe,
        OriginalMapPostEffect3ComputeCompletedPass? compute,OriginalMapPostEffect3LensGhostCompletedDraw? ghost)
    {
        if(!recipe.ExecutesPass||recipe.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            recipe.SourceSha256!=Bank.Source.Source.Sha256||
            (compute is null)==(ghost is null))
            throw new InvalidDataException("Retained Lens sampler writes need one actually executed same-source pass.");
        if(compute is not null&&(!ReferenceEquals(Compute.LastCompletedPass,compute)||
            compute.FrameNumber!=recipe.FrameNumber||compute.SourcePassIndex!=recipe.SourcePassIndex||
            compute.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||compute.Dispatch!=recipe.ActualDispatch||
            compute.Dispatch.X==0||compute.Dispatch.Y==0||compute.Dispatch.Z==0||
            compute.ProgramSha256!=Bank.Source.Source.GetProgram(5,
                Bank.Source.Source.Passes[recipe.SourcePassIndex].ProgramReferences.Single(p=>p.StageCode==5).StageIndex!.Value).Sha256))
            throw new InvalidDataException("Retained Lens CS selection did not come from this private completed dispatch.");
        if(ghost is not null&&(!ReferenceEquals(Ghost.LastCompletedDraw,ghost)||ghost.FrameNumber!=recipe.FrameNumber||
            ghost.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||recipe.SourcePassIndex!=34))
            throw new InvalidDataException("Retained Lens selection did not come from this private completed Ghost submission.");
        var writes=recipe.DescriptorTextureSubmissions.Where(s=>s.NativeStageCode==0&&s.RegisterIndex is 7 or 8).ToArray();
        if(writes.Length==0)return; // A PS setter does not mutate retained CS7/8.
        if(writes.Length!=2||writes.Select(s=>s.RegisterIndex).Distinct().Count()!=2||
            writes.Any(s=>s.Locale!=locale||s.WriterRva!=OriginalMapPostEffect3ParameterLensFlareBefore.NativeShaderResourceWriterRva(locale)||
                s.Role!=OriginalMapPostEffect3BindingRole.ShaderResourceRead||
                s.ParameterIndex is not null||s.DescriptorTextureOffset!=(s.RegisterIndex==7?0x30:0x38)))
            throw new InvalidDataException("Retained Lens CS7/8 source manager setter pair differs.");
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            retainedComputeSamplerWrite=new(recipe,compute,ghost,Array.AsReadOnly(writes));
        }
    }

    public LensRetainedSamplerCapability CaptureRetainedNativeComputeSamplers()
        =>LensRetainedSamplerCapability.Capture(this);

    /// <summary>Private completed native Before setter history and actual
    /// manager lifetime. It intentionally does not retain expired Frame1 World
    /// texture/pin leases or claim a new native Frame2 setter occurred.</summary>
    public sealed class LensRetainedSamplerCapability
    {
        private readonly OriginalMapPostEffect3LensExecutionOwner owner;
        private readonly RetainedSamplerWrite actualWrite;
        private LensRetainedSamplerCapability(OriginalMapPostEffect3LensExecutionOwner owner,RetainedSamplerWrite actualWrite)
            =>(this.owner,this.actualWrite)=(owner,actualWrite);
        internal static LensRetainedSamplerCapability Capture(OriginalMapPostEffect3LensExecutionOwner owner)
        {
            lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                var write=owner.retainedComputeSamplerWrite??
                    throw new InvalidDataException("Lens has not actually submitted native CS7/8 manager setters.");
                return new(owner,write);
            }
        }
        public long NativeWriterFrameNumber=>actualWrite.Recipe.FrameNumber;
        public int NativeWriterSourcePassIndex=>actualWrite.Recipe.SourcePassIndex;
        public int NativeStageOrdinal=>0;
        public string ContextOwnerIdentity=>actualWrite.Recipe.ContextOwnerIdentity;
        public OriginalMapPostEffect3SourceSamplerOwner ActualManagerSamplerOwner=>owner.Samplers;
        public string SourceEvidence=>actualWrite.Recipe.SourceEvidence+
            $";actual-completed-native-CS-before1163970-manager30/38-writer-frame{NativeWriterFrameNumber}/pass{NativeWriterSourcePassIndex};retained-sampler-state-only";
        private void RequireSnapshot()
        {
            lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                if(!ReferenceEquals(owner.retainedComputeSamplerWrite,actualWrite)||
                    ContextOwnerIdentity!=owner.Bank.Source.ContextOwnerIdentity||actualWrite.Submissions.Count!=2)
                    throw new InvalidDataException("Retained Lens sampler state was superseded or its manager scope retired.");
            }
        }
        public void RequireCurrent()
        {
            RequireSnapshot();
            // Validate actual manager RID lifetime without borrowing the old
            // recipe's Frame1 source-input/current-texture producer.
            owner.Samplers.LeaseSelected(new RetainedSelection(this,NativeWriterFrameNumber,7),0).RequireCurrent();
            owner.Samplers.LeaseSelected(new RetainedSelection(this,NativeWriterFrameNumber,8),0).RequireCurrent();
        }
        public IOriginalMapPostEffect3ComputeSamplerLease LeaseForOwnedFrame(long actualConsumerFrameNumber,uint sourceRegister)
        {
            RequireCurrent();
            if(actualConsumerFrameNumber<NativeWriterFrameNumber||actualConsumerFrameNumber<=0||sourceRegister is not (7 or 8))
                throw new InvalidDataException("Retained Lens sampler consumer frame/register differs from source history.");
            return owner.Samplers.LeaseSelected(new RetainedSelection(this,actualConsumerFrameNumber,sourceRegister),0);
        }
        private sealed record RetainedSelection(LensRetainedSamplerCapability Capability,long FrameNumber,uint SourceRegister)
            :IOriginalMapPostEffect3ManagerSamplerSelection
        {
            public string ContextOwnerIdentity=>Capability.ContextOwnerIdentity;
            public int NativeStageOrdinal=>0;
            public OriginalMapPostEffect3ManagerSamplerKind Kind=>SourceRegister==7?
                OriginalMapPostEffect3ManagerSamplerKind.Linear:OriginalMapPostEffect3ManagerSamplerKind.Point;
            public string SourceEvidence=>Capability.SourceEvidence+$";actual-owned-consumer-frame{FrameNumber}/CS{SourceRegister};no-new-setter-claim";
            public void RequireCurrent()=>Capability.RequireSnapshot();
        }
    }
}
