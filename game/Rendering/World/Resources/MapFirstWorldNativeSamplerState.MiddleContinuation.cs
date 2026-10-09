using Godot;
using San14.Data;

public sealed partial class MapFirstWorldNativeSamplerState
{
    private bool middleContinuationAccepted;
    internal IMapPostEffect3MiddleNativeSamplerScope CaptureMiddleContinuation(
        MapPostEffect3MiddleExecutor actualMiddle,MapPostEffect3ShaftExecutionOwner actualShaft,
        MapPostEffect3ShaftCompletedFamily family,ShaftContinuation? actualContinuation)
        =>MiddleContinuation.Capture(this,actualMiddle,actualShaft,family,actualContinuation);

    private sealed class MiddleContinuation:IMapPostEffect3MiddleNativeSamplerScope
    {
        private readonly MapFirstWorldNativeSamplerState owner;
        private readonly MapPostEffect3MiddleExecutor middle;
        private readonly MapPostEffect3ShaftExecutionOwner shaft;
        private readonly MapPostEffect3ShaftCompletedFamily family;
        private readonly MapPostEffect3FirstWorldPreparedPrefix prefix;
        private readonly Prefix actualNativePrefix;
        private readonly ShaftContinuation? shaftContinuation;
        private readonly object gate=new();
        private int next=8;
        private MapPostEffect3ComputeCompletedPass? lastMiddle;
        private MiddleContinuation(MapFirstWorldNativeSamplerState owner,
            MapPostEffect3MiddleExecutor middle,MapPostEffect3ShaftExecutionOwner shaft,
            MapPostEffect3ShaftCompletedFamily family,Prefix native,ShaftContinuation? continued)
            =>(this.owner,this.middle,this.shaft,this.family,prefix,actualNativePrefix,shaftContinuation)=
                (owner,middle,shaft,family,family.ComputeChain.Prefix,native,continued);
        internal static MiddleContinuation Capture(MapFirstWorldNativeSamplerState owner,
            MapPostEffect3MiddleExecutor middle,MapPostEffect3ShaftExecutionOwner shaft,
            MapPostEffect3ShaftCompletedFamily family,ShaftContinuation? continued)
        {
            lock(owner.lifetime)
            {
                ObjectDisposedException.ThrowIf(owner.retiring,owner);
                var prefix=family.ComputeChain.Prefix;prefix.RequireCurrent();
                if(owner.middleContinuationAccepted||!ReferenceEquals(shaft.LastCompletedFamily,family)||
                    !ReferenceEquals(middle.Compute.Bank,owner.Bank)||!ReferenceEquals(middle.Parameters,prefix.Parameters)||
                    !ReferenceEquals(middle.Textures,prefix.Textures)||middle.NextSourcePassIndex!=8||
                    middle.CompletedPasses.Count!=0)
                    throw new InvalidDataException("Middle sampler continuation must initialize the exact unused next source executor.");
                if(family.ComputeChain.SourceSkippedAllShaftPasses)
                {
                    if(prefix.Result.Shaft.BeforeHookAllowsPasses||family.ActualStockPass7 is not null||
                        family.ComputeChain.OrderedComputePasses.Count!=0||continued is not null||owner.dispatchAccepted||
                        middle.Compute.LastCompletedPass is {} priorDispatch&&priorDispatch.FrameNumber==prefix.Context.FrameNumber)
                        throw new InvalidDataException("Source-skipped Shaft cannot claim an executed native compute prefix.");
                    if(owner.current is null)owner.CaptureFirstWorldPrefix(
                        owner.host.Scene.LastWorldDeferredSubmittedFrame??throw new InvalidDataException("Disabled Shaft has no actual late World submission."),
                        owner.host.Final.Chain.Exposure.LastPacket??throw new InvalidDataException("Disabled Shaft has no actual node1 Exposure."));
                }
                else
                {
                    if(continued is null||family.ActualStockPass7 is null||family.ComputeChain.OrderedComputePasses.Count!=6||
                        !owner.dispatchAccepted||!owner.dispatchFinished||continued.LastCompletedSourcePassIndex!=6)
                        throw new InvalidDataException("Enabled Shaft must actually finish six compute passes and stock7 before Middle.");
                    continued.RequireCurrent();
                }
                var native=owner.current??throw new InvalidDataException("Middle native reset has no actual accepted graphics prefix.");
                if(!ReferenceEquals(native.Submitted.Context,prefix.Context))throw new InvalidDataException("Middle reset prefix belongs to another World frame.");
                owner.middleContinuationAccepted=true;
                var result=new MiddleContinuation(owner,middle,shaft,family,native,continued);
                if(continued is not null)result.lastMiddle=continued.TransferToMiddle(middle,family);
                else result.lastMiddle=middle.Compute.LastCompletedPass;
                result.RequireCurrent();return result;
            }
        }
        public long FrameNumber=>prefix.Context.FrameNumber;
        public string ContextOwnerIdentity=>owner.Bank.Source.ContextOwnerIdentity;
        public string SourceEvidence=>owner.Source.SourceEvidence+";"+prefix.Begin.SourceEvidence+
            (shaftContinuation is null?";actual-source-disabled-Shaft-reset-CS7/8-preserved":";actual-CS1..6-plus-PS7-Shaft-prefix-CS7/8-preserved")+
            ";Middle8..19-counted-wrappers-only0..6;1153F90-no-sampler-write;ordered-native-stage0-continuation";
        public void RequireCurrent()
        {
            lock(gate)lock(owner.lifetime)
            {
                RequireOwners();
                if(middle.NextSourcePassIndex!=next||middle.CompletedPasses.Count!=next-8||
                    !ReferenceEquals(middle.Compute.LastCompletedPass,lastMiddle))
                    throw new InvalidDataException("Middle native sampler progression was bypassed or its actual executor changed.");
            }
        }
        private void RequireOwners()
        {
            ObjectDisposedException.ThrowIf(owner.retiring,owner);prefix.RequireCurrent();
            if(!ReferenceEquals(shaft.LastCompletedFamily,family)||!ReferenceEquals(owner.current,actualNativePrefix)||
                !ReferenceEquals(owner.RequireGraphicsPrefix(actualNativePrefix.Submitted,owner.sky,
                    actualNativePrefix.Exposure,actualNativePrefix.ExposureDrawCount),actualNativePrefix.SkyPrefix)||!owner.defaultSampler.IsValid)
                throw new InvalidDataException("Middle lost its actual World/Exposure/reset/Shaft owner.");
            shaftContinuation?.RequireTransferredCurrent(middle);
        }
        public IReadOnlyList<IMapPostEffect3ComputeSamplerLease> LeaseForPass(
            int sourcePassIndex,IEnumerable<uint> actuallyDeclaredSamplerRegisters)
        {
            lock(gate)
            {
                RequireCurrent();if(sourcePassIndex!=next||next>19)throw new InvalidDataException("Middle sampler scope cannot skip source pass order.");
                var recipe=MapPostEffect3MiddleExecutionRecipe.Prepare(prefix.Parameters,next);
                recipe.RequireCurrent();if(!recipe.ExecutesPass)throw new InvalidDataException("Source-disabled Middle must not lease a sampler for a fabricated dispatch.");
                uint[] registers=actuallyDeclaredSamplerRegisters.Order().ToArray();
                if(registers.Distinct().Count()!=registers.Length||registers.Any(r=>r is not (7 or 8))||
                    owner.Bank.Source.Passes[next].ActiveBindings.Any(b=>b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex is 7 or 8))
                    throw new NotSupportedException("Middle may retain CS7/8 only when its counted wrapper setters cannot overwrite them.");
                return Array.AsReadOnly<IMapPostEffect3ComputeSamplerLease>(registers.Select(r=>
                    (IMapPostEffect3ComputeSamplerLease)new ContinuedSampler(this,next,lastMiddle,r)).ToArray());
            }
        }
        public void AcceptCompleted(MapPostEffect3ComputeCompletedPass completed)
        {
            lock(gate)lock(owner.lifetime)
            {
                RequireOwners();var result=RequireActualNextResult();
                if(!result.Recipe.ExecutesPass||!ReferenceEquals(result.ActualDispatch,completed)||
                    !ReferenceEquals(middle.Compute.LastCompletedPass,completed)||completed.SourcePassIndex!=next||
                    completed.FrameNumber!=FrameNumber||completed.ContextOwnerIdentity!=ContextOwnerIdentity)
                    throw new InvalidDataException("Middle sampler progression needs its actual successful next source dispatch.");
                lastMiddle=completed;next++;
            }
        }
        public void AcceptSkipped(MapPostEffect3MiddleExecutionRecipe sourceDisabledRecipe)
        {
            lock(gate)lock(owner.lifetime)
            {
                RequireOwners();var result=RequireActualNextResult();sourceDisabledRecipe.RequireCurrent();
                if(!ReferenceEquals(result.Recipe,sourceDisabledRecipe)||sourceDisabledRecipe.ExecutesPass||
                    result.ActualDispatch is not null||!ReferenceEquals(middle.Compute.LastCompletedPass,lastMiddle))
                    throw new InvalidDataException("Middle skip must be the real next disabled source recipe, without a fabricated compute receipt.");
                next++;
            }
        }
        private MapPostEffect3MiddlePassResult RequireActualNextResult()
        {
            if(next>19||middle.NextSourcePassIndex!=next+1||middle.CompletedPasses.Count!=next-7)
                throw new InvalidDataException("Middle sampler state cannot advance without its actual one-step source execution.");
            var result=middle.CompletedPasses[^1];result.Recipe.RequireCurrent();
            if(result.Recipe.SourcePassIndex!=next||result.Recipe.FrameNumber!=FrameNumber||
                result.Recipe.ContextOwnerIdentity!=ContextOwnerIdentity||result.Recipe.SourceSha256!=owner.Bank.Source.Source.Sha256)
                throw new InvalidDataException("Middle actual next recipe has another source/frame/module.");
            return result;
        }
        private sealed record ContinuedSampler(MiddleContinuation Scope,int Pass,
            MapPostEffect3ComputeCompletedPass? Previous,uint SourceRegister):IMapPostEffect3ComputeSamplerLease
        {
            public long FrameNumber=>Scope.FrameNumber;
            public string ContextOwnerIdentity=>Scope.ContextOwnerIdentity;
            public int NativeStageOrdinal=>0;
            public Rid SamplerRid=>Scope.owner.RequireSelectedSamplerRid(SourceRegister);
            public string SourceEvidence=>Scope.SourceEvidence+";actual-next-pass="+Pass+";retained-CS-register="+SourceRegister;
            public void RequireCurrent()
            {
                lock(Scope.gate)lock(Scope.owner.lifetime)
                {
                    Scope.RequireOwners();
                    if(Scope.next!=Pass||!ReferenceEquals(Scope.lastMiddle,Previous)||
                        Scope.middle.NextSourcePassIndex!=Pass||Scope.middle.Compute.LastCompletedPass is{} now&&!ReferenceEquals(now,Previous))
                        throw new InvalidDataException("Middle queued sampler lost its exact preceding dispatch and source order.");
                }
            }
        }
    }
}
