using Godot;
using San14.Data;

public sealed partial class OriginalMapFirstWorldNativeSamplerState
{
    private bool shaftContinuationAccepted;
    /// <summary>Native Shaft Before1153F90 reads enabled10 and returns without
    /// sampler writes. Continue only this owner's successful first dispatch;
    /// each subsequent pass joins the same executor's ordered completion.</summary>
    public ShaftContinuation ContinueCompletedFirstShaft(OriginalMapPostEffect3ComputeExecutor executor,
        OriginalMapPostEffect3ComputeCompletedPass first)
    {
        ArgumentNullException.ThrowIfNull(executor);ArgumentNullException.ThrowIfNull(first);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(shaftContinuationAccepted||!dispatchFinished||!dispatchAccepted||current is not{} prefix||
                !ReferenceEquals(executor.Bank,Bank)||!ReferenceEquals(executor.LastCompletedPass,first)||
                first.SourcePassIndex!=1||first.FrameNumber!=prefix.Submitted.Context.FrameNumber||
                first.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                first.ProgramSha256!=Bank.Source.Source.GetProgram(5,0).Sha256)
                throw new InvalidDataException("Shaft sampler continuation needs this actual completed first native CS dispatch.");
            return ShaftContinuation.CaptureCompletedFirst(this,executor,first);
        }
    }
    public sealed class ShaftContinuation
    {
        private readonly OriginalMapFirstWorldNativeSamplerState owner;
        private readonly OriginalMapPostEffect3ComputeExecutor executor;
        private readonly Prefix prefix;
        private OriginalMapPostEffect3ComputeCompletedPass completed;
        private OriginalMapPostEffect3MiddleExecutor? transferredMiddle;
        private readonly object gate=new();
        private ShaftContinuation(OriginalMapFirstWorldNativeSamplerState owner,
            OriginalMapPostEffect3ComputeExecutor executor,Prefix prefix,OriginalMapPostEffect3ComputeCompletedPass first)
            =>(this.owner,this.executor,this.prefix,completed)=(owner,executor,prefix,first);
        internal static ShaftContinuation CaptureCompletedFirst(OriginalMapFirstWorldNativeSamplerState owner,
            OriginalMapPostEffect3ComputeExecutor executor,OriginalMapPostEffect3ComputeCompletedPass first)
        {
            lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                if(owner.shaftContinuationAccepted||!owner.dispatchFinished||!owner.dispatchAccepted||owner.current is not{} prefix||
                    !ReferenceEquals(executor.Bank,owner.Bank)||!ReferenceEquals(executor.LastCompletedPass,first)||
                    first.SourcePassIndex!=1||first.FrameNumber!=prefix.Submitted.Context.FrameNumber||
                    first.ContextOwnerIdentity!=owner.Bank.Source.ContextOwnerIdentity||
                    first.ProgramSha256!=owner.Bank.Source.Source.GetProgram(5,0).Sha256)
                    throw new InvalidDataException("Cannot mint a Shaft sampler continuation without its actual private first dispatch.");
                owner.shaftContinuationAccepted=true;return new(owner,executor,prefix,first);
            }
        }
        public long FrameNumber=>prefix.Submitted.Context.FrameNumber;
        public string ContextOwnerIdentity=>owner.Bank.Source.ContextOwnerIdentity;
        public int LastCompletedSourcePassIndex{get{lock(gate)return completed.SourcePassIndex;}}
        public void RequireCurrent()
        {
            lock(gate)lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                if(transferredMiddle is not null||!ReferenceEquals(owner.current,prefix)||!owner.defaultSampler.IsValid||
                    !ReferenceEquals(executor.LastCompletedPass,completed)||!ReferenceEquals(executor.Bank,owner.Bank)||
                    !ReferenceEquals(owner.RequireGraphicsPrefix(prefix.Submitted,owner.sky,
                        prefix.Exposure,prefix.ExposureDrawCount),prefix.SkyPrefix))
                    throw new InvalidDataException("Original Shaft sampler continuation was interrupted or its native prefix changed.");
            }
        }
        internal OriginalMapPostEffect3ComputeCompletedPass? TransferToMiddle(
            OriginalMapPostEffect3MiddleExecutor actualMiddle,OriginalMapPostEffect3ShaftCompletedFamily actualFamily)
        {
            lock(gate)
            {
                RequireCurrent();
                if(transferredMiddle is not null||completed.SourcePassIndex!=6||actualMiddle.NextSourcePassIndex!=8||
                    actualMiddle.CompletedPasses.Count!=0||!ReferenceEquals(actualMiddle.Compute.Bank,owner.Bank)||
                    actualFamily.ActualStockPass7 is null||actualFamily.ComputeChain.OrderedComputePasses.Count!=6||
                    !ReferenceEquals(actualFamily.ComputeChain.OrderedComputePasses[^1],completed))
                    throw new InvalidDataException("Native Shaft-to-Middle handoff needs this exact completed six-pass source family.");
                bool sameExecutor=ReferenceEquals(actualMiddle.Compute,executor);
                if(!sameExecutor&&actualMiddle.Compute.LastCompletedPass is not null)
                    throw new InvalidDataException("Another middle executor must be unused at the native handoff.");
                transferredMiddle=actualMiddle;
                return sameExecutor?completed:null;
            }
        }
        internal void RequireTransferredCurrent(OriginalMapPostEffect3MiddleExecutor actualMiddle)
        {
            lock(gate)lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                if(!ReferenceEquals(transferredMiddle,actualMiddle)||!ReferenceEquals(owner.current,prefix)||
                    !owner.defaultSampler.IsValid||!ReferenceEquals(actualMiddle.Compute.Bank,owner.Bank)||
                    !ReferenceEquals(owner.RequireGraphicsPrefix(prefix.Submitted,owner.sky,
                        prefix.Exposure,prefix.ExposureDrawCount),prefix.SkyPrefix))
                    throw new InvalidDataException("Native middle handoff lost its actual private source prefix or executor.");
            }
        }
        public IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> LeaseDefaultsForNextPass(
            int sourcePassIndex,IEnumerable<uint> actuallyDeclaredSamplerRegisters)
        {
            ArgumentNullException.ThrowIfNull(actuallyDeclaredSamplerRegisters);
            lock(gate)
            {
                RequireCurrent();uint[] slots=actuallyDeclaredSamplerRegisters.Order().ToArray();
                if(sourcePassIndex!=completed.SourcePassIndex+1||sourcePassIndex is <2 or >6||
                    slots.Distinct().Count()!=slots.Length||slots.Any(s=>s is not (7 or 8)))
                    throw new NotSupportedException("Shaft continuation admits consecutive CS2..6 and unchanged reset slots7/8 only.");
                var previous=completed;
                return Array.AsReadOnly<IOriginalMapPostEffect3ComputeSamplerLease>(slots.Select(s=>
                    (IOriginalMapPostEffect3ComputeSamplerLease)new ContinuedSampler(this,previous,sourcePassIndex,s)).ToArray());
            }
        }
        public void AcceptCompletedNextPass(OriginalMapPostEffect3ComputeCompletedPass next)
        {
            ArgumentNullException.ThrowIfNull(next);lock(gate)lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                if(transferredMiddle is not null||next.SourcePassIndex!=completed.SourcePassIndex+1||next.SourcePassIndex is <2 or >6||
                    next.FrameNumber!=FrameNumber||next.ContextOwnerIdentity!=ContextOwnerIdentity||
                    !ReferenceEquals(executor.LastCompletedPass,next)||
                    next.ProgramSha256!=owner.Bank.Source.Source.GetProgram(5,next.SourcePassIndex-1).Sha256)
                    throw new InvalidDataException("Shaft sampler progression needs the next actual same-owner successful source CS pass.");
                completed=next;
            }
        }
        private sealed record ContinuedSampler(ShaftContinuation Scope,OriginalMapPostEffect3ComputeCompletedPass Previous,
            int NextPass,uint SourceRegister):IOriginalMapPostEffect3ComputeSamplerLease
        {
            public long FrameNumber=>Scope.FrameNumber;
            public string ContextOwnerIdentity=>Scope.ContextOwnerIdentity;
            public int NativeStageOrdinal=>0;
            public Rid SamplerRid=>Scope.owner.RequireSelectedSamplerRid(SourceRegister);
            public string SourceEvidence=>Scope.owner.Source.SourceEvidence+";"+Previous.SourceEvidence+
                ";actual-consecutive-Shaft="+NextPass+";1153F90-no-sampler-write;retained-CS-reset-slot="+SourceRegister;
            public void RequireCurrent()
            {
                // Compute clears LastCompletedPass while executing this pass.
                // The unchanged preceding receipt remains the ordered anchor.
                lock(Scope.gate)lock(Scope.owner.lifetime)
                {
                    ObjectDisposedException.ThrowIf(Scope.owner.retiring,Scope.owner);
                    if(!ReferenceEquals(Scope.completed,Previous)||NextPass!=Previous.SourcePassIndex+1||
                        !ReferenceEquals(Scope.owner.current,Scope.prefix)||!SamplerRid.IsValid||
                        Scope.executor.LastCompletedPass is{} actual&&!ReferenceEquals(actual,Previous)||
                        !ReferenceEquals(Scope.owner.RequireGraphicsPrefix(Scope.prefix.Submitted,Scope.owner.sky,
                            Scope.prefix.Exposure,Scope.prefix.ExposureDrawCount),Scope.prefix.SkyPrefix))
                        throw new InvalidDataException("Accepted Shaft sampler no longer follows its exact preceding source pass.");
                }
            }
        }
    }
}
