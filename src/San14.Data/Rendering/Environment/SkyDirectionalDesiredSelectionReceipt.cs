using San14.Data.Formats;

namespace San14.Data;

/// <summary>Desired Sky2 binding from one Directional source's actual D7
/// nonnull Sun/Moon setter receipts and an explicit complete owned renderer
/// feature. This does not commit shader state or prove GPU program support.</summary>
public sealed class SkyDirectionalDesiredSelectionReceipt
{
    private readonly SkyDirectionalSelectedLightSource source;
    private readonly SkySceneLights lights;
    private readonly SkySceneSourceFramePacket? appliedFrame;
    private readonly long setterRevision;
    private readonly Sky2Selection selection;
    private readonly byte[] rendererFeature48;
    private readonly byte parameterCache10B0;
    private readonly byte parameterCacheFDC;

    internal SkyDirectionalDesiredSelectionReceipt(SkyDirectionalSelectedLightSource source,
        LoadedShaderContainer shader, SkySceneLights lights,
        SkySceneSourceFramePacket? appliedFrame, long setterRevision,
        ReadOnlySpan<byte> explicitOwnedRendererFeature48, string rendererFeatureWriterEvidence)
    {
        if (explicitOwnedRendererFeature48.Length != 48 || string.IsNullOrWhiteSpace(rendererFeatureWriterEvidence))
            throw new InvalidDataException("Directional desired selection needs the complete explicit owned renderer feature and its writer evidence.");
        this.source = source; this.lights = lights; this.appliedFrame = appliedFrame;
        this.setterRevision = setterRevision;
        source.ValidateSelectionReceipt(lights, appliedFrame, setterRevision);
        selection = Sky2Selection.Read(shader);
        rendererFeature48 = explicitOwnedRendererFeature48.ToArray();
        parameterCache10B0 = source.SelectedPacket.CompleteLightData.Span[0x10B0];
        parameterCacheFDC = source.SelectedPacket.CompleteLightData.Span[0xFDC];
        RendererFeatureWriterEvidence = rendererFeatureWriterEvidence;
        SourceProvenance = source.SourceProvenance +
            ";118D7E0-nonnull-Moon->desired0=1;118DA90-nonnull-Sun->desired1=0;118CF40-cache10B0/FDC->desired2;" +
            "explicit-owned-renderer-feature48:" + rendererFeatureWriterEvidence + ";desired-only/not-native-commit";
        // Validate the caller's complete feature against both original passes.
        _ = GetSelection(true); _ = GetSelection(false);
    }

    public string SourceProvenance { get; }
    public string RendererFeatureWriterEvidence { get; }
    public SkySceneSelectedViewLightIdentity SelectedIdentity => source.SelectedPacket.Identity;
    public bool SunSetterPresent => lights.SunPresent;
    public bool MoonSetterPresent => lights.MoonPresent;
    public bool HasExactZeroMoonRgb => source.HasExactZeroMoonRgb;
    public ReadOnlyMemory<byte> ExplicitOwnedRendererFeature48 => rendererFeature48.ToArray();

    public void ValidateCurrent(SkyDirectionalSelectedLightSource actualSource)
    {
        if (!ReferenceEquals(source, actualSource))
            throw new InvalidDataException("Directional desired selection belongs to its actual selected source owner.");
        source.ValidateSelectionReceipt(lights, appliedFrame, setterRevision);
    }

    /// <summary>Require the original source-slot selection packet, rather than
    /// a clone with matching payload/labels or another selected View record.</summary>
    public void ValidateSourceSlotSelector(SkySceneSelectedViewLightPacket actualSelectedPacket)
    {
        ValidateCurrent(source);
        if (!ReferenceEquals(source.SelectedPacket, actualSelectedPacket))
            throw new InvalidDataException("Directional desired selection requires its actual SourceSlotSelector packet.");
    }

    public void ValidateAppliedFrame(SkySceneSourceFramePacket actualFrame)
    {
        ValidateCurrent(source);
        if (appliedFrame is null || !ReferenceEquals(appliedFrame, actualFrame))
            throw new InvalidDataException("Directional desired selection requires the actual applied season frame continuation.");
    }

    /// <param name="atmosphere">The actual original caller R8 Boolean:
    /// true selects atmosphere pass0; false selects contribution pass1.</param>
    public Sky2SelectionResult GetSelection(bool atmosphere)
    {
        ValidateCurrent(source);
        return selection.SelectDesired(rendererFeature48, lights.MoonPresent, !lights.SunPresent,
            parameterCache10B0, parameterCacheFDC, atmosphere);
    }

    /// <summary>Preserve the existing exact unused-Moon program capability.
    /// Desired selection remains accurate for a nonnull zero Moon, but its
    /// PS3/10/4 capability cannot authorize PS5/11/6, which read MoonRGB.</summary>
    public void ValidateSelectedMoonProgramScope()
    {
        ValidateCurrent(source);
        if (lights.SourceZeroMoon is null) return;
        foreach (bool atmosphere in new[] { true, false })
        {
            var projected = GetSelection(atmosphere);
            var pixel = projected.Combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
            Sky2ProgramPreparation.RequireUnusedMoonColor(selection.Source.ReadResourceDefinitions(pixel.Entry!.ProgramIndex));
        }
    }
}
