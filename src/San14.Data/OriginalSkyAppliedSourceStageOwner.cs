using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Fresh owned WORK and applied source Main Camera optional stage
/// gates. Canonical zero SkyDomeDisplaysetFactory follows the actual loader,
/// reference projection and Camera copy. This scope has no runtime scene or
/// Work1F2 edits; it does not infer original persistent runtime absence.</summary>
public sealed class OriginalSkyAppliedSourceStageOwner
{
    private readonly string recipe;
    private OriginalSkyAppliedSourceStageFrame? current;
    private long revision;
    private OriginalSkyAppliedSourceStageOwner(string locale,string work,string main,string recipe)
    {Locale=locale;OwnedWorkIdentity=work;OwnedMainIdentity=main;this.recipe=recipe;}
    public string Locale{get;}
    public string OwnedWorkIdentity{get;}
    public string OwnedMainIdentity{get;}
    public static OriginalSkyAppliedSourceStageOwner CreateFresh(OriginalGameRepository repository,string locale,
        string ownedWorkIdentity,string ownedMainIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedWorkIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedMainIdentity);
        var main=repository.LoadObjects("system",0x908AB6E2);
        ValidateMainSource(main);
        return new(locale,ownedWorkIdentity,ownedMainIdentity,
            OriginalDeferredSamplerSources.ReadAppliedSkyStageNativeRecipe(repository.SourceDirectory,locale));
    }
    public OriginalSkyAppliedSourceStageFrame PrepareCurrent(OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalMapSourceCreatedLightingPacket acceptedLighting)
    {
        ArgumentNullException.ThrowIfNull(present);ArgumentNullException.ThrowIfNull(acceptedLighting);
        present.ValidatePreparedLighting(acceptedLighting);
        var input=present.AcceptedSeasonCamera.Inputs;
        if(input.MainFrame.Camera.ExecutableLocale!=Locale||acceptedLighting.Binding.CameraOwnerIdentity!=OwnedMainIdentity)
            throw new InvalidDataException("Applied Sky stages require this exact owned Main and locale.");
        ValidateMainSource(input.MainFrame.Camera.Source.Resource);
        var selection=input.Source.Selection;selection.Validate();
        if(selection.SecondPackage is not null)
            throw new NotSupportedException("Applied Sky stage owner closes the direct source package branch.");
        var canonical=OriginalSkySeasonPackageTextures.Read(selection.FirstPackage).Package;
        RequireNullScene(canonical.Camera);
        uint order=BinaryPrimitives.ReadUInt32LittleEndian(present.PreparedCameraPrefix.Span.Slice(0x3B0,4));
        revision=checked(revision+1);
        return current=new(this,revision,present,acceptedLighting,(order&0x80000000)!=0,
            recipe+";sourceMain="+input.MainFrame.Camera.Source.Resource.Source.ResourceUri+
            ";selectedCamera="+canonical.SourceEvidence+";SkyDomeDisplaysetFactory=B25B4066/type5/ordinal157/0;"+
            "owned-fresh-WORK-no-1F2-edit;scene-null->Sky2-nativepass1/VS0/PS4;work="+OwnedWorkIdentity+
            ";Main="+OwnedMainIdentity+";frame="+present.FrameNumber);
    }
    internal void Validate(OriginalSkyAppliedSourceStageFrame frame,long capturedRevision)
    {if(!ReferenceEquals(frame,current)||revision!=capturedRevision)throw new InvalidDataException("Applied Sky stage receipt is stale or superseded.");}
    private static void ValidateMainSource(LoadedObjects main)
    {
        var s=main.Source;
        if(s.ResourceUri!="san14://rdb/system/908AB6E2"||s.OverrideSource is not null||
            s.DecodedSha256!="3CB072E68CA9A2086D0230E3F9F86AD1B9B10E08CEA521A474CE1057A0E23B06"||
            Convert.ToHexString(SHA256.HashData(s.Bytes))!=s.DecodedSha256)
            throw new InvalidDataException("Applied Sky stage Main requires its exact original source.");
        var canonical=DokReader.Read(s.Bytes).Objects.Single(o=>o.ObjectId==OriginalMainMapCameraAdapter.MainCameraObjectId);
        RequireNullScene(canonical);
    }
    private static void RequireNullScene(DokObject camera)
    {
        var fields=camera.Properties.Where(p=>p.NameHash==0xB25B4066).ToArray();
        if(camera.ClassId!=0x23E7C64E||fields.Length!=1||fields[0].Index!=157||fields[0].TypeCode!=5||
            fields[0].ElementCount!=1||fields[0].ElementSize!=4||fields[0].RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue)!=0)
            throw new NotSupportedException("Fresh applied Sky stage requires the actual zero source SkyDomeDisplaysetFactory; nonzero runtime scene needs its producer.");
    }
}

public sealed class OriginalSkyAppliedSourceStageFrame
{
    private readonly OriginalSkyAppliedSourceStageOwner owner;
    private readonly long revision;
    internal OriginalSkyAppliedSourceStageFrame(OriginalSkyAppliedSourceStageOwner owner,long revision,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalMapSourceCreatedLightingPacket lighting,
        bool sky2Enabled,string provenance)
    {this.owner=owner;this.revision=revision;AcceptedPresent=present;AcceptedLighting=lighting;Sky2Enabled=sky2Enabled;SourceProvenance=provenance;}
    public OriginalMapSourceCreatedSeasonLightingFrame AcceptedPresent{get;}
    public OriginalMapSourceCreatedLightingPacket AcceptedLighting{get;}
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public string OwnedWorkIdentity=>owner.OwnedWorkIdentity;
    public string OwnedMainIdentity=>owner.OwnedMainIdentity;
    public bool ScenePresent=>false;
    public bool WorkContributionEnabled=>false;
    public bool Sky2Enabled{get;}
    public int Sky2Combination=>1;
    public uint Sky2PixelProgram=>4;
    public string SourceProvenance{get;}
    public void ValidateCurrent(OriginalMapSourceCreatedLightingPacket lighting,
        OriginalMapSeasonCameraSelection selection,OriginalSkySceneCurrentCameraParameters camera)
    {
        if(!ReferenceEquals(lighting,AcceptedLighting)||!ReferenceEquals(selection,AcceptedPresent.AcceptedSeasonCamera.Inputs.Source.Selection)||
            camera.FrameNumber!=FrameNumber||camera.PreparedCameraPrefix.Length!=0x3C0||
            (BinaryPrimitives.ReadUInt32LittleEndian(camera.PreparedCameraPrefix.Span.Slice(0x3B0,4))&0x80000000)!=
            (Sky2Enabled?0x80000000:0u))
            throw new InvalidDataException("Applied Sky stage receipt belongs to another accepted lighting, selected package or current Camera gate.");
        owner.Validate(this,revision);
    }
}
