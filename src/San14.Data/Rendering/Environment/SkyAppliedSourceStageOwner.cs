using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Optional Sky stages for a fresh WORK owner and an explicitly imported Main
/// camera. Nonzero scene declarations require a scene producer; this owner
/// does not infer absence for imported runtime state or external WORK edits.
/// </summary>
public sealed class SkyAppliedSourceStageOwner
{
    private readonly string recipe;
    private readonly LoadedMapCameraConstructionSource main;
    private MapSeasonCameraPackage? selectedPackage;
    private SkyAppliedSourceStageFrame? current;
    private long revision;

    private SkyAppliedSourceStageOwner(string locale, string workIdentity, string mainIdentity,
        string recipe, LoadedMapCameraConstructionSource main)
    {
        Locale = locale;
        OwnedWorkIdentity = workIdentity;
        OwnedMainIdentity = mainIdentity;
        this.recipe = recipe;
        this.main = main;
    }

    public string Locale { get; }
    public string OwnedWorkIdentity { get; }
    public string OwnedMainIdentity { get; }

    public static SkyAppliedSourceStageOwner CreateFresh(GameResourceRepository repository, string locale,
        string ownedWorkIdentity, string ownedMainIdentity, LoadedMapCameraConstructionSource actualMain)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedWorkIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedMainIdentity);
        ArgumentNullException.ThrowIfNull(actualMain);
        if (actualMain.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Applied Sky stages require the imported Main camera declaration.");
        RequireNullScene(actualMain.Camera);
        return new(locale, ownedWorkIdentity, ownedMainIdentity,
            RenderSamplerSourceAdapters.ReadAppliedSkyStageNativeRecipe(repository.SourceDirectory, locale), actualMain);
    }

    public SkyAppliedSourceStageFrame PrepareCurrent(MapSourceCreatedSeasonLightingFrame present,
        MapSourceCreatedLightingPacket acceptedLighting)
    {
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(acceptedLighting);
        present.ValidatePreparedLighting(acceptedLighting);
        var input = present.AcceptedSeasonCamera.Inputs;
        if (input.MainFrame.Camera.ExecutableLocale != Locale ||
            acceptedLighting.Binding.CameraOwnerIdentity != OwnedMainIdentity)
            throw new InvalidDataException("Applied Sky stages require this owned Main and locale.");
        if (!ReferenceEquals(input.MainFrame.Camera.Source, main))
            throw new InvalidDataException("Applied Sky stages belong to another imported Main camera generation.");

        var selection = input.Source.Selection;
        selection.Validate();
        if (selection.SecondPackage is not null)
            throw new NotSupportedException("Applied Sky stage owner supports the direct source package branch.");
        var package = selection.FirstPackage;
        if (!ReferenceEquals(package, selectedPackage))
        {
            RequireNullScene(package.Camera);
            selectedPackage = package;
        }

        uint order = BinaryPrimitives.ReadUInt32LittleEndian(present.PreparedCameraPrefix.Span.Slice(0x3B0, 4));
        revision = checked(revision + 1);
        return current = new(this, revision, present, acceptedLighting, (order & 0x80000000) != 0,
            recipe + ";sourceMain=" + main.Resource.Source.ResourceUri +
            ";selectedCamera=" + package.SourceEvidence + ";SkyDomeDisplaysetFactory=B25B4066/type5/0;" +
            "owned-fresh-WORK-no-1F2-edit;scene-null->Sky2-nativepass1/VS0/PS4;work=" + OwnedWorkIdentity +
            ";Main=" + OwnedMainIdentity + ";frame=" + present.FrameNumber);
    }

    internal void Validate(SkyAppliedSourceStageFrame frame, long capturedRevision)
    {
        if (!ReferenceEquals(frame, current) || revision != capturedRevision)
            throw new InvalidDataException("Applied Sky stage receipt is stale or superseded.");
    }

    private static void RequireNullScene(DokObject camera)
    {
        var fields = camera.Properties.Where(property => property.NameHash == 0xB25B4066).ToArray();
        if (camera.Kind != DokObjectKind.Inline || camera.Reference is not null ||
            camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId ||
            fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException("Applied Sky stages require a typed SkyDomeDisplaysetFactory declaration.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue) != 0)
            throw new NotSupportedException("Nonzero SkyDomeDisplaysetFactory requires its actual scene producer.");
    }
}

public sealed class SkyAppliedSourceStageFrame
{
    private readonly SkyAppliedSourceStageOwner owner;
    private readonly long revision;
    internal SkyAppliedSourceStageFrame(SkyAppliedSourceStageOwner owner,long revision,
        MapSourceCreatedSeasonLightingFrame present,MapSourceCreatedLightingPacket lighting,
        bool sky2Enabled,string provenance)
    {this.owner=owner;this.revision=revision;AcceptedPresent=present;AcceptedLighting=lighting;Sky2Enabled=sky2Enabled;SourceProvenance=provenance;}
    public MapSourceCreatedSeasonLightingFrame AcceptedPresent{get;}
    public MapSourceCreatedLightingPacket AcceptedLighting{get;}
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public string OwnedWorkIdentity=>owner.OwnedWorkIdentity;
    public string OwnedMainIdentity=>owner.OwnedMainIdentity;
    public bool ScenePresent=>false;
    public bool WorkContributionEnabled=>false;
    public bool Sky2Enabled{get;}
    public int Sky2Combination=>1;
    public uint Sky2PixelProgram=>4;
    public string SourceProvenance{get;}
    public void ValidateCurrent(MapSourceCreatedLightingPacket lighting,
        MapSeasonCameraSelection selection,SkySceneCurrentCameraParameters camera)
    {
        if(!ReferenceEquals(lighting,AcceptedLighting)||!ReferenceEquals(selection,AcceptedPresent.AcceptedSeasonCamera.Inputs.Source.Selection)||
            camera.FrameNumber!=FrameNumber||camera.PreparedCameraPrefix.Length!=0x3C0||
            (BinaryPrimitives.ReadUInt32LittleEndian(camera.PreparedCameraPrefix.Span.Slice(0x3B0,4))&0x80000000)!=
            (Sky2Enabled?0x80000000:0u))
            throw new InvalidDataException("Applied Sky stage receipt belongs to another accepted lighting, selected package or current Camera gate.");
        owner.Validate(this,revision);
    }
}
