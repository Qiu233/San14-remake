namespace San14.Data;

public static partial class OriginalMainMapCameraController
{
    /// <summary>D550 source clearance guard. A known absent outer terrain owner
    /// bypasses it; present owners use the exact continuous QueryBAA9E0 sampler.
    /// The candidate's existing pitch remains distinct from requested pitch.</summary>
    public static OriginalMainMapCameraTerrainPitchResult QualifyTerrainPitch(
        OriginalMainMapCameraTerrainPitchInput input,
        OriginalUnitWorldGroundHeightReference<OriginalUnitWorldGroundHeightTerrainOwner> currentTerrainOwner,
        string ownedSourceProvenance)
    {
        if(string.IsNullOrWhiteSpace(ownedSourceProvenance))throw new InvalidDataException("Main terrain pitch requires owned current source provenance.");
        if(!double.IsFinite(input.RequestedPitch)||!double.IsFinite(input.MinimumPitch)||!double.IsFinite(input.MaximumPitch))
            throw new InvalidDataException("Main terrain pitch requires finite explicit native bounds and requested pitch.");
        double pitch=input.RequestedPitch;
        if(input.NativeBand>=2)return new(true,pitch,ownedSourceProvenance);
        var owner=currentTerrainOwner.Read("Main D550 outer owner");
        if(owner is null)return new(true,pitch,ownedSourceProvenance);
        var candidate=input.CandidateBeforeRequestedPitch;
        float Clearance(OriginalMainMapCameraControllerState pose)
        {
            var eye=DerivePose(pose).Eye10;
            float height=OriginalUnitWorldGroundHeightScene.QueryBAA9E0(owner,eye.X,eye.Z);
            if(!float.IsFinite(height))throw new InvalidDataException("Main camera terrain sampler returned an unresolved nonfinite height.");
            return S(S(eye.Y,height),1000f);
        }
        float existingClearance=Clearance(candidate);
        if(existingClearance>=0f&&existingClearance<200f&&candidate.PitchC0>pitch)
            return new(true,candidate.PitchC0,ownedSourceProvenance);
        float requestedClearance=Clearance(candidate with {PitchC0=Clamp(pitch,input.MinimumPitch,input.MaximumPitch)});
        if(requestedClearance>0f)return new(true,pitch,ownedSourceProvenance);
        while(true)
        {
            double next=Math.Min(DA(pitch,0.0174533),input.MaximumPitch);
            if(next==pitch&&Math.Abs(DS(pitch,input.MaximumPitch))>=NativeFocusEpsilon)
                throw new InvalidDataException("Original terrain pitch increment cannot progress for these explicit source inputs.");
            pitch=next;
            var pose=candidate with {PitchC0=Clamp(pitch,input.MinimumPitch,input.MaximumPitch)};
            // Native constructs the candidate before this test and does not
            // sample another height when the requested value reaches maximum.
            _=DerivePose(pose);
            if(Math.Abs(DS(pitch,input.MaximumPitch))<NativeFocusEpsilon)
                return new(false,pitch,ownedSourceProvenance);
            if(Clearance(pose)>0f)return new(true,pitch,ownedSourceProvenance);
        }
    }
}
